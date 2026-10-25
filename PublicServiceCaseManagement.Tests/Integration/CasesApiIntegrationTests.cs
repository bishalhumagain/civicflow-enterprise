using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

using PublicServiceCaseManagement.Tests.Support;

namespace PublicServiceCaseManagement.Tests.Integration;

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
        var createdCase = await dbContext.Cases
            .SingleAsync(caseEntity => caseEntity.Id == caseId);
        var staffMember = await dbContext.StaffMembers
            .SingleAsync(candidate =>
                candidate.OrganizationId == createdCase.OrganizationId);
        var applicationUserId = await dbContext.ApplicationUsers
            .Where(user => user.Id == TestAuthenticationHandler.DefaultUserId)
            .Select(user => user.Id)
            .SingleAsync();

        Assert.Equal(staffMember.Id, createdCase.AssignedToStaffMemberId);
        Assert.Equal(CaseAssignmentRole.CaseWorker, createdCase.AssignedRole);
        Assert.Equal(
            applicationUserId,
            historyEntry.GetProperty("changedByUserId").GetGuid());
    }

    [Fact]
    public async Task
        CreateCase_WhenCaseWorkerHasNoActiveStaffMember_ReturnsForbiddenWithoutCreatingCase()
    {
        await using var factory = new CivicFlowApiFactory(OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var staffMember = await dbContext.StaffMembers.SingleAsync();
            staffMember.Deactivate(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Case worker without active staff member",
                description = "Case creation must be rejected without an active staff profile."
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        Assert.Empty(await verifyDbContext.Cases.ToListAsync());
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
    public async Task ChangePriority_UpdatesCaseAndReturnsNewPriority()
    {
        await using var factory = new CivicFlowApiFactory(OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Priority change test",
                description = "Verify that case priority can be updated."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = createdJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(
            "Normal",
            createdJson.RootElement.GetProperty("priority").GetString());

        var changeResponse = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/priority",
            new { newPriority = "Critical" });

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);

        using var changedJson = JsonDocument.Parse(
            await changeResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "Critical",
            changedJson.RootElement.GetProperty("priority").GetString());

        var getCaseResponse = await client.GetAsync($"/api/cases/{caseId}");
        Assert.Equal(HttpStatusCode.OK, getCaseResponse.StatusCode);

        using var caseJson = JsonDocument.Parse(
            await getCaseResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "Critical",
            caseJson.RootElement.GetProperty("priority").GetString());
    }

    [Fact]
    public async Task ChangePriority_WithInvalidPriority_ReturnsBadRequest()
    {
        await using var factory = new CivicFlowApiFactory(OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Invalid priority test",
                description = "Verify that unknown priorities are rejected."
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = createdJson.RootElement.GetProperty("id").GetGuid();

        var changeResponse = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/priority",
            new { newPriority = "Urgent" });

        Assert.Equal(HttpStatusCode.BadRequest, changeResponse.StatusCode);
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

    [Fact]
    public async Task CaseWorker_CanOnlyReadCasesAssignedToThem()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        Guid unassignedCaseId;

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var unassignedCase = Case.Create(
                organizationId,
                $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
                "Unassigned case",
                "This case belongs to the organization but has no assignee.",
                DateTimeOffset.UtcNow);
            dbContext.Cases.Add(unassignedCase);
            await dbContext.SaveChangesAsync();
            unassignedCaseId = unassignedCase.Id;
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var detailResponse = await client.GetAsync(
            $"/api/cases/{unassignedCaseId}");
        var listResponse = await client.GetAsync("/api/cases");

        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listJson = JsonDocument.Parse(
            await listResponse.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            listJson.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == unassignedCaseId);
    }

    [Fact]
    public async Task ViewerAssignment_AllowsReadButDeniesCaseUpdates()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Viewer assignment case",
                description = "The assigned viewer must not change this case."
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = createdJson.RootElement.GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var caseEntity = await dbContext.Cases.SingleAsync(
                candidate => candidate.Id == caseId);
            caseEntity.AssignTo(
                caseEntity.AssignedToStaffMemberId!.Value,
                CaseAssignmentRole.Viewer,
                DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync();
        }

        var detailResponse = await client.GetAsync($"/api/cases/{caseId}");
        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/cases/{caseId}/status",
            new { newStatus = "UnderReview" });
        var detailsUpdateResponse = await client.PutAsJsonAsync(
            $"/api/cases/{caseId}",
            new
            {
                title = "Viewer must not edit",
                description = "This description must not be accepted."
            });

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.True(
            detailsUpdateResponse.StatusCode == HttpStatusCode.Forbidden,
            await detailsUpdateResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AssignedCaseWorker_CanUpdateCaseDetails()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Case before edit",
                description = "Initial case description for edit test."
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = createdJson.RootElement.GetProperty("id").GetGuid();

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/cases/{caseId}",
            new
            {
                title = "Updated case title",
                description = "Updated case description for the assigned worker."
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        using var updatedJson = JsonDocument.Parse(
            await updateResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "Updated case title",
            updatedJson.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task OnlyOrganizationAdminCanDeleteCaseAfterConfirmation()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.OrganizationAdmin,
            enableBusinessEndpoints: true);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Case pending deletion",
                description = "This case is removed only after admin confirmation."
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = createdJson.RootElement.GetProperty("id").GetGuid();

        var unconfirmedResponse = await client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/cases/{caseId}")
            {
                Content = JsonContent.Create(new { confirmed = false })
            });
        Assert.Equal(HttpStatusCode.BadRequest, unconfirmedResponse.StatusCode);

        var confirmedResponse = await client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/cases/{caseId}")
            {
                Content = JsonContent.Create(new { confirmed = true })
            });
        Assert.Equal(HttpStatusCode.NoContent, confirmedResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        Assert.False(await dbContext.Cases.AnyAsync(item => item.Id == caseId));
        Assert.Contains(
            await dbContext.AuditEvents
                .Where(item => item.EntityId == caseId.ToString())
                .Select(item => item.Action)
                .ToListAsync(),
            action => action == "case.deleted");
    }

    [Fact]
    public async Task CaseWorker_CannotDeleteCaseEvenWithConfirmation()
    {
        await using var factory = new CivicFlowApiFactory(
            OrganizationRole.CaseWorker,
            enableBusinessEndpoints: true);
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync(
            "/api/cases",
            new
            {
                title = "Case worker cannot delete",
                description = "Only an organization administrator can delete this case."
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createdJson = JsonDocument.Parse(
            await createResponse.Content.ReadAsStringAsync());
        var caseId = createdJson.RootElement.GetProperty("id").GetGuid();

        var response = await client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/cases/{caseId}")
            {
                Content = JsonContent.Create(new { confirmed = true })
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrganizationAdmin_CannotDeleteCaseFromAnotherOrganization()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: true);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        var otherOrganizationId =
            await factory.SeedOrganizationWithoutMembershipAsync();
        var otherCase = Case.Create(
            otherOrganizationId,
            $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
            "Other organization's case",
            "This case belongs to a different organization.",
            DateTimeOffset.UtcNow);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            dbContext.Cases.Add(otherCase);
            await dbContext.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Organization-Id",
            organizationId.ToString());
        var response = await client.SendAsync(
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"/api/cases/{otherCase.Id}")
            {
                Content = JsonContent.Create(new { confirmed = true })
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();
        Assert.True(await verifyDb.Cases.AnyAsync(
            item => item.Id == otherCase.Id &&
                    item.OrganizationId == otherOrganizationId));
    }

}
