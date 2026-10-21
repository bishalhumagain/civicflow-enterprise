using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public class CasesApiIntegrationTests
{
    [Fact]
    public async Task GetStatusHistory_WhenCaseDoesNotExist_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var missingCaseId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/api/cases/{missingCaseId}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }


    [Fact]
    public async Task GetStatusHistory_WhenClientIsCitizen_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);

        var response = await client.GetAsync(
            $"/api/cases/{Guid.NewGuid()}/history");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCase_ChangeStatus_AndGetHistory_ReturnsRecordedTransition()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Integration test case",
                description = "Verify the complete case lifecycle."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var caseId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        var statusResponse = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/status",
            new { newStatus = "UnderReview" });

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        var historyResponse = await client.GetAsync(
            $"/api/cases/{caseId}/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var historyJson = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        var historyEntries = historyJson.RootElement;

        Assert.Equal(JsonValueKind.Array, historyEntries.ValueKind);

        var historyEntry = Assert.Single(
            historyEntries.EnumerateArray());

        Assert.Equal(
            "Submitted",
            historyEntry.GetProperty("previousStatus").GetString());

        Assert.Equal(
            "UnderReview",
            historyEntry.GetProperty("newStatus").GetString());

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        var applicationUserId = await dbContext.ApplicationUsers
            .Where(user => user.Id == TestAuthenticationHandler.DefaultUserId)
            .Select(user => user.Id)
            .SingleAsync();

        Assert.Equal(
            applicationUserId,
            historyEntry.GetProperty("changedByUserId").GetGuid());
    }

    [Theory]
    [InlineData("", "A sufficiently long description for testing.")]
    [InlineData("AB", "A sufficiently long description for testing.")]
    [InlineData("A valid title", "Too short")]
    public async Task CreateCase_WithInvalidInput_ReturnsBadRequest(
        string title,
        string description)
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new { title, description });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateCase_WithTitleLongerThan200Characters_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = new string('A', 201),
                description = "A sufficiently long description for testing."
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_WhenTransitionIsInvalid_ReturnsBadRequestAndDoesNotCreateHistory()
    {
        await using var factory = new CivicFlowApiFactory(
            PublicServiceCaseManagement.Api.Domain.Organizations.OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Invalid transition test",
                description = "Verify invalid status transitions."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());

        var caseId = createdJson.RootElement
            .GetProperty("id")
            .GetGuid();

        var changeResponse = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/status",
            new { newStatus = "Closed" });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            changeResponse.StatusCode);

        var getCaseResponse = await client.GetAsync(
            $"/api/cases/{caseId}");

        Assert.Equal(HttpStatusCode.OK, getCaseResponse.StatusCode);

        using var caseJson = JsonDocument.Parse(
            await getCaseResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Submitted",
            caseJson.RootElement.GetProperty("status").GetString());

        var historyResponse = await client.GetAsync(
            $"/api/cases/{caseId}/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        using var historyJson = JsonDocument.Parse(
            await historyResponse.Content.ReadAsStringAsync());

        Assert.Empty(historyJson.RootElement.EnumerateArray());
    }


    [Fact]
    public async Task GetCase_WhenOrganizationHeaderDoesNotMatchMembership_ReturnsForbid()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        await factory.SeedAuthenticatedOrganizationAsync();

        var otherOrganizationId =
            await factory.SeedOrganizationWithoutMembershipAsync();

        var caseId = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var caseEntity = Case.Create(
                otherOrganizationId,
                $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
                "Cross-organization access test",
                "This case must not be accessible without organization membership.",
                DateTimeOffset.UtcNow);

            dbContext.Cases.Add(caseEntity);
            await dbContext.SaveChangesAsync();

            caseId = caseEntity.Id;
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            otherOrganizationId.ToString());

        var response = await client.GetAsync($"/api/cases/{caseId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetCase_WhenUserBelongsToBothOrganizationsButCaseBelongsToAnother_ReturnsNotFound()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var firstOrganizationId =
            await factory.SeedAuthenticatedOrganizationAsync();

        var secondOrganizationId =
            await factory.SeedAdditionalOrganizationForAuthenticatedUserAsync();

        Guid caseId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var caseEntity = Case.Create(
                firstOrganizationId,
                $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
                "Tenant isolation test",
                "This case belongs to the first organization.",
                DateTimeOffset.UtcNow);

            dbContext.Cases.Add(caseEntity);
            await dbContext.SaveChangesAsync();

            caseId = caseEntity.Id;
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            secondOrganizationId.ToString());

        var response = await client.GetAsync($"/api/cases/{caseId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateCase_WhenUserIsViewer_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId =
            await factory.SeedAuthenticatedOrganizationAsync(
                role: OrganizationRole.Viewer);

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Viewer permission test",
                description = "A viewer must not create cases."
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeCaseStatus_WhenUserIsViewer_ReturnsForbidden()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true);

        var organizationId =
            await factory.SeedAuthenticatedOrganizationAsync(
                role: OrganizationRole.Viewer);

        Guid caseId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var caseEntity = Case.Create(
                organizationId,
                $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
                "Viewer status test",
                "A viewer must not change this case's status.",
                DateTimeOffset.UtcNow);

            dbContext.Cases.Add(caseEntity);
            await dbContext.SaveChangesAsync();

            caseId = caseEntity.Id;
        }

        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var response = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/status",
            new { newStatus = "UnderReview" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

}
