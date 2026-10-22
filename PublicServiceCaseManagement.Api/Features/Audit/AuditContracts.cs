namespace PublicServiceCaseManagement.Api.Features.Audit;

/// <summary>
/// A single persisted audit event as returned to administrators.
/// </summary>
public sealed record AuditEventResponse(
    Guid Id,
    string Category,
    string Action,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? ActorUserId,
    string ActorEmail,
    string? EntityType,
    string? EntityId,
    string? Details,
    DateTimeOffset OccurredAt);

/// <summary>
/// A page of audit events.
/// </summary>
public sealed record AuditEventPageResponse(
    IReadOnlyList<AuditEventResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>
/// The filters administrators can apply to the audit log.
/// </summary>
public sealed record AuditQuery
{
    public string? Category { get; init; }

    public string? Action { get; init; }

    public Guid? ActorUserId { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? Search { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;
}
