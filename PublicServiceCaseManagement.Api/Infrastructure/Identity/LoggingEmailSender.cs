namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Development placeholder that explicitly fails instead of logging or
/// pretending to deliver a message containing a one-time action link.
/// </summary>
public sealed class LoggingEmailSender(
    ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogWarning(
            "Email delivery is not configured in Development; no message was sent.");
        return Task.FromException(
            new InvalidOperationException(
                "Email delivery is not configured for this environment."));
    }
}