namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Development placeholder that writes one-time links to the local log
/// instead of delivering email.
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
            "DEVELOPMENT ONLY: one-time email action link for {Email}. Do not share or retain this log.{NewLine}{Body}",
            toEmail,
            Environment.NewLine,
            body);
        return Task.CompletedTask;
    }
}