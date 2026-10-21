namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

public sealed class AuditLogger(ILogger<AuditLogger> logger) : IAuditLogger
{
    public void AuthenticationSucceeded(
        string email,
        Guid? userId,
        string context) =>
        logger.LogInformation(
            "AUTH_SUCCESS email={Email} userId={UserId} context={Context}",
            email,
            userId,
            context);

    public void AuthenticationFailed(string email, string reason) =>
        logger.LogWarning(
            "AUTH_FAILURE email={Email} reason={Reason}",
            email,
            reason);

    public void AccountLifecycle(
        string email,
        Guid? userId,
        string action) =>
        logger.LogInformation(
            "ACCOUNT_LIFECYCLE email={Email} userId={UserId} action={Action}",
            email,
            userId,
            action);

    public void PrivilegeChange(Guid userId, string detail) =>
        logger.LogWarning(
            "PRIVILEGE_CHANGE userId={UserId} detail={Detail}",
            userId,
            detail);
}
