using System.Net;
using System.Net.Mail;
using System.Text;

namespace PublicServiceCaseManagement.Api.Infrastructure.Identity;

/// <summary>
/// Sends account lifecycle and invitation messages through a TLS-protected
/// SMTP relay. SMTP credentials are read only from runtime configuration.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly string host;
    private readonly int port;
    private readonly string fromAddress;
    private readonly string? fromName;
    private readonly string? username;
    private readonly string? password;

    public SmtpEmailSender(IConfiguration configuration)
    {
        var section = configuration.GetSection("Smtp");
        host = section["Host"]?.Trim() ?? string.Empty;
        fromAddress = section["FromAddress"]?.Trim() ?? string.Empty;
        fromName = section["FromName"]?.Trim();
        username = section["Username"];
        password = section["Password"];

        if (!int.TryParse(section["Port"], out port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "SMTP configuration requires a valid port.");
        }

        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException(
                "SMTP configuration requires a host and sender address.");
        }

        try
        {
            _ = new MailAddress(fromAddress);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "SMTP configuration requires a valid sender address.",
                exception);
        }

        if (!bool.TryParse(section["UseSsl"], out var useSsl) || !useSsl)
        {
            throw new InvalidOperationException(
                "SMTP email delivery requires TLS to be enabled.");
        }

        if (string.IsNullOrWhiteSpace(username) !=
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "SMTP username and password must either both be configured or both be omitted.");
        }
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var message = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(fromName)
                ? new MailAddress(fromAddress)
                : new MailAddress(fromAddress, fromName),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(toEmail));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = string.IsNullOrWhiteSpace(username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(username, password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}