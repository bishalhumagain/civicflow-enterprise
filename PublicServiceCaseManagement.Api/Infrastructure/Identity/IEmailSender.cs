namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Abstraction for sending account lifecycle emails (verification,
/// password reset). The default implementation logs the message so the
/// flow is fully functional in development without an SMTP dependency.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
