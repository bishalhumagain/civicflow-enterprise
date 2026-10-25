
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Notifications;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Features.Notifications;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Tests.Infrastructure;
using PublicServiceCaseManagement.Api.Features.Cases.Services;
using PublicServiceCaseManagement.Api.Features.Notifications.Services;

namespace PublicServiceCaseManagement.Tests.Features.Cases;

public sealed class CaseServiceTests
{
    private static readonly Guid OrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid OtherOrganizationId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly DateTimeOffset Now =
        new(2026, 10, 17, 8, 0, 0, TimeSpan.Zero);

    private static CivicFlowDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CivicFlowDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CivicFlowDbContext(options);
    }

    private static Case CreateCase(Guid? organizationId = null) =>
        Case.Create(
            organizationId ?? OrganizationId,
            $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
            "Damaged streetlight",
            "A streetlight near the public park is damaged.",
            Now);

    private static StaffMember CreateStaffMember(
        Guid? organizationId = null,
        string? email = null) =>
        StaffMember.Create(
            organizationId ?? OrganizationId,
            "Test Officer",
            email ?? $"officer-{Guid.NewGuid():N}@example.com",
            null,
            Now);

    private static CaseService CreateService(CivicFlowDbContext dbContext) =>
        new(dbContext, new RecordingAuditLogger(), new EmptyNotificationOutbox());

    private sealed class EmptyNotificationOutbox : INotificationOutbox
    {
        public Task<Guid?> EnqueueAsync(
            Guid recipientUserId,
            string type,
            string title,
            string message,
            string? entityType,
            Guid? entityId,
            string idempotencyKey,
            Guid? organizationId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(null);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenStatusChanges_SavesCaseAndHistory()
    {
        await using var dbContext = CreateDbContext();
        var actorUserId = Guid.NewGuid();

        var caseEntity = CreateCase();

        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ChangeStatusAsync(
            caseEntity.Id,
            OrganizationId,
            CaseStatus.UnderReview,
            actorUserId);

        Assert.NotNull(result);
        Assert.Equal(CaseStatus.UnderReview, result.Status);

        var history = await dbContext.CaseStatusHistories
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(caseEntity.Id, history.CaseId);
        Assert.Equal(OrganizationId, history.OrganizationId);
        Assert.Equal(CaseStatus.Submitted, history.PreviousStatus);
        Assert.Equal(CaseStatus.UnderReview, history.NewStatus);
        Assert.Equal(actorUserId, history.ChangedByUserId);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenCaseDoesNotExist_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var service = CreateService(dbContext);

        var result = await service.ChangeStatusAsync(
            Guid.NewGuid(),
            OrganizationId,
            CaseStatus.UnderReview);

        Assert.Null(result);
        Assert.Empty(await dbContext.CaseStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenTransitionIsInvalid_DoesNotSaveHistory()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();

        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ChangeStatusAsync(
                caseEntity.Id,
                OrganizationId,
                CaseStatus.Resolved));

        Assert.Equal(CaseStatus.Submitted, caseEntity.Status);
        Assert.Empty(await dbContext.CaseStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenStatusIsUnchanged_DoesNotCreateHistory()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        caseEntity.ChangeStatus(CaseStatus.UnderReview, Now);

        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ChangeStatusAsync(
            caseEntity.Id,
            OrganizationId,
            CaseStatus.UnderReview);

        Assert.NotNull(result);
        Assert.Equal(CaseStatus.UnderReview, result.Status);
        Assert.Empty(await dbContext.CaseStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task ChangePriorityAsync_WhenPriorityChanges_UpdatesPriority()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ChangePriorityAsync(
            caseEntity.Id,
            OrganizationId,
            CasePriority.Critical);

        Assert.NotNull(result);
        Assert.Equal(CasePriority.Critical, result.Priority);
    }

    [Fact]
    public async Task ChangePriorityAsync_WhenCaseIsOutsideOrganization_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ChangePriorityAsync(
            caseEntity.Id,
            OtherOrganizationId,
            CasePriority.High);

        Assert.Null(result);
        Assert.Equal(CasePriority.Normal, caseEntity.Priority);
    }

    [Fact]
    public async Task ChangePriorityAsync_WhenCaseWorkerIsNotAssigned_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ChangePriorityAsync(
            caseEntity.Id,
            OrganizationId,
            CasePriority.High,
            authorizedStaffMemberId: Guid.NewGuid());

        Assert.Null(result);
        Assert.Equal(CasePriority.Normal, caseEntity.Priority);
    }

    [Fact]
    public async Task ChangePriorityAsync_WhenPriorityIsUnchanged_ReturnsCase()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.ChangePriorityAsync(
            caseEntity.Id,
            OrganizationId,
            CasePriority.Normal);

        Assert.NotNull(result);
        Assert.Equal(CasePriority.Normal, result.Priority);
    }

    [Fact]
    public async Task AssignAsync_WhenStaffMemberIsAssigned_SavesAssignmentAndHistory()
    {
        await using var dbContext = CreateDbContext();
        var actorUserId = Guid.NewGuid();

        var caseEntity = CreateCase();
        var staffMember = CreateStaffMember();

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            staffMember.Id,
            CancellationToken.None,
            actorUserId);

        Assert.NotNull(result);
        Assert.Equal(staffMember.Id, result.AssignedToStaffMemberId);
        Assert.Equal(CaseAssignmentRole.CaseWorker, result.AssignedRole);

        var history = await dbContext.CaseAssignmentHistories
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(caseEntity.Id, history.CaseId);
        Assert.Equal(OrganizationId, history.OrganizationId);
        Assert.Null(history.PreviousStaffMemberId);
        Assert.Equal(staffMember.Id, history.NewStaffMemberId);
        Assert.Equal(CaseAssignmentRole.CaseWorker, history.NewRole);
        Assert.Equal(actorUserId, history.AssignedByUserId);
    }

    [Fact]
    public async Task AssignAsync_WhenReassigned_CreatesHistoryWithPreviousAndNewAssignees()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        var firstStaffMember = CreateStaffMember();
        var secondStaffMember = CreateStaffMember();

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.AddRange(firstStaffMember, secondStaffMember);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            firstStaffMember.Id,
            CancellationToken.None);

        var result = await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            secondStaffMember.Id,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(secondStaffMember.Id, result.AssignedToStaffMemberId);

        var history = await dbContext.CaseAssignmentHistories
            .AsNoTracking()
            .OrderBy(h => h.AssignedAt)
            .ThenBy(h => h.Id)
            .ToListAsync();

        Assert.Equal(2, history.Count);

        Assert.Null(history[0].PreviousStaffMemberId);
        Assert.Equal(firstStaffMember.Id, history[0].NewStaffMemberId);

        Assert.Equal(firstStaffMember.Id, history[1].PreviousStaffMemberId);
        Assert.Equal(secondStaffMember.Id, history[1].NewStaffMemberId);
    }

    [Fact]
    public async Task AssignAsync_WhenSameStaffMemberIsAssignedAgain_DoesNotDuplicateHistory()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        var staffMember = CreateStaffMember();

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            staffMember.Id,
            CancellationToken.None);

        await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            staffMember.Id,
            CancellationToken.None);

        var history = await dbContext.CaseAssignmentHistories
            .AsNoTracking()
            .ToListAsync();

        Assert.Single(history);
        Assert.Equal(staffMember.Id, caseEntity.AssignedToStaffMemberId);
    }

    [Fact]
    public async Task AssignAsync_WhenCaseRoleChangesForSameStaff_CreatesHistory()
    {
        await using var dbContext = CreateDbContext();
        var caseEntity = CreateCase();
        var staffMember = CreateStaffMember();
        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            staffMember.Id,
            CancellationToken.None);

        var updated = await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            staffMember.Id,
            CancellationToken.None,
            role: CaseAssignmentRole.Viewer);

        Assert.NotNull(updated);
        Assert.Equal(CaseAssignmentRole.Viewer, updated.AssignedRole);

        var history = await dbContext.CaseAssignmentHistories
            .AsNoTracking()
            .OrderBy(item => item.AssignedAt)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(CaseAssignmentRole.CaseWorker, history[1].PreviousRole);
        Assert.Equal(CaseAssignmentRole.Viewer, history[1].NewRole);
    }

    [Fact]
    public async Task AssignAsync_WhenStaffMemberIsInactive_ThrowsAndDoesNotCreateHistory()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        var staffMember = CreateStaffMember();
        staffMember.Deactivate(Now);

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignAsync(
                caseEntity.Id,
                OrganizationId,
                staffMember.Id,
                CancellationToken.None));

        Assert.Null(caseEntity.AssignedToStaffMemberId);
        Assert.Empty(await dbContext.CaseAssignmentHistories.ToListAsync());
    }

    [Fact]
    public async Task AssignAsync_WhenStaffMemberBelongsToAnotherOrganization_Throws()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        var staffMember = CreateStaffMember(OtherOrganizationId);

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AssignAsync(
                caseEntity.Id,
                OrganizationId,
                staffMember.Id,
                CancellationToken.None));

        Assert.Null(caseEntity.AssignedToStaffMemberId);
        Assert.Empty(await dbContext.CaseAssignmentHistories.ToListAsync());
    }

    [Fact]
    public async Task AssignAsync_WhenCaseBelongsToAnotherOrganization_ReturnsNull()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase(OtherOrganizationId);
        var staffMember = CreateStaffMember(OtherOrganizationId);

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var result = await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            staffMember.Id,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Null(caseEntity.AssignedToStaffMemberId);
        Assert.Empty(await dbContext.CaseAssignmentHistories.ToListAsync());
    }

    private static ApplicationUser CreateApplicationUser(string email) =>
        ApplicationUser.Create("Test Principal", email, Now);

    private static OrganizationMembership CreateAdminMembership(Guid userId) =>
        OrganizationMembership.Create(
            userId,
            OrganizationId,
            OrganizationRole.OrganizationAdmin,
            Now);

    private static CaseService CreateService(
        CivicFlowDbContext dbContext,
        RecordingNotificationOutbox outbox) =>
        new(dbContext, new RecordingAuditLogger(), outbox);

    private sealed record QueuedNotification(
        Guid RecipientUserId,
        string Type,
        string IdempotencyKey);

    private sealed class RecordingNotificationOutbox : INotificationOutbox
    {
        public List<QueuedNotification> Queued { get; } = [];

        public Task<Guid?> EnqueueAsync(
            Guid recipientUserId,
            string type,
            string title,
            string message,
            string? entityType,
            Guid? entityId,
            string idempotencyKey,
            Guid? organizationId,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            Queued.Add(new QueuedNotification(recipientUserId, type, idempotencyKey));
            return Task.FromResult<Guid?>(Guid.NewGuid());
        }
    }

    [Fact]
    public async Task RouteToOrganizationAdministratorAsync_WhenAdminStaffExists_AssignsToAdmin()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        var adminUser = CreateApplicationUser("admin@example.com");
        var adminStaff = CreateStaffMember(email: "admin@example.com");

        dbContext.Cases.Add(caseEntity);
        dbContext.ApplicationUsers.Add(adminUser);
        dbContext.OrganizationMemberships.Add(CreateAdminMembership(adminUser.Id));
        dbContext.StaffMembers.Add(adminStaff);
        await dbContext.SaveChangesAsync();

        var outbox = new RecordingNotificationOutbox();
        var service = CreateService(dbContext, outbox);

        var result = await service.RouteToOrganizationAdministratorAsync(
            caseEntity.Id,
            OrganizationId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(adminStaff.Id, result.AssignedToStaffMemberId);
        Assert.Equal(CaseAssignmentRole.CaseWorker, result.AssignedRole);
        Assert.Contains(
            outbox.Queued,
            notification => notification.RecipientUserId == adminUser.Id &&
                            notification.Type == NotificationTypes.CaseAssigned);
    }

    [Fact]
    public async Task RouteToOrganizationAdministratorAsync_WhenNoAdminStaffRow_NotifiesAdminsAndLeavesUnassigned()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        var adminUser = CreateApplicationUser("admin@example.com");

        dbContext.Cases.Add(caseEntity);
        dbContext.ApplicationUsers.Add(adminUser);
        dbContext.OrganizationMemberships.Add(CreateAdminMembership(adminUser.Id));
        await dbContext.SaveChangesAsync();

        var outbox = new RecordingNotificationOutbox();
        var service = CreateService(dbContext, outbox);

        var result = await service.RouteToOrganizationAdministratorAsync(
            caseEntity.Id,
            OrganizationId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.AssignedToStaffMemberId);
        Assert.Contains(
            outbox.Queued,
            notification => notification.RecipientUserId == adminUser.Id &&
                            notification.Type == NotificationTypes.CaseAssigned &&
                            notification.IdempotencyKey ==
                                $"case-triage:{caseEntity.Id}:user:{adminUser.Id}");
    }

    [Fact]
    public async Task RouteToOrganizationAdministratorAsync_WhenNoAdmins_NoOp()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        var caseWorker = CreateStaffMember(email: "worker@example.com");
        var caseWorkerUser = CreateApplicationUser("worker@example.com");

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(caseWorker);
        dbContext.ApplicationUsers.Add(caseWorkerUser);
        dbContext.OrganizationMemberships.Add(
            OrganizationMembership.Create(
                caseWorkerUser.Id,
                OrganizationId,
                OrganizationRole.CaseWorker,
                Now));
        await dbContext.SaveChangesAsync();

        var outbox = new RecordingNotificationOutbox();
        var service = CreateService(dbContext, outbox);

        var result = await service.RouteToOrganizationAdministratorAsync(
            caseEntity.Id,
            OrganizationId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result.AssignedToStaffMemberId);
        Assert.Empty(outbox.Queued);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenCaseHasAssignedViewer_QueuesStaffNotification()
    {
        await using var dbContext = CreateDbContext();

        var citizenUserId = Guid.NewGuid();
        var caseEntity = Case.Create(
            OrganizationId,
            $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant(),
            "Damaged streetlight",
            "A streetlight near the public park is damaged.",
            Now,
            citizenUserId);

        var viewerStaff = CreateStaffMember(email: "viewer@example.com");

        var viewerUser = CreateApplicationUser("viewer@example.com");
        var viewerMembership = OrganizationMembership.Create(
            viewerUser.Id,
            OrganizationId,
            OrganizationRole.Viewer,
            Now);

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(viewerStaff);
        dbContext.ApplicationUsers.Add(viewerUser);
        dbContext.OrganizationMemberships.Add(viewerMembership);
        await dbContext.SaveChangesAsync();

        var assignService = CreateService(dbContext);
        await assignService.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            viewerStaff.Id,
            CancellationToken.None,
            role: CaseAssignmentRole.Viewer);

        var outbox = new RecordingNotificationOutbox();
        var notifyService = CreateService(dbContext, outbox);

        var result = await notifyService.ChangeStatusAsync(
            caseEntity.Id,
            OrganizationId,
            CaseStatus.UnderReview);

        Assert.NotNull(result);
        Assert.Contains(
            outbox.Queued,
            notification => notification.RecipientUserId == citizenUserId &&
                            notification.Type == NotificationTypes.CaseStatusChanged);
        Assert.Contains(
            outbox.Queued,
            notification => notification.RecipientUserId == viewerUser.Id &&
                            notification.Type == NotificationTypes.CaseStatusChanged &&
                            notification.IdempotencyKey.StartsWith("case-status-staff:"));
    }
}
