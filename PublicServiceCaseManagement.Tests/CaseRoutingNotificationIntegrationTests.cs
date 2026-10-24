using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Notifications;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Verifies that a citizen submission is triaged to an organization
/// administrator: assigned (and notified) when an admin staff member exists,
/// otherwise the active admins are notified and the case stays unassigned.
/// </summary>
public sealed class CaseRoutingNotificationIntegrationTests
{
    [Fact]
    public async Task CitizenSubmission_AssignsCaseToAdminStaffAndNotifiesAdmin()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: false);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.CaseWorker);

        Guid adminUserId;
        Guid adminStaffId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();
            var now = DateTimeOffset.UtcNow;

            var admin = ApplicationUser.Create(
                "Triage Admin",
                $"admin-{Guid.NewGuid():N}@example.com",
                now);
            var staff = StaffMember.Create(
                organizationId,
                admin.FullName,
                admin.Email!,
                null,
                now);

            dbContext.ApplicationUsers.Add(admin);
            dbContext.StaffMembers.Add(staff);
            dbContext.OrganizationMemberships.Add(
                OrganizationMembership.Create(
                    admin.Id,
                    organizationId,
                    OrganizationRole.OrganizationAdmin,
                    now));
            await dbContext.SaveChangesAsync();

            adminUserId = admin.Id;
            adminStaffId = staff.Id;
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);

        using var content = new MultipartFormDataContent
        {
            { new StringContent(organizationId.ToString()), "organizationId" },
            { new StringContent("Broken streetlight"), "title" },
            {
                new StringContent("The streetlight near the market has been out for days."),
                "description"
            }
        };

        var response = await client.PostAsync("/api/citizens/cases", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = await response.Content
            .ReadFromJsonAsync<CitizenCaseResponse>();
        Assert.NotNull(submitted);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider
                .GetRequiredService<CivicFlowDbContext>();

            var caseEntity = await dbContext.Cases
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == submitted!.Id);
            Assert.Equal(adminStaffId, caseEntity.AssignedToStaffMemberId);
            Assert.Equal(CaseAssignmentRole.CaseWorker, caseEntity.AssignedRole);

            var notification = await dbContext.Notifications
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate =>
                    candidate.RecipientUserId == adminUserId &&
                    candidate.Type == NotificationTypes.CaseAssigned);
            Assert.NotNull(notification);
        }
    }

    [Fact]
    public async Task CitizenSubmission_WhenAdminHasNoStaffRow_NotifiesAdminAndLeavesUnassigned()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: false);
        // The OrganizationAdmin seed path does not create a staff row, so the
        // case cannot be formally owned and the fallback notification applies.
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.OrganizationAdmin);
        var adminUserId = TestAuthenticationHandler.DefaultUserId;

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);

        using var content = new MultipartFormDataContent
        {
            { new StringContent(organizationId.ToString()), "organizationId" },
            { new StringContent("Pothole on main road"), "title" },
            {
                new StringContent("A large pothole has formed on the main road near the school."),
                "description"
            }
        };

        var response = await client.PostAsync("/api/citizens/cases", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = await response.Content
            .ReadFromJsonAsync<CitizenCaseResponse>();
        Assert.NotNull(submitted);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var caseEntity = await dbContext.Cases
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == submitted!.Id);
        Assert.Null(caseEntity.AssignedToStaffMemberId);

        var notification = await dbContext.Notifications
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate =>
                candidate.RecipientUserId == adminUserId &&
                candidate.IdempotencyKey ==
                    $"case-triage:{submitted.Id}:user:{adminUserId}");
        Assert.NotNull(notification);
    }

    [Fact]
    public async Task CitizenSubmission_WhenNoAdministrator_StillCreatesCaseWithoutAssignment()
    {
        await using var factory = new CivicFlowApiFactory(
            useAuthenticatedOrganizationContext: true,
            enableBusinessEndpoints: false);
        var organizationId = await factory.SeedAuthenticatedOrganizationAsync(
            OrganizationRole.Viewer);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.CitizenRole);

        using var content = new MultipartFormDataContent
        {
            { new StringContent(organizationId.ToString()), "organizationId" },
            { new StringContent("Overgrown park grass"), "title" },
            {
                new StringContent("The grass in the public park has grown tall and untended."),
                "description"
            }
        };

        var response = await client.PostAsync("/api/citizens/cases", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var submitted = await response.Content
            .ReadFromJsonAsync<CitizenCaseResponse>();
        Assert.NotNull(submitted);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CivicFlowDbContext>();

        var caseEntity = await dbContext.Cases
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == submitted!.Id);
        Assert.Null(caseEntity.AssignedToStaffMemberId);

        Assert.DoesNotContain(
            await dbContext.Notifications
                .AsNoTracking()
                .Where(candidate =>
                    candidate.IdempotencyKey.StartsWith($"case-triage:{submitted.Id}"))
                .Select(candidate => candidate.IdempotencyKey)
                .ToListAsync(),
            key => true);
    }

    private sealed record CitizenCaseResponse(
        Guid Id,
        string ReferenceNumber,
        string Title,
        string Description,
        string Status,
        DateTimeOffset CreatedAt);
}
