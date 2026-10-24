
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public sealed class OrganizationApiIntegrationTests
{
    [Fact]
    public async Task CreateOrganization_WithValidInput_ReturnsCreated()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new
            {
                name = "Kathmandu Metropolitan City",
                code = "KATHMANDU-METRO"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Kathmandu Metropolitan City",
            json.RootElement.GetProperty("name").GetString());

        Assert.Equal(
            "KATHMANDU-METRO",
            json.RootElement.GetProperty("code").GetString());

        Assert.True(
            json.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task CreateOrganization_WithDuplicateCode_ReturnsConflict()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Organization One", code = "ORG-ONE" });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Organization Two", code = "org-one" });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Theory]
    [InlineData("", "VALID-CODE")]
    [InlineData("Valid Name", "")]
    public async Task CreateOrganization_WithInvalidInput_ReturnsBadRequest(
        string name,
        string code)
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name, code });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrganizations_ReturnsSeededAndCreatedOrganizations()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Pokhara Metropolitan City", code = "POKHARA" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Contains(
            json.RootElement.EnumerateArray(),
            organization =>
                organization.GetProperty("code").GetString() == "POKHARA");
    }

    [Fact]
    public async Task GetOrganizations_AsOrganizationAdmin_ReturnsOnlyActiveManagedOrganizations()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        _ = await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.CaseWorker);
        var inactiveManagedOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync(
                PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<
                    PublicServiceCaseManagement.Api.Infrastructure.Persistence.CivicFlowDbContext>();
            var membership = await dbContext.OrganizationMemberships.SingleAsync(
                candidate =>
                    candidate.OrganizationId == inactiveManagedOrganizationId &&
                    candidate.UserId == TestAuthenticationHandler.DefaultUserId);
            membership.Deactivate(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        var organization = Assert.Single(
            json.RootElement.EnumerateArray());
        Assert.Equal(
            "CIVICFLOW-DEV",
            organization.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetOrganizations_AsCaseWorker_ReturnsEmpty()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task GetOrganization_WhenOrganizationExists_ReturnsOrganization()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Organization One", code = "ORG-ONE" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();

        var response = await client.GetAsync($"/api/organizations/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "ORG-ONE",
            json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetOrganization_WhenOrganizationDoesNotExist_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/organizations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateOrganization_ChangesOrganizationDetails()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Old Name", code = "OLD-CODE" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/organizations/{id}",
            new { name = "New Name", code = "NEW-CODE" });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var updatedJson = JsonDocument.Parse(
            await updateResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "New Name",
            updatedJson.RootElement.GetProperty("name").GetString());

        Assert.Equal(
            "NEW-CODE",
            updatedJson.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task UpdateOrganization_WithDuplicateCode_ReturnsConflict()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Organization One", code = "ORG-ONE" });

        var secondResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Organization Two", code = "ORG-TWO" });

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        using var firstJson = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync());

        var id = firstJson.RootElement.GetProperty("id").GetGuid();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/organizations/{id}",
            new { name = "Organization One Updated", code = "ORG-TWO" });

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeactivateAndReactivateOrganization_UpdatesStatus()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/organizations",
            new { name = "Organization One", code = "ORG-ONE" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var id = createdJson.RootElement.GetProperty("id").GetGuid();

        var deactivateResponse = await client.PatchAsync(
            $"/api/organizations/{id}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        using var deactivatedJson = JsonDocument.Parse(
            await deactivateResponse.Content.ReadAsStringAsync());

        Assert.False(
            deactivatedJson.RootElement.GetProperty("isActive").GetBoolean());

        var reactivateResponse = await client.PatchAsync(
            $"/api/organizations/{id}/reactivate",
            null);

        Assert.Equal(HttpStatusCode.OK, reactivateResponse.StatusCode);

        using var reactivatedJson = JsonDocument.Parse(
            await reactivateResponse.Content.ReadAsStringAsync());

        Assert.True(
            reactivatedJson.RootElement.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DeactivateOrganization_WhenOrganizationDoesNotExist_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.OrganizationAdmin,
            PublicServiceCaseManagement.Api.Domain.Users.PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var response = await client.PatchAsync(
            $"/api/organizations/{Guid.NewGuid()}/deactivate",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
