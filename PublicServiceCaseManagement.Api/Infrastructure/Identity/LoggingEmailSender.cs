namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Development-only placeholder used when no SMTP transport is configured.
/// It does not deliver mail; instead it writes the full message to the API
/// console so verification, password-reset, and invitation links can be used
/// locally. Never register this sender outside Development.
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
            "DEVELOPMENT ONLY: email delivery is not configured, so the " +
            "message below was not sent. Do not use this output outside a " +
            "private local run.{NewLine}" +
            "To: {Recipient}{NewLine}" +
            "Subject: {Subject}{NewLine}" +
            "{NewLine}{Body}",
            Environment.NewLine,
            toEmail,
            Environment.NewLine,
            subject,
            Environment.NewLine,
            Environment.NewLine,
            body);
        return Task.CompletedTask;
    }
}
