using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.BackgroundJobs;
using PublicServiceCaseManagement.Api.Domain.Notifications;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Api.Features.Notifications;
using PublicServiceCaseManagement.Api.Features.Notifications.Services;

namespace PublicServiceCaseManagement.Api.Features.BackgroundJobs.Services;

public sealed class BackgroundJobProcessor(
    CivicFlowDbContext dbContext,
    IEmailSender emailSender,
    IConfiguration configuration,
    TimeProvider timeProvider,
    ILogger<BackgroundJobProcessor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public async Task<bool> ProcessOneAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return false;

        var now = timeProvider.GetUtcNow();
        await RecoverExpiredLeasesAsync(now, cancellationToken);

        var leaseDuration = TimeSpan.FromSeconds(Math.Clamp(
            configuration.GetValue("BackgroundJobs:LeaseSeconds", 300),
            30,
            3600));
        var jobId = await dbContext.Database.SqlQuery<Guid>($"""
            WITH candidate AS (
                SELECT "Id"
                FROM background_jobs
                WHERE "Status" IN ('Queued', 'RetryScheduled')
                  AND "ScheduledAt" <= {now}
                  AND "AttemptCount" < "MaxAttempts"
                ORDER BY "ScheduledAt", "CreatedAt"
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            UPDATE background_jobs AS job
            SET "Status" = 'Running',
                "AttemptCount" = job."AttemptCount" + 1,
                "Version" = job."Version" + 1,
                "StartedAt" = COALESCE(job."StartedAt", {now}),
                "LastAttemptAt" = {now},
                "LeaseOwner" = {leaseOwner},
                "LeaseExpiresAt" = {now + leaseDuration},
                "NextRetryAt" = NULL,
                "ErrorCode" = NULL,
                "ErrorDetails" = NULL
            FROM candidate
            WHERE job."Id" = candidate."Id"
            RETURNING job."Id" AS "Value"
            """).FirstOrDefaultAsync(cancellationToken);

        if (jobId == Guid.Empty)
            return false;

        var job = await dbContext.BackgroundJobs
            .SingleAsync(item => item.Id == jobId, cancellationToken);
        var attempt = BackgroundJobAttempt.Start(
            job.Id,
            job.AttemptCount,
            now);
        dbContext.BackgroundJobAttempts.Add(attempt);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (job.Type != BackgroundJobTypes.SendNotificationEmail)
        {
            await FailAsync(
                job,
                attempt,
                null,
                "unsupported_job_type",
                "The job type is not supported.",
                permanent: true,
                cancellationToken);
            return true;
        }

        NotificationOutbox.NotificationEmailPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<NotificationOutbox.NotificationEmailPayload>(
                job.PayloadJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            await FailAsync(
                job,
                attempt,
                null,
                "invalid_job_payload",
                "The job payload is invalid.",
                permanent: true,
                cancellationToken);
            return true;
        }

        if (payload is null || payload.NotificationId == Guid.Empty)
        {
            await FailAsync(
                job,
                attempt,
                null,
                "invalid_job_payload",
                "The job payload is invalid.",
                permanent: true,
                cancellationToken);
            return true;
        }

        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(
                item => item.Id == payload.NotificationId,
                cancellationToken);
        var recipient = notification is null
            ? null
            : await dbContext.ApplicationUsers
                .AsNoTracking()
                .Where(user => user.Id == notification.RecipientUserId &&
                               user.IsActive)
                .Select(user => user.Email)
                .SingleOrDefaultAsync(cancellationToken);
        if (notification is null || string.IsNullOrWhiteSpace(recipient))
        {
            await FailAsync(
                job,
                attempt,
                notification,
                "notification_unavailable",
                "The notification recipient is unavailable.",
                permanent: true,
                cancellationToken);
            return true;
        }

        try
        {
            await emailSender.SendAsync(
                recipient,
                notification.Title,
                notification.Message,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            await FailAsync(
                job,
                attempt,
                notification,
                "email_delivery_failed",
                "Email delivery failed; the system will retry.",
                permanent: false,
                cancellationToken);
            return true;
        }

        var completedAt = timeProvider.GetUtcNow();
        job.MarkSucceeded(completedAt);
        attempt.Complete(completedAt);
        notification.MarkEmailSent();
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Background job {JobId} of type {JobType} succeeded. CorrelationId {CorrelationId}.",
            job.Id,
            job.Type,
            job.CorrelationId);
        return true;
    }

    private async Task RecoverExpiredLeasesAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE background_job_attempts AS attempt
            SET "Status" = 'Failed',
                "CompletedAt" = {now},
                "ErrorCode" = 'worker_lease_expired',
                "ErrorDetails" = 'Worker lease expired before completion.'
            FROM background_jobs AS job
            WHERE attempt."JobId" = job."Id"
              AND attempt."Status" = 'Running'
              AND job."Status" = 'Running'
              AND job."LeaseExpiresAt" <= {now}
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE background_jobs
            SET "Status" = CASE
                    WHEN "AttemptCount" >= "MaxAttempts" THEN 'Failed'
                    ELSE 'RetryScheduled'
                END,
                "ScheduledAt" = {now},
                "NextRetryAt" = CASE
                    WHEN "AttemptCount" >= "MaxAttempts" THEN NULL
                    ELSE {now}
                END,
                "CompletedAt" = CASE
                    WHEN "AttemptCount" >= "MaxAttempts" THEN {now}
                    ELSE NULL
                END,
                "ErrorCode" = 'worker_lease_expired',
                "ErrorDetails" = 'Worker lease expired before completion.',
                "LeaseOwner" = NULL,
                "LeaseExpiresAt" = NULL,
                "Version" = "Version" + 1
            WHERE "Status" = 'Running'
              AND "LeaseExpiresAt" <= {now}
            """, cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE notifications AS notification
            SET "DeliveryStatus" = 'Failed',
                "DeliveryErrorCode" = 'worker_lease_expired',
                "DeliveryErrorDetails" = 'Email delivery could not be completed.'
            FROM background_jobs AS job
            WHERE notification."BackgroundJobId" = job."Id"
              AND job."Status" = 'Failed'
              AND job."ErrorCode" = 'worker_lease_expired'
              AND job."CompletedAt" = {now}
            """, cancellationToken);
    }

    public async Task PruneExpiredAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsNpgsql())
            return;

        var retentionDays = Math.Clamp(
            configuration.GetValue("BackgroundJobs:RetentionDays", 90),
            1,
            3650);
        var now = timeProvider.GetUtcNow();
        await dbContext.Notifications
            .Where(notification =>
                notification.ExpiresAt != null &&
                notification.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
        await dbContext.BackgroundJobs
            .Where(job =>
                job.CompletedAt != null &&
                job.CompletedAt <= now.AddDays(-retentionDays) &&
                (job.Status == BackgroundJobStatuses.Succeeded ||
                 job.Status == BackgroundJobStatuses.Failed ||
                 job.Status == BackgroundJobStatuses.Cancelled))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task FailAsync(
        BackgroundJob job,
        BackgroundJobAttempt attempt,
        Notification? notification,
        string errorCode,
        string errorDetails,
        bool permanent,
        CancellationToken cancellationToken)
    {
        var completedAt = timeProvider.GetUtcNow();
        if (!permanent && job.AttemptCount < job.MaxAttempts)
        {
            var delay = GetRetryDelay(job.AttemptCount, configuration);
            job.ScheduleRetry(completedAt + delay, errorCode, errorDetails);
        }
        else
        {
            job.MarkFailed(completedAt, errorCode, errorDetails);
            notification?.MarkEmailFailed(errorCode, errorDetails);
        }

        attempt.Fail(completedAt, errorCode, errorDetails);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogWarning(
            "Background job {JobId} of type {JobType} failed with {ErrorCode}. CorrelationId {CorrelationId}.",
            job.Id,
            job.Type,
            errorCode,
            job.CorrelationId);
    }

    public static TimeSpan GetRetryDelay(int attemptCount, IConfiguration configuration)
    {
        var baseSeconds = Math.Clamp(
            configuration.GetValue("BackgroundJobs:RetryBaseSeconds", 30),
            1,
            3600);
        var maxSeconds = Math.Clamp(
            configuration.GetValue("BackgroundJobs:RetryMaxSeconds", 3600),
            baseSeconds,
            86400);
        var exponent = Math.Clamp(attemptCount - 1, 0, 20);
        var exponentialSeconds = Math.Min(
            maxSeconds,
            baseSeconds * Math.Pow(2, exponent));
        var jitter = 0.75 + (Random.Shared.NextDouble() * 0.5);
        return TimeSpan.FromSeconds(exponentialSeconds * jitter);
    }
}
