using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Audit.Contracts;

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
public sealed record AuditQuery : IValidatableObject
{
    [StringLength(100)] public string? Category { get; init; }

    [StringLength(100)] public string? Action { get; init; }

    public Guid? ActorUserId { get; init; }

    public Guid? OrganizationId { get; init; }

    [StringLength(200)] public string? Search { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    [Range(1, 1_000_000)] public int Page { get; init; } = 1;

    [Range(1, 200)] public int PageSize { get; init; } = 50;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && From.Value > To.Value)
        {
            yield return new ValidationResult(
                "'from' must be earlier than or equal to 'to'.",
                [nameof(From), nameof(To)]);
        }
    }
}