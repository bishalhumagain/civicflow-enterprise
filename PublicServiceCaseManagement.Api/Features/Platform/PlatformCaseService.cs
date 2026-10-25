using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Platform;

/// <summary>
/// Builds the cross-organization case directory for a platform super
/// administrator. Every query runs inside the platform read context, whose
/// database policies are SELECT-only.
/// </summary>
public sealed class PlatformCaseService(
    CivicFlowDbContext dbContext,
    IOrganizationTransactionContext transactionContext) : IPlatformCaseService
{
    private static readonly HashSet<string> AllowedSorts =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "created_desc",
            "created_asc",
            "updated_desc",
            "updated_asc",
            "priority_desc",
            "reference_asc"
        };

    public async Task<PlatformCasePageResponse> GetPageAsync(
        int page,
        int pageSize,
        Guid? organizationId,
        CaseStatus? status,
        CasePriority? priority,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var normalizedSort = string.IsNullOrWhiteSpace(sort)
            ? "created_desc"
            : sort.Trim().ToLowerInvariant();

        if (!AllowedSorts.Contains(normalizedSort))
        {
            throw new ArgumentException(
                "The requested sort order is not supported.",
                nameof(sort));
        }

        await transactionContext.BeginPlatformReadAsync(cancellationToken);

        var query = dbContext.Cases.AsNoTracking();

        if (organizationId.HasValue)
        {
            query = query.Where(c => c.OrganizationId == organizationId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(c => c.Priority == priority.Value);
        }

        var term = TextNormalization.CollapseWhitespace(search).ToLowerInvariant();

        if (term.Length > 0)
        {
            query = query.Where(c =>
                c.ReferenceNumber.ToLower().Contains(term) ||
                c.Title.ToLower().Contains(term) ||
                c.Description.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = normalizedSort switch
        {
            "created_asc" => query
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id),
            "updated_desc" => query
                .OrderByDescending(c => c.UpdatedAt)
                .ThenByDescending(c => c.Id),
            "updated_asc" => query
                .OrderBy(c => c.UpdatedAt)
                .ThenBy(c => c.Id),
            "priority_desc" => query
                .OrderByDescending(c => c.Priority)
                .ThenByDescending(c => c.CreatedAt)
                .ThenBy(c => c.Id),
            "reference_asc" => query
                .OrderBy(c => c.ReferenceNumber)
                .ThenBy(c => c.Id),
            _ => query
                .OrderByDescending(c => c.CreatedAt)
                .ThenByDescending(c => c.Id)
        };

        var cases = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var summaries = await ToSummariesAsync(cases, cancellationToken);

        return new PlatformCasePageResponse(
            summaries,
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<PlatformCaseDetailResponse?> GetDetailAsync(
        Guid caseId,
        CancellationToken cancellationToken = default)
    {
        await transactionContext.BeginPlatformReadAsync(cancellationToken);

        var caseEntity = await dbContext.Cases
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == caseId, cancellationToken);

        if (caseEntity is null)
        {
            return null;
        }

        var summary = (await ToSummariesAsync(
            new[] { caseEntity },
            cancellationToken))[0];

        var statusHistory = await dbContext.CaseStatusHistories
            .AsNoTracking()
            .Where(history =>
                history.CaseId == caseId &&
                history.OrganizationId == caseEntity.OrganizationId)
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .ToListAsync(cancellationToken);

        var updates = await dbContext.CaseUpdates
            .AsNoTracking()
            .Where(update =>
                update.CaseId == caseId &&
                update.OrganizationId == caseEntity.OrganizationId)
            .OrderBy(update => update.CreatedAt)
            .ThenBy(update => update.Id)
            .ToListAsync(cancellationToken);

        var assignmentHistory = await dbContext.CaseAssignmentHistories
            .AsNoTracking()
            .Where(history =>
                history.CaseId == caseId &&
                history.OrganizationId == caseEntity.OrganizationId)
            .OrderBy(history => history.AssignedAt)
            .ThenBy(history => history.Id)
            .ToListAsync(cancellationToken);

        var documents = await dbContext.CaseDocuments
            .AsNoTracking()
            .Where(document =>
                document.CaseId == caseId &&
                document.OrganizationId == caseEntity.OrganizationId)
            .OrderBy(document => document.UploadedAt)
            .ThenBy(document => document.Id)
            .ToListAsync(cancellationToken);

        var relatedUserIds = statusHistory
            .Select(history => history.ChangedByUserId)
            .Concat(updates.Select(update => (Guid?)update.AuthorUserId))
            .Concat(documents.Select(document => document.UploadedByUserId))
            .Concat(assignmentHistory.Select(history => history.AssignedByUserId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var relatedStaffIds = assignmentHistory
            .Select(history => (Guid?)history.NewStaffMemberId)
            .Concat(assignmentHistory.Select(history =>
                history.PreviousStaffMemberId))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var emails = await LoadEmailsAsync(
            relatedUserIds,
            relatedStaffIds,
            cancellationToken);

        var staffNames = await LoadStaffNamesAsync(
            relatedStaffIds,
            cancellationToken);

        var submittedBy = caseEntity.SubmittedByUserId.HasValue
            ? (await LoadPersonsAsync(
                new[] { caseEntity.SubmittedByUserId.Value },
                cancellationToken)).GetValueOrDefault(
                    caseEntity.SubmittedByUserId.Value)
            : null;

        PlatformCaseStaffPerson? assignedTo = null;

        if (caseEntity.AssignedToStaffMemberId.HasValue)
        {
            assignedTo = await dbContext.StaffMembers
                .AsNoTracking()
                .Where(staff => staff.Id == caseEntity.AssignedToStaffMemberId.Value)
                .Select(staff => new PlatformCaseStaffPerson(
                    staff.Id,
                    staff.FullName,
                    staff.Email,
                    staff.PhoneNumber,
                    staff.IsActive))
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new PlatformCaseDetailResponse(
            summary,
            submittedBy,
            assignedTo,
            statusHistory
                .Select(history => new PlatformCaseStatusEntry(
                    history.Id,
                    history.PreviousStatus,
                    history.NewStatus,
                    history.ChangedAt,
                    history.ChangedByUserId,
                    ResolveEmail(emails, history.ChangedByUserId),
                    history.Explanation,
                    history.IsVisibleToCitizen))
                .ToList(),
            updates
                .Select(update => new PlatformCaseUpdateEntry(
                    update.Id,
                    update.Kind,
                    update.Message,
                    update.IsVisibleToCitizen,
                    update.AuthorUserId,
                    ResolveEmail(emails, update.AuthorUserId),
                    update.CreatedAt))
                .ToList(),
            assignmentHistory
                .Select(history => new PlatformCaseAssignmentEntry(
                    history.Id,
                    history.PreviousStaffMemberId,
                    ResolveStaffName(staffNames, history.PreviousStaffMemberId),
                    history.NewStaffMemberId,
                    ResolveStaffName(staffNames, history.NewStaffMemberId),
                    history.PreviousRole,
                    history.NewRole,
                    history.AssignedAt,
                    history.AssignedByUserId,
                    ResolveEmail(emails, history.AssignedByUserId)))
                .ToList(),
            documents
                .Select(document => new PlatformCaseDocumentEntry(
                    document.Id,
                    document.CaseId,
                    document.OriginalFileName,
                    document.ContentType,
                    document.SizeBytes,
                    document.Sha256Hash,
                    document.UploadedAt,
                    document.UploadedByUserId,
                    ResolveEmail(emails, document.UploadedByUserId),
                    document.IsVisibleToCitizen))
                .ToList());
    }

    public async Task<CaseDocument?> GetDocumentAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        await transactionContext.BeginPlatformReadAsync(cancellationToken);

        return await dbContext.CaseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                document =>
                    document.Id == documentId &&
                    document.CaseId == caseId,
                cancellationToken);
    }

    private async Task<IReadOnlyList<PlatformCaseSummary>> ToSummariesAsync(
        IReadOnlyList<Case> cases,
        CancellationToken cancellationToken)
    {
        if (cases.Count == 0)
        {
            return Array.Empty<PlatformCaseSummary>();
        }

        var organizationIds = cases
            .Select(c => c.OrganizationId)
            .Distinct()
            .ToList();

        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organizationIds.Contains(organization.Id))
            .ToDictionaryAsync(
                organization => organization.Id,
                organization => organization,
                cancellationToken);

        var staffIds = cases
            .Where(c => c.AssignedToStaffMemberId.HasValue)
            .Select(c => c.AssignedToStaffMemberId!.Value)
            .Distinct()
            .ToList();

        var staffNames = await LoadStaffNamesAsync(staffIds, cancellationToken);

        var submitterIds = cases
            .Where(c => c.SubmittedByUserId.HasValue)
            .Select(c => c.SubmittedByUserId!.Value)
            .Distinct()
            .ToList();

        var submitters = await LoadPersonsAsync(
            submitterIds,
            cancellationToken);

        return cases
            .Select(c =>
            {
                var organization = organizations.GetValueOrDefault(c.OrganizationId);

                return new PlatformCaseSummary(
                    c.Id,
                    c.ReferenceNumber,
                    c.Title,
                    c.Description,
                    c.Status,
                    c.Priority,
                    c.OrganizationId,
                    organization?.Name ?? "Unknown organization",
                    organization?.Code ?? string.Empty,
                    c.AssignedToStaffMemberId,
                    ResolveStaffName(staffNames, c.AssignedToStaffMemberId),
                    c.AssignedRole,
                    c.SubmittedByUserId,
                    c.SubmittedByUserId.HasValue
                        ? submitters.GetValueOrDefault(
                            c.SubmittedByUserId.Value)?.FullName
                        : null,
                    c.CreatedAt,
                    c.UpdatedAt);
            })
            .ToList();
    }

    private async Task<Dictionary<Guid, PlatformCasePerson>> LoadPersonsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, PlatformCasePerson>();
        }

        var users = await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber
            })
            .ToListAsync(cancellationToken);

        var profiles = await dbContext.CitizenProfiles
            .AsNoTracking()
            .Where(profile => userIds.Contains(profile.ApplicationUserId))
            .Select(profile => new
            {
                profile.ApplicationUserId,
                profile.FullName,
                profile.Email,
                profile.PhoneNumber
            })
            .ToListAsync(cancellationToken);

        var profilesByUser = profiles.ToDictionary(
            profile => profile.ApplicationUserId,
            profile => profile);

        return users.ToDictionary(
            user => user.Id,
            user =>
            {
                if (profilesByUser.TryGetValue(user.Id, out var profile))
                {
                    return new PlatformCasePerson(
                        user.Id,
                        profile.FullName,
                        profile.Email,
                        profile.PhoneNumber);
                }

                return new PlatformCasePerson(
                    user.Id,
                    user.FullName,
                    user.Email ?? string.Empty,
                    user.PhoneNumber);
            });
    }

    private async Task<Dictionary<Guid, string>> LoadStaffNamesAsync(
        IReadOnlyCollection<Guid> staffIds,
        CancellationToken cancellationToken)
    {
        if (staffIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staff => staffIds.Contains(staff.Id))
            .Select(staff => new { staff.Id, staff.FullName })
            .ToDictionaryAsync(
                staff => staff.Id,
                staff => staff.FullName,
                cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadEmailsAsync(
        IReadOnlyCollection<Guid> userIds,
        IReadOnlyCollection<Guid> staffIds,
        CancellationToken cancellationToken)
    {
        var emails = new Dictionary<Guid, string>();

        if (userIds.Count > 0)
        {
            foreach (var user in await dbContext.ApplicationUsers
                         .AsNoTracking()
                         .Where(user => userIds.Contains(user.Id))
                         .Select(user => new { user.Id, user.Email })
                         .ToListAsync(cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    emails[user.Id] = user.Email;
                }
            }
        }

        if (staffIds.Count > 0)
        {
            foreach (var staff in await dbContext.StaffMembers
                         .AsNoTracking()
                         .Where(staff => staffIds.Contains(staff.Id))
                         .Select(staff => new { staff.Id, staff.Email })
                         .ToListAsync(cancellationToken))
            {
                emails.TryAdd(staff.Id, staff.Email);
            }
        }

        return emails;
    }

    private static string? ResolveEmail(
        IReadOnlyDictionary<Guid, string> emails,
        Guid? userId) =>
        userId.HasValue ? emails.GetValueOrDefault(userId.Value) : null;

    private static string? ResolveStaffName(
        IReadOnlyDictionary<Guid, string> staffNames,
        Guid? staffId) =>
        staffId.HasValue ? staffNames.GetValueOrDefault(staffId.Value) : null;
}
