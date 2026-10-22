using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Users;
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

            logger.LogInformation(
                "Seeded the platform administrator account {Email} ({UserId}). " +
                "Sign in at /super-platform-admin.",
                adminEmail,
                admin.Id);

            return;
        }

        // An existing account may have been created before the seeding rules
        // were tightened, or may have been left half-configured. Repair the
        // attributes that block platform administration instead of returning
        // silently and leaving the operator unable to sign in.
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

        if (!await userManager.IsInRoleAsync(existing, AppRoles.SuperAdmin))
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

        if (!await userManager.CheckPasswordAsync(existing, adminPassword!))
        {
            if (configuration.GetValue<bool>("SeedAdmin:ResetPassword"))
            {
                await ResetPasswordAsync(
                    userManager,
                    existing,
                    adminPassword!,
                    adminEmail!);

                logger.LogWarning(
                    "Reset the password of the seeded platform administrator " +
                    "{Email} from SeedAdmin:Password. Remove " +
                    "SeedAdmin:ResetPassword after this startup.",
                    adminEmail);

                return;
            }

            logger.LogWarning(
                "The seeded platform administrator {Email} exists and is " +
                "configured correctly, but SeedAdmin:Password does not match " +
                "the stored password. The configured password was NOT applied. " +
                "Set SeedAdmin:ResetPassword=true once to synchronise it, or " +
                "use the password reset flow.",
                adminEmail);
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
