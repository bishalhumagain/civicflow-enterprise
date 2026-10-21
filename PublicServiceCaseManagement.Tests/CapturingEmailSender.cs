using System.Collections.Concurrent;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// Test email sender that captures outbound messages so tests can retrieve
/// verification and password-reset links without a real mail server.
/// </summary>
public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<CapturedEmail> messages = new();

    public IReadOnlyCollection<CapturedEmail> Messages =>
        messages.ToArray();

    public Task SendAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        messages.Enqueue(new CapturedEmail(toEmail, subject, body));
        return Task.CompletedTask;
    }

    public CapturedEmail? LastFor(string toEmail) =>
        messages
            .Where(message =>
                string.Equals(
                    message.ToEmail,
                    toEmail,
                    StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

    public string? ExtractToken(string body, string parameterName)
    {
        var marker = $"{parameterName}=";
        var index = body.IndexOf(marker, StringComparison.Ordinal);

        if (index < 0)
        {
            return null;
        }

        var start = index + marker.Length;
        var end = body.IndexOf('&', start);

        if (end < 0)
        {
            end = body.Length;
        }

        return Uri.UnescapeDataString(body[start..end]);
    }

    public sealed record CapturedEmail(
        string ToEmail,
        string Subject,
        string Body);
}
