using Microsoft.Extensions.Logging;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Tests;

public sealed class LoggingEmailSenderTests
{
    [Fact]
    public async Task SendAsync_LogsOneTimeActionLinkForDevelopment()
    {
        const string actionLink = "https://localhost/verify?token=development-only-token";
        var logger = new RecordingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(logger);

        await sender.SendAsync(
            "citizen@example.com",
            "Confirm your email address",
            actionLink);

        var entry = Assert.Single(logger.Entries);

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("DEVELOPMENT ONLY", entry.Message);
        Assert.Contains("citizen@example.com", entry.Message);
        Assert.Contains(actionLink, entry.Message);
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