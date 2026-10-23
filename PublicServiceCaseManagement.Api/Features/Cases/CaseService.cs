using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class CaseService(
    CivicFlowDbContext dbContext,
    IAuditLogger auditLogger) : ICaseService
{
    public async Task<Case> CreateAsync(
        Guid organizationId,
        string title,
        string description,
        CancellationToken cancellationToken,
        Guid? submittedByUserId = null)
    {
        var now = DateTimeOffset.UtcNow;
        var referenceNumber = $"CF-{Guid.NewGuid():N}"[..11].ToUpperInvariant();

        var newCase = Case.Create(
            organizationId,
            referenceNumber,
            title,
            description,
            now,
            submittedByUserId);

        dbContext.Cases.Add(newCase);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Case,
                Action = AuditActions.CaseCreated,
                OrganizationId = organizationId,
                EntityType = "Case",
                EntityId = newCase.Id.ToString(),
                Details = $"{newCase.ReferenceNumber} {newCase.Title}"
            },
            cancellationToken);

        return newCase;
    }

    public async Task<Case?> GetByIdAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken,
        Guid? assignedToStaffMemberId = null)
    {
        return await dbContext.Cases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == id &&
                     c.OrganizationId == organizationId &&
                     (assignedToStaffMemberId == null ||
                      c.AssignedToStaffMemberId == assignedToStaffMemberId),
                cancellationToken);
    }

    public async Task<Case?> UpdateDetailsAsync(
        Guid organizationId,
        Guid caseId,
        string title,
        string description,
        CancellationToken cancellationToken,
        Guid? authorizedStaffMemberId = null)
    {
        var caseEntity = await dbContext.Cases
            .SingleOrDefaultAsync(
                item => item.Id == caseId &&
                        item.OrganizationId == organizationId &&
                        (authorizedStaffMemberId == null ||
                         (item.AssignedToStaffMemberId == authorizedStaffMemberId &&
                          item.AssignedRole == CaseAssignmentRole.CaseWorker)),
                cancellationToken);
        if (caseEntity is null)
            return null;

        caseEntity.UpdateDetails(title, description, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Case,
                Action = AuditActions.CaseUpdated,
                OrganizationId = organizationId,
                EntityType = "Case",
                EntityId = caseEntity.Id.ToString(),
                Details = $"{caseEntity.ReferenceNumber} {caseEntity.Title}"
            },
            cancellationToken);

        return caseEntity;
    }

    public async Task<IReadOnlyList<string>?> DeleteAsync(
        Guid organizationId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var caseEntity = await dbContext.Cases
            .SingleOrDefaultAsync(
                item => item.Id == caseId &&
                        item.OrganizationId == organizationId,
                cancellationToken);
        if (caseEntity is null)
            return null;

        var documents = await dbContext.CaseDocuments
            .Where(document => document.CaseId == caseId &&
                               document.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var statusHistory = await dbContext.CaseStatusHistories
            .Where(history => history.CaseId == caseId &&
                              history.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);
        var assignmentHistory = await dbContext.CaseAssignmentHistories
            .Where(history => history.CaseId == caseId &&
                              history.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        dbContext.CaseDocuments.RemoveRange(documents);
        dbContext.CaseStatusHistories.RemoveRange(statusHistory);
        dbContext.CaseAssignmentHistories.RemoveRange(assignmentHistory);
        dbContext.Cases.Remove(caseEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Case,
                Action = AuditActions.CaseDeleted,
                OrganizationId = organizationId,
                EntityType = "Case",
                EntityId = caseId.ToString(),
                Details = $"{caseEntity.ReferenceNumber} {caseEntity.Title}"
            },
            cancellationToken);

        return documents.Select(document => document.StorageKey).ToArray();
    }

    public async Task<(IReadOnlyList<Case> Items, int TotalCount)> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken,
        Guid? assignedToStaffMemberId = null)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Cases
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId &&
                        (assignedToStaffMemberId == null ||
                         c.AssignedToStaffMemberId == assignedToStaffMemberId));

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
        CaseStatus newStatus,
        Guid? changedByUserId = null,
        Guid? authorizedStaffMemberId = null)
    {
        var caseEntity = await dbContext.Cases
            .SingleOrDefaultAsync(c =>
                c.Id == caseId &&
                c.OrganizationId == organizationId &&
                (authorizedStaffMemberId == null ||
                 (c.AssignedToStaffMemberId == authorizedStaffMemberId &&
                  c.AssignedRole == CaseAssignmentRole.CaseWorker)));

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
            changedAt,
            changedByUserId);

        dbContext.CaseStatusHistories.Add(history);

        await dbContext.SaveChangesAsync();

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Case,
                Action = AuditActions.CaseStatusChanged,
                OrganizationId = caseEntity.OrganizationId,
                EntityType = "Case",
                EntityId = caseEntity.Id.ToString(),
                Details =
                    $"{caseEntity.ReferenceNumber} status " +
                    $"{previousStatus} -> {caseEntity.Status}"
            });

        return caseEntity;
    }

    public async Task<Case?> AssignAsync(
        Guid caseId,
        Guid organizationId,
        Guid staffMemberId,
        CancellationToken cancellationToken,
        Guid? assignedByUserId = null,
        Domain.Cases.CaseAssignmentRole role =
            Domain.Cases.CaseAssignmentRole.CaseWorker)
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
        var previousRole = caseEntity.AssignedRole;

        var assignedAt = DateTimeOffset.UtcNow;

        caseEntity.AssignTo(staffMember.Id, role, assignedAt);

        if (previousStaffMemberId == staffMember.Id &&
            previousRole == role)
        {
            return caseEntity;
        }

        var history = CaseAssignmentHistory.Create(
            caseEntity.Id,
            caseEntity.OrganizationId,
            previousStaffMemberId,
            staffMember.Id,
            previousRole,
            role,
            assignedAt,
            assignedByUserId);

        dbContext.CaseAssignmentHistories.Add(history);

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Case,
                Action = AuditActions.CaseAssigned,
                OrganizationId = caseEntity.OrganizationId,
                EntityType = "Case",
                EntityId = caseEntity.Id.ToString(),
                Details =
                    $"{caseEntity.ReferenceNumber} assigned to " +
                    $"{staffMember.FullName} <{staffMember.Email}>"
            },
            cancellationToken);

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