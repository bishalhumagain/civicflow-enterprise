namespace PublicServiceCaseManagement.Api.Domain.BackgroundJobs;

public sealed class BackgroundJobAttempt
{
    private BackgroundJobAttempt()
    {
    }

    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public int AttemptNumber { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string Status { get; private set; } = BackgroundJobStatuses.Running;
    public string? ErrorCode { get; private set; }
    public string? ErrorDetails { get; private set; }

    public static BackgroundJobAttempt Start(
        Guid jobId,
        int attemptNumber,
        DateTimeOffset startedAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            AttemptNumber = attemptNumber,
            StartedAt = startedAt,
            Status = BackgroundJobStatuses.Running
        };

    public void Complete(DateTimeOffset completedAt)
    {
        Status = BackgroundJobStatuses.Succeeded;
        CompletedAt = completedAt;
    }

    public void Fail(
        DateTimeOffset completedAt,
        string errorCode,
        string errorDetails)
    {
        Status = BackgroundJobStatuses.Failed;
        CompletedAt = completedAt;
        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
    }
}
