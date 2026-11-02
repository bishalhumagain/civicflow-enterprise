using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Ensures the Identity role catalogue exists and optionally seeds a
/// super-administrator so the platform is usable without manual database
/// surgery. The credentials come from configuration and are never
/// hard-coded, logged, or returned.
///
/// Seeding is deliberately loud: a partially configured or invalid seed
/// aborts startup instead of leaving the operator with an account that
/// cannot sign in.
/// </summary>
public static class IdentityDataSeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var configuration = scope.ServiceProvider
            .GetRequiredService<IConfiguration>();
        var auditLogger = scope.ServiceProvider
            .GetRequiredService<IAuditLogger>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentityDataSeeder");

        foreach (var roleName in new[]
                 {
                     AppRoles.Citizen,
                     AppRoles.Staff,
                     AppRoles.SuperAdmin
                 })
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var roleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName
                });

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to create the '{roleName}' role: " +
                    DescribeFailures(roleResult));
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"]?.Trim();
        var adminPassword = configuration["SeedAdmin:Password"];
        var adminFullName = configuration["SeedAdmin:FullName"]?.Trim();

        var emailConfigured = !string.IsNullOrWhiteSpace(adminEmail);
        var passwordConfigured = !string.IsNullOrWhiteSpace(adminPassword);

        if (!emailConfigured && !passwordConfigured)
        {
            logger.LogInformation(
                "SeedAdmin is not configured; skipping platform administrator " +
                "seeding. Set SeedAdmin:Email and SeedAdmin:Password (for " +
                "example via SeedAdmin__Email and SeedAdmin__Password) to seed " +
                "one.");
            return;
        }

        if (!emailConfigured || !passwordConfigured)
        {
            throw new InvalidOperationException(
                "SeedAdmin is partially configured. Set both SeedAdmin:Email " +
                "and SeedAdmin:Password, or leave both empty to skip seeding.");
        }

        var existing = await FindAdministratorAsync(
            userManager,
            dbContext,
            adminEmail!);

        if (existing is null)
        {
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(
                    cancellationToken)
                : null;
            var admin = ApplicationUser.Create(
                string.IsNullOrWhiteSpace(adminFullName)
                    ? "Platform Administrator"
                    : adminFullName!,
                adminEmail!,
                TimeProvider.System.GetUtcNow());

            admin.EmailConfirmed = true;
            admin.SetPlatformRole(
                PlatformRole.SuperAdmin,
                admin.CreatedAt);

            var createResult = await userManager.CreateAsync(
                admin,
                adminPassword!);

            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to seed the platform administrator '{adminEmail}': " +
                    DescribeFailures(createResult) +
                    " Check SeedAdmin:Password against the configured password " +
                    "policy (at least 12 characters with upper case, lower " +
                    "case, a digit, and a symbol).");
            }

            var addRoleResult = await userManager.AddToRoleAsync(
                admin,
                AppRoles.SuperAdmin);

            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to grant the super administrator role to " +
                    $"'{adminEmail}': {DescribeFailures(addRoleResult)}");
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Seeded the platform administrator account {Email} ({UserId}). " +
                "Sign in at /super-platform-admin.",
                adminEmail,
                admin.Id);

            await auditLogger.RecordAsync(
                new AuditRecord
                {
                    Category = AuditCategory.Account,
                    Action = AuditActions.PlatformRoleChanged,
                    EntityType = "Account",
                    EntityId = admin.Id.ToString(),
                    Details = "platform_role: None -> SuperAdmin via SeedAdmin"
                },
                cancellationToken);

            return;
        }

        var hadPlatformRole = existing.PlatformRole == PlatformRole.SuperAdmin;
        var hadIdentityRole = await userManager.IsInRoleAsync(
            existing,
            AppRoles.SuperAdmin);
        var passwordMatches = await userManager.CheckPasswordAsync(
            existing,
            adminPassword!);

        if (!passwordMatches)
        {
            if (hadPlatformRole &&
                hadIdentityRole &&
                configuration.GetValue<bool>("SeedAdmin:ResetPassword"))
            {
                await ResetPasswordAsync(
                    userManager,
                    existing,
                    adminPassword!,
                    adminEmail!);

                logger.LogWarning(
                    "Reset the password of the already-provisioned platform " +
                    "administrator {Email} from SeedAdmin:Password. Remove " +
                    "SeedAdmin:ResetPassword after this startup.",
                    adminEmail);
            }
            else if (hadPlatformRole && hadIdentityRole)
            {
                logger.LogWarning(
                    "The provisioned platform administrator {Email} exists, " +
                    "but SeedAdmin:Password does not match the stored password. " +
                    "The configured password was NOT applied.",
                    adminEmail);
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    $"The configured SeedAdmin account '{adminEmail}' already " +
                    "exists but its credentials do not verify. It was not " +
                    "granted platform privileges or marked as verified. " +
                    "Provision the account through a trusted process first.");
            }
        }

        // Email equality alone is not proof of ownership: public registration
        // permits an account to exist before its email has been verified.
        await using var existingAccountTransaction =
            dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(
                    cancellationToken)
                : null;
        var roleChanged = !hadPlatformRole || !hadIdentityRole;
        var changed = false;
        var now = TimeProvider.System.GetUtcNow();

        if (existing.PlatformRole != PlatformRole.SuperAdmin)
        {
            existing.SetPlatformRole(PlatformRole.SuperAdmin, now);
            changed = true;
        }

        if (!existing.EmailConfirmed)
        {
            existing.EmailConfirmed = true;
            changed = true;
        }

        if (!existing.IsActive)
        {
            existing.Reactivate(now);
            changed = true;
        }

        if (!string.Equals(
                existing.NormalizedEmail,
                adminEmail!.ToUpperInvariant(),
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(existing.NormalizedEmail))
        {
            existing.Email = adminEmail;
            existing.NormalizedEmail = adminEmail.ToUpperInvariant();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(existing.UserName))
        {
            existing.UserName = adminEmail;
            existing.NormalizedUserName = adminEmail.ToUpperInvariant();
            changed = true;
        }

        if (changed)
        {
            var updateResult = await userManager.UpdateAsync(existing);

            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to repair the platform administrator " +
                    $"'{adminEmail}': {DescribeFailures(updateResult)}");
            }

            logger.LogWarning(
                "Repaired the existing platform administrator account {Email} " +
                "({UserId}) so it can sign in at /super-platform-admin.",
                adminEmail,
                existing.Id);
        }

        if (!hadIdentityRole)
        {
            var addRoleResult = await userManager.AddToRoleAsync(
                existing,
                AppRoles.SuperAdmin);

            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to grant the super administrator role to " +
                    $"'{adminEmail}': {DescribeFailures(addRoleResult)}");
            }

            logger.LogWarning(
                "Granted the super administrator role to the existing account " +
                "{Email} ({UserId}).",
                adminEmail,
                existing.Id);
        }

        if (roleChanged)
        {
            var stampResult = await userManager.UpdateSecurityStampAsync(
                existing);
            if (!stampResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Unable to rotate the security stamp after provisioning " +
                    "the platform administrator.");
            }
        }

        if (existingAccountTransaction is not null)
        {
            await existingAccountTransaction.CommitAsync(cancellationToken);
        }

        if (roleChanged)
        {
            await auditLogger.RecordAsync(
                new AuditRecord
                {
                    Category = AuditCategory.Account,
                    Action = AuditActions.PlatformRoleChanged,
                    EntityType = "Account",
                    EntityId = existing.Id.ToString(),
                    Details =
                        "platform_role: " +
                        $"{(hadPlatformRole ? PlatformRole.SuperAdmin : PlatformRole.None)} " +
                        "-> SuperAdmin via SeedAdmin"
                },
                cancellationToken);
        }
    }

    private static async Task<ApplicationUser?> FindAdministratorAsync(
        UserManager<ApplicationUser> userManager,
        CivicFlowDbContext dbContext,
        string adminEmail)
    {
        var byNormalizedEmail = await userManager.FindByEmailAsync(adminEmail);

        if (byNormalizedEmail is not null)
        {
            return byNormalizedEmail;
        }

        // Legacy rows can have an unset NormalizedEmail, which makes the
        // Identity lookup miss the account that already owns the address.
        var lowerEmail = adminEmail.ToLowerInvariant();

        return await dbContext.ApplicationUsers
            .SingleOrDefaultAsync(
                user => (user.Email ?? string.Empty).ToLower() == lowerEmail);
    }

    private static async Task ResetPasswordAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string password,
        string email)
    {
        if (await userManager.HasPasswordAsync(user))
        {
            var removeResult = await userManager.RemovePasswordAsync(user);

            if (!removeResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to reset the platform administrator password for " +
                    $"'{email}': {DescribeFailures(removeResult)}");
            }
        }

        var addResult = await userManager.AddPasswordAsync(user, password);

        if (!addResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to set the platform administrator password for " +
                $"'{email}': {DescribeFailures(addResult)}");
        }

        await userManager.UpdateSecurityStampAsync(user);
    }

    private static string DescribeFailures(IdentityResult result) =>
        string.Join(
            " ",
            result.Errors
                .Select(error => error.Description)
                .Distinct(StringComparer.Ordinal));
}
