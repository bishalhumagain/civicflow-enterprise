using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class CaseService(CivicFlowDbContext dbContext) : ICaseService
{
    public async Task<Case> CreateAsync(
        Guid organizationId,
        string title,
        string description,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var referenceNumber = $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant();

        var newCase = Case.Create(
            organizationId,
            referenceNumber,
            title,
            description,
            now);

        dbContext.Cases.Add(newCase);
        await dbContext.SaveChangesAsync(cancellationToken);

        return newCase;
    }

    public async Task<Case?> GetByIdAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Cases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == id && c.OrganizationId == organizationId,
                cancellationToken);
    }

    public async Task<(IReadOnlyList<Case> Items, int TotalCount)> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Cases
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Case?> ChangeStatusAsync(
        Guid caseId,
        Guid organizationId,
        CaseStatus newStatus)
    {
        var caseEntity = await dbContext.Cases
            .SingleOrDefaultAsync(c =>
                c.Id == caseId &&
                c.OrganizationId == organizationId);

        if (caseEntity is null)
        {
            return null;
        }


        var previousStatus = caseEntity.Status;

        if (previousStatus == newStatus)
        {
            return caseEntity;
        }

        var changedAt = DateTimeOffset.UtcNow;

        caseEntity.ChangeStatus(newStatus, changedAt);

        var history = CaseStatusHistory.Create(
            caseEntity.Id,
            caseEntity.OrganizationId,
            previousStatus,
            caseEntity.Status,
            changedAt);

        dbContext.CaseStatusHistories.Add(history);

        await dbContext.SaveChangesAsync();

        return caseEntity;
    }

    public async Task<Case?> AssignAsync(
        Guid caseId,
        Guid organizationId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        var caseEntity = await dbContext.Cases
            .SingleOrDefaultAsync(
                c => c.Id == caseId &&
                     c.OrganizationId == organizationId,
                cancellationToken);

        if (caseEntity is null)
        {
            return null;
        }

        var staffMember = await dbContext.StaffMembers
            .SingleOrDefaultAsync(
                s => s.Id == staffMemberId &&
                     s.OrganizationId == organizationId &&
                     s.IsActive,
                cancellationToken);

        if (staffMember is null)
        {
            throw new InvalidOperationException(
                "The staff member does not exist, is inactive, or belongs to another organization.");
        }

        var previousStaffMemberId = caseEntity.AssignedToStaffMemberId;

        var assignedAt = DateTimeOffset.UtcNow;

        caseEntity.AssignTo(staffMember.Id, assignedAt);

        if (previousStaffMemberId == staffMember.Id)
        {
            return caseEntity;
        }

        var history = CaseAssignmentHistory.Create(
            caseEntity.Id,
            caseEntity.OrganizationId,
            previousStaffMemberId,
            staffMember.Id,
            assignedAt);

        dbContext.CaseAssignmentHistories.Add(history);

        await dbContext.SaveChangesAsync(cancellationToken);

        return caseEntity;
    }

    public async Task<IReadOnlyList<CaseStatusHistory>> GetStatusHistoryAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.CaseStatusHistories
            .AsNoTracking()
            .Where(history =>
                history.CaseId == caseId &&
                history.OrganizationId == organizationId)
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<CaseAssignmentHistory>> GetAssignmentHistoryAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var caseExists = await dbContext.Cases
            .AnyAsync(
                c => c.Id == caseId &&
                     c.OrganizationId == organizationId,
                cancellationToken);

        if (!caseExists)
            return Array.Empty<CaseAssignmentHistory>();

        return await dbContext.CaseAssignmentHistories
            .AsNoTracking()
            .Where(h => h.CaseId == caseId &&
                        h.OrganizationId == organizationId)
            .OrderBy(h => h.AssignedAt)
            .ThenBy(h => h.Id)
            .ToListAsync(cancellationToken);

    }
}