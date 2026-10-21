namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Development placeholder email sender that writes messages to the log
/// instead of delivering them. It never includes secrets in logs beyond
/// the message body, which in development contains one-time action links.
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
        logger.LogInformation(
            "Email queued for {Recipient} with subject '{Subject}'.",
            toEmail,
            subject);

        logger.LogDebug(
            "Email body for {Recipient}: {Body}",
            toEmail,
            body);

        return Task.CompletedTask;
    }
}
