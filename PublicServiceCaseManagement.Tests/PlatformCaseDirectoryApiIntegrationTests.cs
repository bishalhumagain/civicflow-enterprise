using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Tests;

public sealed class PlatformCaseDirectoryApiIntegrationTests
{
    [Fact]
    public async Task GetPlatformCases_AsSuperAdmin_ReturnsCasesAcrossOrganizations()
    {
        await using var factory = CreateFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var firstOrganizationId = await factory.SeedOrganizationWithoutMembershipAsync();
        var secondOrganizationId = await factory.SeedOrganizationWithoutMembershipAsync();
        var citizenId = await factory.SeedCitizenAsync("Platform Case Citizen");

        var openCaseId = await SeedCaseAsync(
            factory,
            firstOrganizationId,
            "Streetlight outage on Cedar Avenue",
            CaseStatus.InProgress,
            CasePriority.High,
            citizenId);
        var resolvedCaseId = await SeedCaseAsync(
            factory,
            secondOrganizationId,
            "Waste collection missed",
            CaseStatus.Resolved,
            CasePriority.Low);

        var response = await client.GetAsync("/api/platform/cases?pageSize=50");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());

        var openCase = Assert.Single(
            items,
            item => item.GetProperty("id").GetGuid() == openCaseId);
        Assert.Equal(
            firstOrganizationId,
            openCase.GetProperty("organizationId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(
            openCase.GetProperty("organizationName").GetString()));
        Assert.Equal("High", openCase.GetProperty("priority").GetString());
        Assert.Equal(
            "Platform Case Citizen",
            openCase.GetProperty("submittedByName").GetString());

