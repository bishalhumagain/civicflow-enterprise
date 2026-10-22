using PublicServiceCaseManagement.Api.Infrastructure.Audit;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Records security-relevant events (authentication, account lifecycle,
/// privilege changes) to a dedicated audit log category and to the persisted
/// audit trail. Kept separate from the general application log so it can be
/// shipped to a SIEM or retained independently.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Records an arbitrary audit entry. This is the primitive every other
    /// method builds on.
    /// </summary>
    Task RecordAsync(
        AuditRecord record,
        CancellationToken cancellationToken = default);

    Task AuthenticationSucceededAsync(
        string email,
        Guid? userId,
        string context,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    Task AuthenticationFailedAsync(
        string email,
        string reason,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    Task AccountLifecycleAsync(
        string email,
        Guid? userId,
        string action,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);

    Task PrivilegeChangeAsync(
        Guid userId,
        string detail,
        Guid? organizationId = null,
        CancellationToken cancellationToken = default);
}
