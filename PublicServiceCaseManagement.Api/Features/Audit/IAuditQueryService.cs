namespace PublicServiceCaseManagement.Api.Features.Audit;

/// <summary>
/// Read access to the persisted audit trail.
/// </summary>
public interface IAuditQueryService
{
    /// <summary>
    /// Reads the audit trail across the whole platform. Only platform
    /// administrators may call this.
    /// </summary>
    Task<AuditEventPageResponse> GetPlatformEventsAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the audit trail of a single organization. Only administrators of
    /// that organization may call this.
    /// </summary>
    Task<AuditEventPageResponse> GetOrganizationEventsAsync(
        Guid organizationId,
        AuditQuery query,
        CancellationToken cancellationToken = default);
}