        var resolvedCase = Assert.Single(
            items,
            item => item.GetProperty("id").GetGuid() == resolvedCaseId);
        Assert.Equal(
            secondOrganizationId,
            resolvedCase.GetProperty("organizationId").GetGuid());
    }

    [Fact]
    public async Task GetPlatformCases_AppliesOrganizationStatusPriorityAndSearchFilters()
    {
        await using var factory = CreateFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var firstOrganizationId = await factory.SeedOrganizationWithoutMembershipAsync();
        var secondOrganizationId = await factory.SeedOrganizationWithoutMembershipAsync();

        var targetCaseId = await SeedCaseAsync(
            factory,
            firstOrganizationId,
            "Streetlight outage on Cedar Avenue",
            CaseStatus.InProgress,
            CasePriority.High);
        await SeedCaseAsync(
            factory,
            firstOrganizationId,
            "Noise complaint at the market",
            CaseStatus.Resolved,
            CasePriority.Normal);
        await SeedCaseAsync(
            factory,
            secondOrganizationId,
            "Streetlight request in another organization",
            CaseStatus.InProgress,
            CasePriority.High);

        var byOrganization = await GetItemsAsync(
            client,
            $"/api/platform/cases?organizationId={firstOrganizationId}&pageSize=50");
        Assert.Equal(2, byOrganization.Count);

        var byStatus = await GetItemsAsync(
            client,
            "/api/platform/cases?status=Resolved&pageSize=50");
        Assert.Single(byStatus);

        var byPriority = await GetItemsAsync(
            client,
            "/api/platform/cases?priority=High&pageSize=50");
        Assert.Equal(2, byPriority.Count);

        var bySearch = await GetItemsAsync(
            client,
            "/api/platform/cases?search=streetlight&pageSize=50");
        Assert.Equal(2, bySearch.Count);

        var combined = await GetItemsAsync(
            client,
            $"/api/platform/cases?organizationId={firstOrganizationId}" +
            "&status=InProgress&priority=High&search=cedar&pageSize=50");

        var matched = Assert.Single(combined);
        Assert.Equal(targetCaseId, matched.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task GetPlatformCaseDetail_AsSuperAdmin_ReturnsFullCrossTenantDetail()
    {
        await using var factory = CreateFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var organizationId = await factory.SeedOrganizationWithoutMembershipAsync();
        var citizenId = await factory.SeedCitizenAsync("Detail Test Citizen");
        var caseId = await SeedCaseAsync(
            factory,
            organizationId,
            "Detailed platform case",
            CaseStatus.InProgress,
            CasePriority.High,
            citizenId);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var caseEntity = await dbContext.Cases.SingleAsync(c => c.Id == caseId);
            var staffMember = StaffMember.Create(
                organizationId,
                "Assigned Case Worker",
                $"worker-{Guid.NewGuid():N}@example.test",
                null,
                DateTimeOffset.UtcNow);

            dbContext.StaffMembers.Add(staffMember);
            caseEntity.AssignTo(
                staffMember.Id,
                CaseAssignmentRole.CaseWorker,
                DateTimeOffset.UtcNow);
            dbContext.CaseStatusHistories.Add(CaseStatusHistory.Create(
                caseId,
                organizationId,
                CaseStatus.Submitted,
                CaseStatus.InProgress,
                DateTimeOffset.UtcNow.AddMinutes(1),
                citizenId,
                "Internal triage note.",
                isVisibleToCitizen: false));
            dbContext.CaseUpdates.Add(CaseUpdate.Create(
                caseId,
                organizationId,
                CaseUpdateKind.InternalNote,
                "A platform administrator can inspect internal notes.",
                citizenId,
                DateTimeOffset.UtcNow.AddMinutes(2)));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/platform/cases/{caseId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            caseId,
            json.RootElement.GetProperty("case").GetProperty("id").GetGuid());
        Assert.Equal(
            "Detail Test Citizen",
            json.RootElement.GetProperty("submittedBy")
                .GetProperty("fullName").GetString());
        Assert.Equal(
            "Assigned Case Worker",
            json.RootElement.GetProperty("assignedTo")
                .GetProperty("fullName").GetString());
        Assert.Single(json.RootElement.GetProperty("statusHistory").EnumerateArray());
        Assert.Single(json.RootElement.GetProperty("updates").EnumerateArray());
    }

    [Fact]
    public async Task GetPlatformCases_WithoutSuperAdmin_ReturnsForbidden()
    {
        await using var factory = CreateFactory(OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/cases");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetPlatformCaseDetail_UnknownCase_ReturnsNotFound()
    {
        await using var factory = CreateFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/platform/cases/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPlatformOrganizationBreakdown_AsSuperAdmin_CountsCasesPerOrganization()
    {
        await using var factory = CreateFactory(
            OrganizationRole.OrganizationAdmin,
            PlatformRole.SuperAdmin);
        using var client = factory.CreateClient();

        var organizationId = await factory.SeedOrganizationWithoutMembershipAsync();
        await SeedCaseAsync(
            factory,
            organizationId,
            "Open breakdown case",
            CaseStatus.InProgress,
            CasePriority.High);
        await SeedCaseAsync(
            factory,
            organizationId,
            "Resolved breakdown case",
            CaseStatus.Resolved,
            CasePriority.Normal);

        var response = await client.GetAsync(
            "/api/dashboard/platform/organizations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var organization = Assert.Single(
            json.RootElement.EnumerateArray(),
            item => item.GetProperty("organizationId").GetGuid() == organizationId);

        Assert.Equal(2, organization.GetProperty("totalCases").GetInt32());
        Assert.Equal(1, organization.GetProperty("openCases").GetInt32());
        Assert.Equal(1, organization.GetProperty("resolvedCases").GetInt32());
        Assert.Equal(1, organization.GetProperty("highPriorityCases").GetInt32());
    }

    private static CivicFlowApiFactory CreateFactory(
        OrganizationRole organizationRole,
        PlatformRole platformRole = PlatformRole.None) =>
        new(
            organizationRole,
            platformRole,
            enableBusinessEndpoints: true);

    private static async Task<Guid> SeedCaseAsync(
        CivicFlowApiFactory factory,
        Guid organizationId,
        string title,
        CaseStatus status,
        CasePriority priority,
        Guid? submittedByUserId = null)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var now = DateTimeOffset.UtcNow;
        var caseEntity = Case.Create(
            organizationId,
            $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
            title,
            $"A detailed description for {title}.",
            now,
            submittedByUserId);

        if (priority != CasePriority.Normal)
        {
            caseEntity.ChangePriority(priority, now);
        }

        foreach (var nextStatus in StatusPath(status))
        {
            caseEntity.ChangeStatus(nextStatus, now);
        }

        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        return caseEntity.Id;
    }

    private static IEnumerable<CaseStatus> StatusPath(CaseStatus targetStatus)
    {
        var path = new List<CaseStatus>();

        if (targetStatus == CaseStatus.Submitted)
        {
            return path;
        }

        path.Add(CaseStatus.UnderReview);

        if (targetStatus == CaseStatus.UnderReview)
        {
            return path;
        }

        path.Add(CaseStatus.Assigned);

        if (targetStatus == CaseStatus.Assigned)
        {
            return path;
        }

        path.Add(CaseStatus.InProgress);

        if (targetStatus == CaseStatus.InProgress)
        {
            return path;
        }

        if (targetStatus == CaseStatus.Resolved)
        {
            path.Add(CaseStatus.Resolved);
        }

        return path;
    }

    private static async Task<List<JsonElement>> GetItemsAsync(
        HttpClient client,
        string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToList();
    }
}
