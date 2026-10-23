using Microsoft.Extensions.Logging;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Tests;

public sealed class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendAsync_DoesNotLogMessageContentAndFailsExplicitly()
    {
        const string actionToken = "sensitive-action-token";
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(logger);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(
            "citizen@example.com",
            "Confirm your email address",
            $"https://localhost/verify?token={actionToken}"));

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(
            "Email delivery is not configured for this environment.",
            exception.Message);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain("citizen@example.com", entry.Message);
        Assert.DoesNotContain("Confirm your email address", entry.Message);
        Assert.DoesNotContain(actionToken, entry.Message);
        Assert.Contains("no message was sent", entry.Message);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, string Message);
}