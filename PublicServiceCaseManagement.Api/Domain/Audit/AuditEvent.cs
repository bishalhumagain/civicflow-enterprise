namespace PublicServiceCaseManagement.Api.Domain.Audit;

/// <summary>
/// A persisted audit event. Every security-relevant and administrative action
/// is recorded here so platform administrators can see what happened across
/// the whole product, and organization administrators can see what happened
/// inside their own organization.
/// </summary>
/// <remarks>
/// The organization reference is intentionally a plain identifier without a
/// foreign key: the audit trail has to survive the deletion of the
/// organization it describes, and the trail is never tenant-isolated at the
/// database level because platform administrators read across organizations.
/// </remarks>
public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    private AuditEvent(
        AuditCategory category,
        string action,
        Guid? organizationId,
        Guid? actorUserId,
        string actorEmail,
        string? entityType,
        string? entityId,
        string? details,
        DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        Category = category.ToString();
        Action = action;
        OrganizationId = organizationId;
        ActorUserId = actorUserId;
        ActorEmail = actorEmail;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public Guid? OrganizationId { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string ActorEmail { get; private set; } = string.Empty;

    public string? EntityType { get; private set; }

    public string? EntityId { get; private set; }

    public string? Details { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditEvent Create(
        AuditCategory category,
        string action,
        Guid? organizationId,
        Guid? actorUserId,
        string actorEmail,
        string? entityType,
        string? entityId,
        string? details,
        DateTimeOffset occurredAt)
    {
        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(
                nameof(category),
                "The audit category is invalid.");
        }

        if (string.IsNullOrWhiteSpace(action) || action.Trim().Length > 100)
        {
            throw new ArgumentException(
                "An audit action is required and must not exceed 100 characters.",
                nameof(action));
        }

        if (actorEmail is not null && actorEmail.Trim().Length > 254)
        {
            throw new ArgumentException(
                "The actor email must not exceed 254 characters.",
                nameof(actorEmail));
        }

        if (entityType is not null && entityType.Trim().Length > 50)
        {
            throw new ArgumentException(
                "The entity type must not exceed 50 characters.",
                nameof(entityType));
        }

        if (entityId is not null && entityId.Trim().Length > 100)
        {
            throw new ArgumentException(
                "The entity identifier must not exceed 100 characters.",
                nameof(entityId));
        }

        if (details is not null && details.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "The audit details must not exceed 1000 characters.",
                nameof(details));
        }

        return new AuditEvent(
            category,
            action.Trim(),
            organizationId,
            actorUserId,
            (actorEmail ?? string.Empty).Trim(),
            string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim(),
            string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim(),
            string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
            occurredAt);
    }
}
