using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Covers the staff account provisioning endpoint, which is the only way to
/// create an organization login. The request contract is a positional record,
/// so its validation metadata has to sit on the primary constructor
/// parameters for ASP.NET Core to bind it at all.
/// </summary>
public sealed class StaffAccountApiIntegrationTests
{
    private const string StrongPassword = "Correct-Horse-Battery-9!";
    private const string TemporaryPassword = "Temporary-Staff-2026!";

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

    /// <summary>
    /// Creates an organization administrator that can sign in on the
    /// organization portal and administer the returned organization.
    /// </summary>
    private static async Task<(string Email, Guid OrganizationId)>
        CreateOrganizationAdminAsync(
            CivicFlowApiFactory factory,
            OrganizationRole role = OrganizationRole.OrganizationAdmin)
    {
        await EnsureRolesAsync(factory);

        using var scope = factory.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var email = $"org-admin-{Guid.NewGuid():N}@example.com";

        var user = ApplicationUser.Create(
            "Organization Admin",
            email,
            DateTimeOffset.UtcNow);

        user.EmailConfirmed = true;

        var result = await userManager.CreateAsync(user, StrongPassword);

        Assert.True(
            result.Succeeded,
            string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));

        await userManager.AddToRoleAsync(user, AppRoles.Staff);

        var organization = Organization.Create(
            "Provisioning Test Organization",
            $"PROV-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);

        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMemberships.Add(
            OrganizationMembership.Create(
                user.Id,
                organization.Id,
                role,
                DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync();

        return (email, organization.Id);
    }

    private static async Task<HttpResponseMessage> SignInAsync(
        HttpClient client,
        string email,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return response;
    }

    [Fact]
    public async Task ProvisionStaffAccount_WithValidInput_ReturnsCreated()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var client = CreateCookieClient(factory);

        await SignInAsync(client, adminEmail, StrongPassword);

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var email = $"officer-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(
            "/api/staff/accounts",
            new
            {
                fullName = "Case Officer",
                email,
                temporaryPassword = TemporaryPassword,
                role = "CaseWorker"
            });

        var probe = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"status={response.StatusCode} body={probe}");

        using var json = JsonDocument.Parse(probe);

        var root = json.RootElement;

        Assert.False(root.GetProperty("userId").GetGuid().Equals(Guid.Empty));
        Assert.False(root.GetProperty("membershipId").GetGuid().Equals(Guid.Empty));
        Assert.False(root.GetProperty("staffMemberId").GetGuid().Equals(Guid.Empty));
        Assert.Equal("Case Officer", root.GetProperty("fullName").GetString());
        Assert.Equal("CaseWorker", root.GetProperty("organizationRole").GetString());
    }

    [Fact]
    public async Task ProvisionStaffAccount_CreatesUsableOrganizationLogin()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var adminClient = CreateCookieClient(factory);

        await SignInAsync(adminClient, adminEmail, StrongPassword);

        adminClient.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var email = $"signin-{Guid.NewGuid():N}@example.com";

        var response = await adminClient.PostAsJsonAsync(
            "/api/staff/accounts",
            new
            {
                fullName = "Sign In Officer",
                email,
                temporaryPassword = TemporaryPassword,
                role = "CaseWorker"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // The provisioned account must be able to open the organization
        // portal with the temporary password.
        using var staffClient = CreateCookieClient(factory);

        var loginResponse = await staffClient.PostAsJsonAsync(
            "/api/auth/login/organization",
            new { email, password = TemporaryPassword });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var me = await (await staffClient.GetAsync("/api/auth/me"))
            .Content.ReadFromJsonAsync<
                PublicServiceCaseManagement.Api.Features.Auth.CurrentUserResponse>();

        Assert.NotNull(me);
        Assert.Contains(AppRoles.Staff, me!.Roles);
    }

    [Fact]
    public async Task ProvisionStaffAccount_WithInvalidInput_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var client = CreateCookieClient(factory);

        await SignInAsync(client, adminEmail, StrongPassword);

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/staff/accounts",
            new
            {
                fullName = "Weak Password Officer",
                email = $"weak-{Guid.NewGuid():N}@example.com",
                temporaryPassword = "short",
                role = "CaseWorker"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProvisionStaffAccount_WithUnknownRole_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(factory);

        using var client = CreateCookieClient(factory);

        await SignInAsync(client, adminEmail, StrongPassword);

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/staff/accounts",
            new
            {
                fullName = "Unknown Role Officer",
                email = $"role-{Guid.NewGuid():N}@example.com",
                temporaryPassword = TemporaryPassword,
                role = "NotARole"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProvisionStaffAccount_WithoutOrganizationAdmin_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory
        {
            UseTestAuthentication = false
        };

        var (adminEmail, organizationId) =
            await CreateOrganizationAdminAsync(
                factory,
                OrganizationRole.CaseWorker);

        using var client = CreateCookieClient(factory);

        await SignInAsync(client, adminEmail, StrongPassword);

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/staff/accounts",
            new
            {
                fullName = "Unprivileged Officer",
                email = $"unprivileged-{Guid.NewGuid():N}@example.com",
                temporaryPassword = TemporaryPassword,
                role = "CaseWorker"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
