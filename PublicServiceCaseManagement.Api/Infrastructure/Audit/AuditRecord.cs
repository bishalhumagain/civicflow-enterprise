using PublicServiceCaseManagement.Api.Domain.Audit;

namespace PublicServiceCaseManagement.Api.Infrastructure.Audit;

/// <summary>
/// A single audit entry handed to the audit trail writer. The writer turns
/// it into a persisted <see cref="AuditEvent"/>.
/// </summary>
public sealed record AuditRecord
{
    public required AuditCategory Category { get; init; }

    public required string Action { get; init; }

    public Guid? OrganizationId { get; init; }

    public Guid? ActorUserId { get; init; }

    public string ActorEmail { get; init; } = string.Empty;

    public string? EntityType { get; init; }

    public string? EntityId { get; init; }

    public string? Details { get; init; }
}
