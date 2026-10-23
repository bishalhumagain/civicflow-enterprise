using System.Net;
using System.Net.Http.Json;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Organizations;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public sealed class AuthenticatedOrganizationApiIntegrationTests
{
    [Fact]
    public async Task GetMyOrganizations_ReturnsOnlyActiveMembershipsForCurrentUser()
    {
        await using var factory = new CivicFlowApiFactory(
            enableBusinessEndpoints: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        await factory.SeedOrganizationWithoutMembershipAsync();

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/organizations/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var organizations =
            await response.Content.ReadFromJsonAsync<
                IReadOnlyList<OrganizationWorkspaceOption>>();

        var organization = Assert.Single(organizations!);
        Assert.Equal(organizationId, organization.Id);
        Assert.Equal(nameof(OrganizationRole.OrganizationAdmin), organization.Role);
    }

    [Fact]
    public async Task GetCases_WithActiveMembership_ReturnsOk()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCases_WithoutOrganizationHeader_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCases_WithOrganizationWithoutMembership_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            Guid.NewGuid().ToString());

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCases_WithoutAuthenticatedUser_ReturnsUnauthorized()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Anonymous", "true");

        var response = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OrdinaryAuthenticatedStaffClient_CannotAccessSuperAdminEndpoint()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.Viewer,
            PlatformRole.None);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CanReadCases_ButCannotCreateCases()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.Viewer);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var getResponse = await client.GetAsync("/api/cases");

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Viewer test case",
                description = "This request should be forbidden."
            });

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task CaseWorker_CanCreateCases()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "CaseWorker test case",
                description = "This request should be permitted."
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CaseWorker_CannotCreateStaff()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "New Staff Member",
                email = "staff@example.com"
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CanCreateStaff()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "New Staff Member",
                email = "staff@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CannotChangeCaseStatus()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.Viewer);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PatchAsJsonAsync(
            $"/api/cases/{Guid.NewGuid()}/status",
            new { newStatus = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_CannotAssignCase()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.Viewer);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PatchAsJsonAsync(
            $"/api/cases/{Guid.NewGuid()}/assignment",
            new { staffMemberId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    public async Task CaseWorker_CannotChangeStaffActiveStatus(string action)
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PatchAsync(
            $"/api/staff/{Guid.NewGuid()}/{action}",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CanDeactivateAndReactivateStaff()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Lifecycle Test Staff",
                email = "lifecycle.staff@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var staffJson = System.Text.Json.JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var staffId = staffJson.RootElement.GetProperty("id").GetGuid();

        var deactivateResponse = await client.PatchAsync(
            $"/api/staff/{staffId}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var reactivateResponse = await client.PatchAsync(
            $"/api/staff/{staffId}/reactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);
    }

    [Fact]
    public async Task CaseFromAnotherOrganization_IsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var firstOrganizationId =
            await factory.SeedAuthenticatedOrganizationAsync(
                OrganizationRole.OrganizationAdmin);

        var secondOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync(
                OrganizationRole.OrganizationAdmin);

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            firstOrganizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "First Organization Case",
                description = "Belongs only to the first organization."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var caseJson = System.Text.Json.JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var caseId = caseJson.RootElement.GetProperty("id").GetGuid();

        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            secondOrganizationId.ToString());

        var getResponse = await client.GetAsync($"/api/cases/{caseId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/status",
            new { newStatus = 1 });

        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }

    [Fact]
    public async Task StaffFromAnotherOrganization_IsNotIncludedInStaffList()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var firstOrganizationId =
            await factory.SeedAuthenticatedOrganizationAsync(
                OrganizationRole.OrganizationAdmin);

        var secondOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync(
                OrganizationRole.OrganizationAdmin);

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            firstOrganizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "First Organization Staff",
                email = "first.organization.staff@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            secondOrganizationId.ToString());

        var listResponse = await client.GetAsync("/api/staff");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = System.Text.Json.JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync());

        var staffNames = listJson.RootElement
            .EnumerateArray()
            .Select(item => item.GetProperty("fullName").GetString())
            .ToList();

        Assert.DoesNotContain(
            "First Organization Staff",
            staffNames);
    }
    [Fact]
    public async Task UserWithoutMembership_CannotAccessCasesOrStaff()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);

        var organizationWithoutMembershipId =
            await factory.SeedOrganizationWithoutMembershipAsync();

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationWithoutMembershipId.ToString());

        var casesResponse = await client.GetAsync("/api/cases");
        var staffResponse = await client.GetAsync("/api/staff");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            casesResponse.StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            staffResponse.StatusCode);
    }

}
