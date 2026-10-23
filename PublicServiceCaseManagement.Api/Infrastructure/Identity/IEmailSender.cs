namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Abstraction for sending account lifecycle emails (verification,
/// password reset, and invitations). Development explicitly rejects delivery;
/// other environments use the configured TLS-protected SMTP transport.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
