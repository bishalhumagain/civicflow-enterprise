namespace PublicServiceCaseManagement.Api.Domain.BackgroundJobs;

public static class BackgroundJobTypes
{
    public const string SendNotificationEmail = "notification.email";
}

public static class BackgroundJobStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string RetryScheduled = "RetryScheduled";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public sealed class BackgroundJob
{
    private BackgroundJob()
    {
    }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public string Status { get; private set; } = BackgroundJobStatuses.Queued;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public Guid? OrganizationId { get; private set; }
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? NextRetryAt { get; private set; }
    public int AttemptCount { get; private set; }
    public int MaxAttempts { get; private set; }
    public int Version { get; private set; }
    public string? LeaseOwner { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorDetails { get; private set; }

    public static BackgroundJob Create(
        string type,
        string payloadJson,
        string idempotencyKey,
        string correlationId,
        Guid? organizationId,
        string? entityType,
        Guid? entityId,
        DateTimeOffset now,
        int maxAttempts)
    {
        if (type != BackgroundJobTypes.SendNotificationEmail)
            throw new ArgumentException("Unsupported background job type.", nameof(type));
        if (string.IsNullOrWhiteSpace(payloadJson) || payloadJson.Length > 2000)
            throw new ArgumentException("A valid job payload is required.", nameof(payloadJson));
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            throw new ArgumentException("A valid idempotency key is required.", nameof(idempotencyKey));
        if (maxAttempts is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        return new BackgroundJob
        {
            Id = Guid.NewGuid(),
            Type = type,
            PayloadJson = payloadJson,
            Status = BackgroundJobStatuses.Queued,
            IdempotencyKey = idempotencyKey,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId)
                ? Guid.NewGuid().ToString("N")
                : correlationId[..Math.Min(correlationId.Length, 100)],
            OrganizationId = organizationId,
            EntityType = entityType,
            EntityId = entityId,
            CreatedAt = now,
            ScheduledAt = now,
            MaxAttempts = maxAttempts
        };
    }

    public void MarkStarted(string leaseOwner, DateTimeOffset now, DateTimeOffset leaseExpiresAt)
    {
        Status = BackgroundJobStatuses.Running;
        StartedAt ??= now;
        LastAttemptAt = now;
        LeaseOwner = leaseOwner;
        LeaseExpiresAt = leaseExpiresAt;
        NextRetryAt = null;
        AttemptCount++;
        ErrorCode = null;
        ErrorDetails = null;
        Version++;
    }

    public void MarkSucceeded(DateTimeOffset now)
    {
        Status = BackgroundJobStatuses.Succeeded;
        CompletedAt = now;
        ClearLease();
        Version++;
    }

    public void ScheduleRetry(DateTimeOffset nextRetryAt, string errorCode, string errorDetails)
    {
        Status = BackgroundJobStatuses.RetryScheduled;
        ScheduledAt = nextRetryAt;
        NextRetryAt = nextRetryAt;
        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
        ClearLease();
        Version++;
    }

    public void MarkFailed(DateTimeOffset now, string errorCode, string errorDetails)
    {
        Status = BackgroundJobStatuses.Failed;
        CompletedAt = now;
        NextRetryAt = null;
        ErrorCode = errorCode;
        ErrorDetails = errorDetails;
        ClearLease();
        Version++;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status is not (BackgroundJobStatuses.Queued or BackgroundJobStatuses.RetryScheduled))
            throw new InvalidOperationException("Only queued or retry-scheduled jobs can be cancelled.");

        Status = BackgroundJobStatuses.Cancelled;
        CompletedAt = now;
        Version++;
    }

    public void Retry(DateTimeOffset now)
    {
        if (Status != BackgroundJobStatuses.Failed)
            throw new InvalidOperationException("Only failed jobs can be retried.");
        if (AttemptCount >= MaxAttempts && MaxAttempts >= 20)
            throw new InvalidOperationException("The job has reached its maximum attempt limit.");

        Status = BackgroundJobStatuses.Queued;
        ScheduledAt = now;
        NextRetryAt = null;
        CompletedAt = null;
        if (AttemptCount >= MaxAttempts && MaxAttempts < 20)
            MaxAttempts++;
        ErrorCode = null;
        ErrorDetails = null;
        Version++;
    }

    private void ClearLease()
    {
        LeaseOwner = null;
        LeaseExpiresAt = null;
    }
}
