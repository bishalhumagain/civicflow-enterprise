
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Covers the platform administration surface: organization deletion, the
/// platform directory, and the audit trail endpoints.
/// </summary>
public sealed class PlatformAdministrationApiIntegrationTests
{
    [Fact]
    public async Task DeleteOrganization_AsSuperAdmin_RemovesOrganization()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Disposable Organization", code = "DISP-ONE" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync(
            $"/api/organizations/{id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/organizations/{id}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteOrganization_WhenOrganizationStillHoldsCases_ReturnsConflict()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.CaseWorker,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Organization With Cases", code = "CASES-ONE" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var organizationId = createdJson.RootElement.GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var now = DateTimeOffset.UtcNow;

            dbContext.Cases.Add(
                PublicServiceCaseManagement.Api.Domain.Cases.Case.Create(
                    organizationId,
                    $"CF-{Guid.NewGuid():N}"[..12],
                    "A case that blocks the deletion",
                    "The organization still holds this case.",
                    now));

            await dbContext.SaveChangesAsync();
        }

        var deleteResponse = await client.DeleteAsync(
            $"/api/organizations/{organizationId}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteOrganization_WithoutSuperAdmin_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync(
            $"/api/organizations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPlatformDirectory_AsSuperAdmin_ReturnsOverviewAndDirectory()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var usersResponse = await client.GetAsync("/api/platform/users");
        var staffResponse = await client.GetAsync("/api/platform/staff");
        var overviewResponse = await client.GetAsync("/api/platform/overview");

        Assert.Equal(HttpStatusCode.OK, usersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, staffResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);

        using var overviewJson = JsonDocument.Parse(
            await overviewResponse.Content.ReadAsStringAsync());

        Assert.True(
            overviewJson.RootElement.GetProperty("userCount").GetInt32() >= 1);

        Assert.True(
            overviewJson.RootElement.GetProperty("staffCount").GetInt32() >= 1);

        using var staffJson = JsonDocument.Parse(
            await staffResponse.Content.ReadAsStringAsync());

        Assert.NotEmpty(staffJson.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task GetPlatformDirectory_WithoutSuperAdmin_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var usersResponse = await client.GetAsync("/api/platform/users");
        var staffResponse = await client.GetAsync("/api/platform/staff");
        var overviewResponse = await client.GetAsync("/api/platform/overview");

        Assert.Equal(HttpStatusCode.Forbidden, usersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, staffResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, overviewResponse.StatusCode);
    }

    [Fact]
    public async Task GetPlatformAudit_AfterOrganizationCreated_ReturnsRecordedEvent()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Audited Organization", code = "AUDIT-ONE" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await client.GetAsync(
            "/api/platform/audit?category=Organization");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var items = json.RootElement.GetProperty("items");

        Assert.True(items.GetArrayLength() >= 1);

        Assert.Contains(
            items.EnumerateArray(),
            item => item.GetProperty("action").GetString() == "organization.created");
    }

    [Fact]
    public async Task GetPlatformAudit_WithoutSuperAdmin_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationAudit_AsOrganizationAdmin_ReturnsScopedEvents()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);

        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", organizationId.ToString());

        var response = await client.GetAsync(
            $"/api/organizations/{organizationId}/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.True(json.RootElement.GetProperty("totalCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetOrganizationAudit_ForAnotherOrganization_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var otherOrganizationId = await factory.SeedOrganizationWithoutMembershipAsync();

        var response = await client.GetAsync(
            $"/api/organizations/{otherOrganizationId}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStaffPhone_AsOrganizationAdmin_SetsPhoneNumber()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Phone Owner",
                email = $"phone-{Guid.NewGuid():N}@example.com",
                phoneNumber = "+9779800000000"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var staffId = createdJson.RootElement.GetProperty("id").GetGuid();

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/staff/{staffId}/phone",
            new { phoneNumber = "+9779812345678" });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var updatedJson = JsonDocument.Parse(
            await updateResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "+9779812345678",
            updatedJson.RootElement.GetProperty("phoneNumber").GetString());

        var listResponse = await client.GetAsync("/api/staff");

        using var listJson = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync());

        var staffMember = Assert.Single(
            listJson.RootElement.EnumerateArray(),
            member => member.GetProperty("id").GetGuid() == staffId);

        Assert.Equal(
            "+9779812345678",
            staffMember.GetProperty("phoneNumber").GetString());
    }

    [Fact]
    public async Task GetPlatformStatistics_AsSuperAdmin_ReturnsCaseStatistics()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/platform");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.True(json.RootElement.GetProperty("totalCases").GetInt32() >= 0);
        Assert.True(json.RootElement.GetProperty("casesCreatedThisWeek").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetPlatformStatistics_WithoutSuperAdmin_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/platform");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizationStatistics_AsStaff_ReturnsCaseStatistics()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);

        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add("X-Organization-Id", organizationId.ToString());

        var response = await client.GetAsync(
            $"/api/dashboard/organization?organizationId={organizationId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.True(json.RootElement.GetProperty("totalCases").GetInt32() >= 0);
    }

    [Fact]
    public async Task GetOrganizationStatistics_CannotSelectAnotherOrganization()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        var otherOrganizationId =
            await factory.SeedOrganizationWithoutMembershipAsync();

        client.DefaultRequestHeaders.Remove("X-Organization-Id");
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.GetAsync(
            $"/api/dashboard/organization?organizationId={otherOrganizationId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStaffPhone_WithInvalidPhone_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/staff",
            new
            {
                fullName = "Invalid Phone Owner",
                email = $"invalid-{Guid.NewGuid():N}@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var staffId = createdJson.RootElement.GetProperty("id").GetGuid();

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/staff/{staffId}/phone",
            new { phoneNumber = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }
}
