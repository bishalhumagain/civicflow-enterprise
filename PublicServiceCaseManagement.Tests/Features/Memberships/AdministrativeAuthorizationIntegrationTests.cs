
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Features.Memberships;

public sealed class AdministrativeAuthorizationIntegrationTests
{
    [Fact]
    public async Task CaseWorker_CannotCreateMembership()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        var userId = await SeedTargetUserAsync(factory);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/memberships",
            new { userId, role = "Viewer" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaseWorker_CannotChangeMembershipRole()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        var userId = await SeedTargetUserAsync(factory);
        var membershipId = await CreateMembershipAsync(
            factory, organizationId, userId);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}/memberships/{membershipId}/role",
            new { role = "CaseWorker" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaseWorker_CannotDeactivateMembership()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        var userId = await SeedTargetUserAsync(factory);
        var membershipId = await CreateMembershipAsync(
            factory, organizationId, userId);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PatchAsync(
            $"/api/organizations/{organizationId}/memberships/{membershipId}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CannotAdministerDifferentOrganization()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        var otherOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync();
        using var client = CreateClient(factory, organizationId);

        var response = await client.GetAsync(
            $"/api/organizations/{otherOrganizationId}/memberships");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CannotCreateOrganization()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new
            {
                name = "Unauthorized Organization",
                code = $"NO-{Guid.NewGuid():N}"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CannotUpdateOrganization()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}",
            new
            {
                name = "Unauthorized Update",
                code = $"UP-{Guid.NewGuid():N}"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanCreateOrganization()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.Viewer,
            PlatformRole.SuperAdmin);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new
            {
                name = "Authorized Organization",
                code = $"SA-{Guid.NewGuid():N}"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanListOrganizations()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.Viewer,
            PlatformRole.SuperAdmin);
        using var client = CreateClient(factory, organizationId);

        var response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CanCreateMembershipInOwnOrganization()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        var userId = await SeedTargetUserAsync(factory);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/memberships",
            new { userId, role = "Viewer" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CannotCreateAnotherOrganizationAdminMembership()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        var userId = await SeedTargetUserAsync(factory);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/memberships",
            new { userId, role = nameof(OrganizationRole.OrganizationAdmin) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CannotPromoteMembershipToOrganizationAdmin()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        var userId = await SeedTargetUserAsync(factory);
        var membershipId = await CreateMembershipAsync(
            factory,
            organizationId,
            userId);
        using var client = CreateClient(factory, organizationId);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}/memberships/{membershipId}/role",
            new { role = nameof(OrganizationRole.OrganizationAdmin) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_ListsOnlyManagedOrganizations()
    {
        using var factory = new CivicFlowApiFactory(true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.None);
        using var client = CreateClient(factory, organizationId);

        var response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var organization = Assert.Single(
            json.RootElement.EnumerateArray());
        Assert.Equal(
            organizationId,
            organization.GetProperty("id").GetGuid());
    }

    private static HttpClient CreateClient(
        CivicFlowApiFactory factory,
        Guid organizationId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id", organizationId.ToString());
        return client;
    }

    private static async Task<Guid> CreateMembershipAsync(
        CivicFlowApiFactory factory,
        Guid organizationId,
        Guid userId)
    {
        using var scope = factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var membership = OrganizationMembership.Create(
            userId,
            organizationId,
            OrganizationRole.Viewer,
            DateTimeOffset.UtcNow);

        dbContext.OrganizationMemberships.Add(membership);
        await dbContext.SaveChangesAsync();

        return membership.Id;
    }
    private static async Task<Guid> SeedTargetUserAsync(
        CivicFlowApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var user = ApplicationUser.Create(
            "Membership Target User",
            $"target-{Guid.NewGuid():N}@example.com",
            DateTimeOffset.UtcNow);

        dbContext.ApplicationUsers.Add(user);
        await dbContext.SaveChangesAsync();

        return user.Id;
    }

    private sealed record MembershipResponse(Guid Id);
}