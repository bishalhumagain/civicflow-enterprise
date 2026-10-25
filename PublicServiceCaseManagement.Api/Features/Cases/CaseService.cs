using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Notifications;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Features.Notifications;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class CaseService(
    CivicFlowDbContext dbContext,
    IAuditLogger auditLogger,
    INotificationOutbox notificationOutbox) : ICaseService
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
        if (submittedByUserId.HasValue)
        {
            await notificationOutbox.EnqueueAsync(
                submittedByUserId.Value,
                NotificationTypes.CaseSubmitted,
                "Case received",
                "Your case was received and is awaiting review.",
                "Case",
                newCase.Id,
                $"case-submitted:{newCase.Id}",
                organizationId,
                newCase.Id.ToString("N"),
                cancellationToken);
        }

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
        var updates = await dbContext.CaseUpdates
            .Where(update => update.CaseId == caseId &&
                             update.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        dbContext.CaseDocuments.RemoveRange(documents);
        dbContext.CaseStatusHistories.RemoveRange(statusHistory);
        dbContext.CaseAssignmentHistories.RemoveRange(assignmentHistory);
        dbContext.CaseUpdates.RemoveRange(updates);
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
        Guid? assignedToStaffMemberId = null,
        CaseStatus? status = null,
        CasePriority? priority = null,
        string? search = null)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = dbContext.Cases
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId &&
                        (assignedToStaffMemberId == null ||
                         c.AssignedToStaffMemberId == assignedToStaffMemberId));

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(c => c.Priority == priority.Value);
        }

        var term = TextNormalization
            .CollapseWhitespace(search)
            .ToLowerInvariant();

        if (term.Length > 0)
        {
            query = query.Where(c =>
                c.ReferenceNumber.ToLower().Contains(term) ||
                c.Title.ToLower().Contains(term) ||
                c.Description.ToLower().Contains(term));
        }

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
        Guid? authorizedStaffMemberId = null,
        CancellationToken cancellationToken = default,
        string? explanation = null,
        bool explanationVisibleToCitizen = false)
    {
        var caseEntity = await dbContext.Cases
            .SingleOrDefaultAsync(c =>
                c.Id == caseId &&
                c.OrganizationId == organizationId &&
                (authorizedStaffMemberId == null ||
                 (c.AssignedToStaffMemberId == authorizedStaffMemberId &&
                  c.AssignedRole == CaseAssignmentRole.CaseWorker)),
                cancellationToken);

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
            changedByUserId,
            explanation,
            explanationVisibleToCitizen);

        dbContext.CaseStatusHistories.Add(history);

        if (caseEntity.SubmittedByUserId.HasValue)
        {
            await notificationOutbox.EnqueueAsync(
                caseEntity.SubmittedByUserId.Value,
                NotificationTypes.CaseStatusChanged,
                "Case status updated",
                history.IsVisibleToCitizen && history.Explanation is { } visible
                    ? $"Your case moved to {caseEntity.Status}: " +
                      TruncateNotificationMessage(visible, 400)
                    : $"Your case moved to {caseEntity.Status}.",
                "Case",
                caseEntity.Id,
                $"case-status:{history.Id}",
                organizationId,
                history.Id.ToString("N"),
                cancellationToken);
        }

        // Staff members assigned to the case — either as the working assignee
        // or in a read-only viewer role — are watchers, so a status change must
        // notify them too. Recipients are resolved from the assigned staff
        // member's matching active membership users by email.
        if (caseEntity.AssignedToStaffMemberId is { } assignedStaffMemberId)
        {
            var assignedStaffEmail = await dbContext.StaffMembers
                .AsNoTracking()
                .Where(staff => staff.Id == assignedStaffMemberId)
                .Select(staff => staff.Email)
                .SingleOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(assignedStaffEmail))
            {
                var watcherUserIds = await (
                    from membership in dbContext.OrganizationMemberships
                    join user in dbContext.ApplicationUsers
                        on membership.UserId equals user.Id
                    where membership.OrganizationId == organizationId &&
                          membership.IsActive &&
                          user.IsActive &&
                          user.Email != null &&
                          user.Email.ToUpper() == assignedStaffEmail.ToUpper()
                    select user.Id)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var watcherUserId in watcherUserIds)
                {
                    await notificationOutbox.EnqueueAsync(
                        watcherUserId,
                        NotificationTypes.CaseStatusChanged,
                        "Case update",
                        "The status of a case you are watching changed.",
                        "Case",
                        caseEntity.Id,
                        $"case-status-staff:{history.Id}:user:{watcherUserId}",
                        organizationId,
                        history.Id.ToString("N"),
                        cancellationToken);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

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

        var assignedUserId = await (
            from membership in dbContext.OrganizationMemberships
            join user in dbContext.ApplicationUsers
                on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId &&
                  membership.IsActive &&
                  user.IsActive &&
                  user.Email != null &&
                  user.Email.ToUpper() == staffMember.Email.ToUpper()
            select (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (assignedUserId.HasValue)
        {
            await notificationOutbox.EnqueueAsync(
                assignedUserId.Value,
                NotificationTypes.CaseAssigned,
                "Case assignment",
                "A case was assigned to you.",
                "Case",
                caseEntity.Id,
                $"case-assigned:{history.Id}",
                organizationId,
                history.Id.ToString("N"),
                cancellationToken);
        }

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

    public async Task<Case?> RouteToOrganizationAdministratorAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        // A newly-created citizen case is owned initially by an organization
        // administrator, who then assigns it to the right team member. Prefer a
        // staff member that corresponds to an active OrganizationAdmin, chosen
        // deterministically (earliest created) so repeat routing is stable.
        var adminEmails = await (
            from membership in dbContext.OrganizationMemberships
            join user in dbContext.ApplicationUsers
                on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId &&
                  membership.Role == OrganizationRole.OrganizationAdmin &&
                  membership.IsActive &&
                  user.IsActive &&
                  user.Email != null
            select (user.Email ?? string.Empty).ToUpper())
            .Distinct()
            .ToListAsync(cancellationToken);

        Guid? adminStaffMemberId = null;

        if (adminEmails.Count > 0)
        {
            adminStaffMemberId = await dbContext.StaffMembers
                .Where(staff => staff.OrganizationId == organizationId &&
                                staff.IsActive &&
                                adminEmails.Contains(staff.Email.ToUpper()))
                .OrderBy(staff => staff.CreatedAt)
                .ThenBy(staff => staff.Id)
                .Select(staff => (Guid?)staff.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (adminStaffMemberId is { } staffMemberId)
        {
            // AssignAsync records the assignment history and notifies the admin
            // (resolved from their matching membership user by email).
            return await AssignAsync(
                caseId,
                organizationId,
                staffMemberId,
                cancellationToken,
                assignedByUserId: null,
                role: CaseAssignmentRole.CaseWorker);
        }

        // No admin has a staff-member row yet, so the case cannot be formally
        // owned by one. Still notify every active administrator so triage
        // begins, and leave the case unassigned until an admin assigns it.
        foreach (var adminUserId in await (
            from membership in dbContext.OrganizationMemberships
            join user in dbContext.ApplicationUsers
                on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId &&
                  membership.Role == OrganizationRole.OrganizationAdmin &&
                  membership.IsActive &&
                  user.IsActive &&
                  user.Email != null
            select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken))
        {
            await notificationOutbox.EnqueueAsync(
                adminUserId,
                NotificationTypes.CaseAssigned,
                "New case needs assignment",
                "A case was submitted. Assign it to a team member.",
                "Case",
                caseId,
                $"case-triage:{caseId}:user:{adminUserId}",
                organizationId,
                caseId.ToString("N"),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(
            organizationId,
            caseId,
            cancellationToken);
    }

    public async Task<CaseUpdate?> AddUpdateAsync(
        Guid caseId,
        Guid organizationId,
        CaseUpdateKind kind,
        string message,
        Guid authorUserId,
        Guid? authorizedStaffMemberId = null,
        CancellationToken cancellationToken = default)
    {
        if (kind is not (CaseUpdateKind.PublicUpdate
            or CaseUpdateKind.InformationRequest
            or CaseUpdateKind.InternalNote))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "Staff can add public updates, information requests, and " +
                "internal notes.");
        }

        var caseEntity = await dbContext.Cases
            .SingleOrDefaultAsync(
                c => c.Id == caseId &&
                     c.OrganizationId == organizationId &&
                     (authorizedStaffMemberId == null ||
                      (c.AssignedToStaffMemberId == authorizedStaffMemberId &&
                       c.AssignedRole == CaseAssignmentRole.CaseWorker)),
                cancellationToken);

        if (caseEntity is null)
        {
            return null;
        }

        if (caseEntity.Status is CaseStatus.Closed or CaseStatus.Rejected)
        {
            throw new InvalidOperationException(
                $"A case in {caseEntity.Status} status cannot receive new updates.");
        }

        var createdAt = DateTimeOffset.UtcNow;
        CaseStatusHistory? history = null;

        if (kind == CaseUpdateKind.InformationRequest &&
            caseEntity.Status != CaseStatus.AwaitingInformation)
        {
            var previousStatus = caseEntity.Status;

            caseEntity.ChangeStatus(CaseStatus.AwaitingInformation, createdAt);
            history = CaseStatusHistory.Create(
                caseEntity.Id,
                caseEntity.OrganizationId,
                previousStatus,
                caseEntity.Status,
                createdAt,
                authorUserId,
                "Additional information was requested.",
                isVisibleToCitizen: true);

            dbContext.CaseStatusHistories.Add(history);
        }

        var update = CaseUpdate.Create(
            caseEntity.Id,
            caseEntity.OrganizationId,
            kind,
            message,
            authorUserId,
            createdAt);

        dbContext.CaseUpdates.Add(update);

        if (update.IsVisibleToCitizen && caseEntity.SubmittedByUserId.HasValue)
        {
            await notificationOutbox.EnqueueAsync(
                caseEntity.SubmittedByUserId.Value,
                kind == CaseUpdateKind.InformationRequest
                    ? NotificationTypes.CaseInformationRequested
                    : NotificationTypes.CaseUpdateAdded,
                kind == CaseUpdateKind.InformationRequest
                    ? "Information requested"
                    : "New case update",
                kind == CaseUpdateKind.InformationRequest
                    ? "Your case team needs more information. Open your case to respond."
                    : "A new update was added to your case.",
                "Case",
                caseEntity.Id,
                $"case-update:{update.Id}",
                organizationId,
                update.Id.ToString("N"),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Case,
                Action = kind switch
                {
                    CaseUpdateKind.InformationRequest =>
                        AuditActions.CaseInformationRequested,
                    CaseUpdateKind.InternalNote => AuditActions.CaseNoteAdded,
                    _ => AuditActions.CaseUpdatePublished
                },
                OrganizationId = organizationId,
                EntityType = "Case",
                EntityId = caseEntity.Id.ToString(),
                Details =
                    $"{caseEntity.ReferenceNumber} {kind} " +
                    $"(visible to citizen: {update.IsVisibleToCitizen})"
            },
            cancellationToken);

        return update;
    }

    public async Task<CaseUpdate?> AddCitizenResponseAsync(
        Guid caseId,
        Guid citizenUserId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var caseEntity = await dbContext.Cases
            .SingleOrDefaultAsync(
                c => c.Id == caseId &&
                     c.SubmittedByUserId == citizenUserId,
                cancellationToken);

        if (caseEntity is null)
        {
            return null;
        }

        if (caseEntity.Status is CaseStatus.Closed or CaseStatus.Rejected)
        {
            throw new InvalidOperationException(
                $"A case in {caseEntity.Status} status cannot receive new updates.");
        }

        var createdAt = DateTimeOffset.UtcNow;
        CaseStatusHistory? history = null;

        if (caseEntity.Status == CaseStatus.AwaitingInformation)
        {
            caseEntity.ChangeStatus(CaseStatus.UnderReview, createdAt);
            history = CaseStatusHistory.Create(
                caseEntity.Id,
                caseEntity.OrganizationId,
                CaseStatus.AwaitingInformation,
                caseEntity.Status,
                createdAt,
                citizenUserId,
                "Citizen provided the requested information.",
                isVisibleToCitizen: true);

            dbContext.CaseStatusHistories.Add(history);
        }

        var update = CaseUpdate.Create(
            caseEntity.Id,
            caseEntity.OrganizationId,
            CaseUpdateKind.CitizenResponse,
            message,
            citizenUserId,
            createdAt);

        dbContext.CaseUpdates.Add(update);

        if (caseEntity.AssignedToStaffMemberId is { } assignedStaffMemberId)
        {
            foreach (var watcherUserId in await GetAssignedStaffWatcherUserIdsAsync(
                         caseEntity.OrganizationId,
                         assignedStaffMemberId,
                         cancellationToken))
            {
                await notificationOutbox.EnqueueAsync(
                    watcherUserId,
                    NotificationTypes.CaseCitizenResponded,
                    "Citizen response received",
                    "The citizen added information to a case you are handling.",
                    "Case",
                    caseEntity.Id,
                    $"case-citizen-response:{update.Id}:user:{watcherUserId}",
                    caseEntity.OrganizationId,
                    update.Id.ToString("N"),
                    cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.RecordAsync(
            new AuditRecord
            {
                Category = AuditCategory.Case,
                Action = AuditActions.CaseCitizenResponded,
                OrganizationId = caseEntity.OrganizationId,
                EntityType = "Case",
                EntityId = caseEntity.Id.ToString(),
                Details = $"{caseEntity.ReferenceNumber} citizen response"
            },
            cancellationToken);

        return update;
    }

    public async Task<IReadOnlyList<CaseUpdate>> GetUpdatesAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.CaseUpdates
            .AsNoTracking()
            .Where(update =>
                update.CaseId == caseId &&
                update.OrganizationId == organizationId)
            .OrderBy(update => update.CreatedAt)
            .ThenBy(update => update.Id)
            .ToListAsync(cancellationToken);
    }

    private static string TruncateNotificationMessage(
        string message,
        int maximumLength) =>
        message.Length <= maximumLength
            ? message
            : message[..(maximumLength - 3)] + "...";

    private async Task<IReadOnlyList<Guid>> GetAssignedStaffWatcherUserIdsAsync(
        Guid organizationId,
        Guid assignedStaffMemberId,
        CancellationToken cancellationToken)
    {
        var assignedStaffEmail = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staff => staff.Id == assignedStaffMemberId)
            .Select(staff => staff.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(assignedStaffEmail))
        {
            return Array.Empty<Guid>();
        }

        return await (
            from membership in dbContext.OrganizationMemberships
            join user in dbContext.ApplicationUsers
                on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId &&
                  membership.IsActive &&
                  user.IsActive &&
                  user.Email != null &&
                  user.Email.ToUpper() == assignedStaffEmail.ToUpper()
            select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
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