using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Ensures the Identity role catalogue exists and, in development,
/// optionally seeds a super-administrator so the platform is usable
/// without manual database surgery. The super-administrator credentials
/// come from configuration and are never hard-coded.
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
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(
                    new IdentityRole<Guid>
                    {
                        Id = Guid.NewGuid(),
                        Name = roleName
                    });
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) ||
            string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var existing = await userManager.FindByEmailAsync(adminEmail);

        if (existing is not null)
        {
            return;
        }

        var admin = ApplicationUser.Create(
            configuration["SeedAdmin:FullName"] ?? "Platform Administrator",
            adminEmail,
            TimeProvider.System.GetUtcNow());

        admin.EmailConfirmed = true;
        admin.SetPlatformRole(PlatformRole.SuperAdmin, admin.CreatedAt);

        var result = await userManager.CreateAsync(admin, adminPassword);

        if (!result.Succeeded)
        {
            logger.LogError(
                "Failed to seed the platform administrator: {Errors}",
                string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description)));

            return;
        }

        await userManager.AddToRoleAsync(admin, AppRoles.SuperAdmin);

        logger.LogInformation(
            "Seeded the platform administrator account {Email}.",
            adminEmail);
    }
}
