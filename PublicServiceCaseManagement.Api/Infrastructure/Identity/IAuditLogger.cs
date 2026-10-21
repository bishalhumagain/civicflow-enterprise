namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Records security-relevant events (authentication, account lifecycle,
/// privilege changes) to a dedicated audit log category. Kept separate
/// from the general application log so it can be shipped to a SIEM or
/// retained independently.
/// </summary>
public interface IAuditLogger
{
    void AuthenticationSucceeded(string email, Guid? userId, string context);

    void AuthenticationFailed(string email, string reason);

    void AccountLifecycle(string email, Guid? userId, string action);

    void PrivilegeChange(Guid userId, string detail);
}
