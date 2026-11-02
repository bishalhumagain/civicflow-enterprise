namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Development placeholder that suppresses email delivery without logging
/// message content or recipient information.
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
            "DEVELOPMENT ONLY: email delivery is suppressed; no message content was logged.");
        return Task.CompletedTask;
    }
}