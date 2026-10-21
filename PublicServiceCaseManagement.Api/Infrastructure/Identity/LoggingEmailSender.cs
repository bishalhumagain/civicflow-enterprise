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
        // The message body contains one-time action links (email confirmation,
        // password reset). Logging it at Information makes the flow usable in
        // development without an SMTP server; production deployments replace
        // this sender with a real delivery implementation.
        logger.LogInformation(
            "Email queued for {Recipient} with subject '{Subject}'. Body: {Body}",
            toEmail,
            subject,
            body);

        return Task.CompletedTask;
    }
}
