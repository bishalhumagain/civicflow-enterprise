
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Staff;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

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
            Now);

    [Fact]
    public async Task ChangeStatusAsync_WhenStatusChanges_SavesCaseAndHistory()
    {
        await using var dbContext = CreateDbContext();
        var actorUserId = Guid.NewGuid();

        var caseEntity = CreateCase();

        dbContext.Cases.Add(caseEntity);
        await dbContext.SaveChangesAsync();

        var service = new CaseService(dbContext);

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

        var service = new CaseService(dbContext);

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

        var service = new CaseService(dbContext);

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

        var service = new CaseService(dbContext);

        var result = await service.ChangeStatusAsync(
            caseEntity.Id,
            OrganizationId,
            CaseStatus.UnderReview);

        Assert.NotNull(result);
        Assert.Equal(CaseStatus.UnderReview, result.Status);
        Assert.Empty(await dbContext.CaseStatusHistories.ToListAsync());
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

        var service = new CaseService(dbContext);

        var result = await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            staffMember.Id,
            CancellationToken.None,
            actorUserId);

        Assert.NotNull(result);
        Assert.Equal(staffMember.Id, result.AssignedToStaffMemberId);

        var history = await dbContext.CaseAssignmentHistories
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(caseEntity.Id, history.CaseId);
        Assert.Equal(OrganizationId, history.OrganizationId);
        Assert.Null(history.PreviousStaffMemberId);
        Assert.Equal(staffMember.Id, history.NewStaffMemberId);
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

        var service = new CaseService(dbContext);

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

        var service = new CaseService(dbContext);

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
    public async Task AssignAsync_WhenStaffMemberIsInactive_ThrowsAndDoesNotCreateHistory()
    {
        await using var dbContext = CreateDbContext();

        var caseEntity = CreateCase();
        var staffMember = CreateStaffMember();
        staffMember.Deactivate(Now);

        dbContext.Cases.Add(caseEntity);
        dbContext.StaffMembers.Add(staffMember);
        await dbContext.SaveChangesAsync();

        var service = new CaseService(dbContext);

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

        var service = new CaseService(dbContext);

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

        var service = new CaseService(dbContext);

        var result = await service.AssignAsync(
            caseEntity.Id,
            OrganizationId,
            staffMember.Id,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Null(caseEntity.AssignedToStaffMemberId);
        Assert.Empty(await dbContext.CaseAssignmentHistories.ToListAsync());
    }
}
