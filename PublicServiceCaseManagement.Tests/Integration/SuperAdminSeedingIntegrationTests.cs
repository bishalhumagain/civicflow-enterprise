using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Auth;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;
using PublicServiceCaseManagement.Api.Features.Auth.Contracts;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

/// <summary>
/// Covers the super-administrator seeding contract: a configured seed must
/// produce an account that can sign in to the platform portal, a broken seed
/// must fail startup loudly, and an existing half-configured account must be
/// repaired rather than silently skipped.
/// </summary>
public sealed class SuperAdminSeedingIntegrationTests
{
    private const string StrongPassword = "Seeded-Platform-Admin-2026!";

    private static HttpClient CreateCookieClient(
        WebApplicationFactory<Program> factory)
    {
        var handler = factory.Server.CreateHandler();
        var cookieHandler = new CookieContainerHandler(handler);

        return new HttpClient(cookieHandler)
        {
            BaseAddress = new Uri("http://localhost")
        };
    }

    private static CivicFlowApiFactory CreateFactory(
        string? email,
        string? password,
        string? fullName = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["SeedAdmin:Email"] = email,
            ["SeedAdmin:Password"] = password,
            ["SeedAdmin:FullName"] = fullName
        };

        return new CivicFlowApiFactory(settings: settings)
        {
            UseTestAuthentication = false
        };
    }

    [Fact]
    public async Task SeedAdmin_CreatesAccountThatCanSignInToPlatformPortal()
    {
        var email = $"seeded-admin-{Guid.NewGuid():N}@example.com";

        await using var factory = CreateFactory(email, StrongPassword);

        using var client = CreateCookieClient(factory);

        var platformLogin = await client.PostAsJsonAsync(
            "/api/auth/login/platform-admin",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, platformLogin.StatusCode);

        var me = await (await client.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Equal("Platform Administrator", me!.FullName);
        Assert.True(me.EmailConfirmed);
        Assert.Contains(AppRoles.SuperAdmin, me.Roles);
        Assert.Equal(nameof(PlatformRole.SuperAdmin), me.PlatformRole);

        // The same credentials must not open the organization portal.
        using var organizationClient = CreateCookieClient(factory);

        var organizationLogin = await organizationClient.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, organizationLogin.StatusCode);
    }

    [Fact]
    public async Task SeedAdmin_WithWeakPassword_FailsStartupWithActionableMessage()
    {
        await using var factory = CreateFactory(
            $"weak-seed-{Guid.NewGuid():N}@example.com",
            "short");

        var exception = Assert.ThrowsAny<Exception>(
            () => factory.CreateClient());

        Assert.Contains(
            "SeedAdmin",
            exception.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "password",
            exception.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeedAdmin_WithMissingPassword_FailsStartup()
    {
        await using var factory = CreateFactory(
            $"partial-seed-{Guid.NewGuid():N}@example.com",
            password: null);

        var exception = Assert.ThrowsAny<Exception>(
            () => factory.CreateClient());

        Assert.Contains(
            "partially configured",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedAdmin_WhenNotConfigured_SkipsSeeding()
    {
        await using var factory = CreateFactory(email: null, password: null);

        using var client = CreateCookieClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login/platform-admin",
            new
            {
                email = $"nobody-{Guid.NewGuid():N}@example.com",
                password = StrongPassword
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SeedAdmin_RepairsExistingAccountThatLostPlatformAccess()
    {
        var email = $"repair-seed-{Guid.NewGuid():N}@example.com";

        await using var factory = CreateFactory(email, StrongPassword);

        using var client = CreateCookieClient(factory);

        // Simulate an account left behind by an older seeding rule: the
        // password works, but the platform role, confirmation state, and role
        // membership are wrong.
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByEmailAsync(email);

            Assert.NotNull(user);

            await userManager.RemoveFromRoleAsync(
                user!,
                AppRoles.SuperAdmin);

            user.PlatformRole = PlatformRole.None;
            user.EmailConfirmed = false;

            var update = await userManager.UpdateAsync(user);
            Assert.True(update.Succeeded);
        }

        await IdentityDataSeeder.SeedAsync(factory.Services);

        using var repairedClient = CreateCookieClient(factory);

        var login = await repairedClient.PostAsJsonAsync(
            "/api/auth/login/platform-admin",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var me = await (await repairedClient.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.True(me!.EmailConfirmed);
        Assert.Contains(AppRoles.SuperAdmin, me.Roles);
    }

    [Fact]
    public async Task SeedAdmin_WithDifferentPassword_DoesNotOverwriteStoredPassword()
    {
        var email = $"unchanged-seed-{Guid.NewGuid():N}@example.com";

        await using var factory = CreateFactory(email, StrongPassword);

        using var client = CreateCookieClient(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);

            // An operator changed the password through the normal flow.
            var remove = await userManager.RemovePasswordAsync(user!);
            Assert.True(remove.Succeeded);

            var add = await userManager.AddPasswordAsync(
                user!,
                "Operator-Changed-Password-1!");
            Assert.True(add.Succeeded);
        }

        // Restarting the seeder with the old configured password must not
        // silently revert the operator's change.
        await IdentityDataSeeder.SeedAsync(factory.Services);

        using var clientWithNewPassword = CreateCookieClient(factory);

        var newPasswordLogin = await clientWithNewPassword.PostAsJsonAsync(
            "/api/auth/login/platform-admin",
            new { email, password = "Operator-Changed-Password-1!" });

        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);

        using var clientWithSeedPassword = CreateCookieClient(factory);

        var seedPasswordLogin = await clientWithSeedPassword.PostAsJsonAsync(
            "/api/auth/login/platform-admin",
            new { email, password = StrongPassword });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            seedPasswordLogin.StatusCode);
    }
}
