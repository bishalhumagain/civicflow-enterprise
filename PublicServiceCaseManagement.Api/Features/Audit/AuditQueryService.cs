using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Audit;

/// <summary>
/// Reads the audit trail. The organization scope is always applied by the
/// caller, so an organization administrator can never widen the trail beyond
/// their own organization.
/// </summary>
public sealed class AuditQueryService(
    CivicFlowDbContext dbContext) : IAuditQueryService
{
    private const int MaximumPageSize = 200;

    public Task<AuditEventPageResponse> GetPlatformEventsAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default) =>
        QueryAsync(null, query, cancellationToken);

    public Task<AuditEventPageResponse> GetOrganizationEventsAsync(
        Guid organizationId,
        AuditQuery query,
        CancellationToken cancellationToken = default) =>
        QueryAsync(organizationId, query, cancellationToken);

    private async Task<AuditEventPageResponse> QueryAsync(
        Guid? organizationScope,
        AuditQuery query,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaximumPageSize);

        var events = dbContext.AuditEvents
            .AsNoTracking()
            .AsQueryable();

        if (organizationScope.HasValue)
        {
            events = events.Where(audit => audit.OrganizationId == organizationScope.Value);
        }
        else if (query.OrganizationId.HasValue)
        {
            events = events.Where(audit => audit.OrganizationId == query.OrganizationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            events = events.Where(audit => audit.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            events = events.Where(audit => audit.Action == action);
        }

        if (query.ActorUserId.HasValue)
        {
            events = events.Where(audit => audit.ActorUserId == query.ActorUserId.Value);
        }

        if (query.From.HasValue)
        {
            events = events.Where(audit => audit.OccurredAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            events = events.Where(audit => audit.OccurredAt <= query.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();

            events = events.Where(audit =>
                audit.ActorEmail.ToLower().Contains(search) ||
                audit.Action.ToLower().Contains(search) ||
                (audit.EntityId != null && audit.EntityId.ToLower().Contains(search)) ||
                (audit.Details != null && audit.Details.ToLower().Contains(search)));
        }

        var totalCount = await events.CountAsync(cancellationToken);

        var items = await events
            .OrderByDescending(audit => audit.OccurredAt)
            .ThenBy(audit => audit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var organizationNames = await LoadOrganizationNamesAsync(
            items,
            cancellationToken);

        var responses = items
            .Select(audit => ToResponse(audit, organizationNames))
            .ToList();

        return new AuditEventPageResponse(
            responses,
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadOrganizationNamesAsync(
        IReadOnlyList<AuditEvent> events,
        CancellationToken cancellationToken)
    {
        var organizationIds = events
            .Select(audit => audit.OrganizationId)
            .Where(organizationId => organizationId.HasValue)
            .Select(organizationId => organizationId!.Value)
            .Distinct()
            .ToList();

        if (organizationIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organizationIds.Contains(organization.Id))
            .ToDictionaryAsync(
                organization => organization.Id,
                organization => organization.Name,
                cancellationToken);

        return organizations;
    }

    private static AuditEventResponse ToResponse(
        AuditEvent audit,
        IReadOnlyDictionary<Guid, string> organizationNames) =>
        new(
            audit.Id,
            audit.Category,
            audit.Action,
            audit.OrganizationId,
            audit.OrganizationId.HasValue &&
            organizationNames.TryGetValue(audit.OrganizationId.Value, out var name)
                ? name
                : null,
            audit.ActorUserId,
            audit.ActorEmail,
            audit.EntityType,
            audit.EntityId,
            audit.Details,
            audit.OccurredAt);
}
