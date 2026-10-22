using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Auth;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Verifies that the citizen portal and the organization portal are separate
/// sign-in surfaces: an account may only open the dashboard its roles belong
/// to, and a rejected attempt never leaves a usable session behind.
/// </summary>
public sealed class RoleScopedSignInIntegrationTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9!";

    private sealed record LoginPortalResponse(
        bool Authenticated,
        string Portal,
        IReadOnlyList<string> Roles);

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

    private static async Task EnsureRolesAsync(CivicFlowApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in new[]
                 {
                     AppRoles.Citizen,
                     AppRoles.Staff,
                     AppRoles.SuperAdmin
                 })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(
                    new IdentityRole<Guid>
                    {
                        Id = Guid.NewGuid(),
                        Name = role
                    });
            }
        }
    }

    private static async Task<string> CreateUserAsync(
        CivicFlowApiFactory factory,
        string email,
        string role)
    {
        await EnsureRolesAsync(factory);

        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = ApplicationUser.Create(
            "Portal Test User",
            email,
            DateTimeOffset.UtcNow);

        user.EmailConfirmed = true;

        var result = await userManager.CreateAsync(user, StrongPassword);

        Assert.True(
            result.Succeeded,
            string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));

        await userManager.AddToRoleAsync(user, role);

        return email;
    }

    [Fact]
    public async Task CitizenAccount_CannotSignInToOrganizationPortal()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"citizen-portal-{Guid.NewGuid():N}@example.com",
            AppRoles.Citizen);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<
            Dictionary<string, string>>();

        Assert.NotNull(body);
        Assert.Equal("not_an_organization_account", body!["code"]);

        // The rejected attempt must not leave a session behind.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task StaffAccount_CannotSignInToCitizenPortal()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"staff-portal-{Guid.NewGuid():N}@example.com",
            AppRoles.Staff);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<
            Dictionary<string, string>>();

        Assert.NotNull(body);
        Assert.Equal("not_a_citizen_account", body!["code"]);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task CitizenAccount_CanSignInToCitizenPortal()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"citizen-ok-{Guid.NewGuid():N}@example.com",
            AppRoles.Citizen);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await (await client.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Equal("citizen", Assert.Single(me!.Roles));
    }

    [Fact]
    public async Task StaffAccount_CanSignInToOrganizationPortal()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"staff-ok-{Guid.NewGuid():N}@example.com",
            AppRoles.Staff);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await (await client.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Equal("staff", Assert.Single(me!.Roles));
    }

    [Fact]
    public async Task SuperAdminAccount_CannotSignInToOrganizationPortal()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"superadmin-org-{Guid.NewGuid():N}@example.com",
            AppRoles.SuperAdmin);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<
            Dictionary<string, string>>();

        Assert.NotNull(body);
        Assert.Equal("use_platform_admin_portal", body!["code"]);

        // The rejected attempt must not leave a session behind.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task SuperAdminAccount_CanSignInToPlatformAdminPortal()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"superadmin-ok-{Guid.NewGuid():N}@example.com",
            AppRoles.SuperAdmin);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login/platform-admin",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await (await client.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Equal("superadmin", Assert.Single(me!.Roles));
    }

    [Fact]
    public async Task StaffAccount_CannotSignInToPlatformAdminPortal()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"staff-platform-{Guid.NewGuid():N}@example.com",
            AppRoles.Staff);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login/platform-admin",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<
            Dictionary<string, string>>();

        Assert.NotNull(body);
        Assert.Equal("not_a_platform_admin", body!["code"]);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task CitizenAccount_CannotSignInToPlatformAdminPortal()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"citizen-platform-{Guid.NewGuid():N}@example.com",
            AppRoles.Citizen);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login/platform-admin",
            new { email, password = StrongPassword });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffAccount_CannotAccessCitizenOnlyEndpoints()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"staff-scope-{Guid.NewGuid():N}@example.com",
            AppRoles.Staff);

        await client.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password = StrongPassword });

        // The citizen profile endpoint is restricted to the citizen role.
        var response = await client.GetAsync("/api/citizens/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LoginResponse_ReportsTheResolvedRoles()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        using var client = CreateCookieClient(factory);

        var email = await CreateUserAsync(
            factory,
            $"roles-echo-{Guid.NewGuid():N}@example.com",
            AppRoles.Staff);

        var body = await (await client.PostAsJsonAsync(
                "/api/auth/login/organization",
                new { email, password = StrongPassword }))
            .Content.ReadFromJsonAsync<LoginPortalResponse>();

        Assert.NotNull(body);
        Assert.True(body!.Authenticated);
        Assert.Equal("organization", body.Portal);
        Assert.Contains(AppRoles.Staff, body.Roles);
    }
}
