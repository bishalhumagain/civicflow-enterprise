using Microsoft.Extensions.Logging;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

/// <summary>
/// The development email sender writes the message body — which carries the
/// one-time email-confirmation and password-reset links — to the log. The
/// body must be logged at Information level so the links are visible with
/// the default log configuration.
/// </summary>
public sealed class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendAsync_LogsBodyAtInformationLevel()
    {
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(logger);

        await sender.SendAsync(
            "citizen@example.com",
            "Confirm your email address",
            "https://localhost:5173/citizen/verify-email?email=citizen%40example.com&token=abc123");

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("citizen@example.com", entry.Message);
        Assert.Contains("abc123", entry.Message);
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
