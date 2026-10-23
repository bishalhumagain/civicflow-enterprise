using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.BackgroundJobs;
using PublicServiceCaseManagement.Api.Domain.Notifications;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Notifications;

public interface INotificationOutbox
{
    Task<Guid?> EnqueueAsync(
        Guid recipientUserId,
        string type,
        string title,
        string message,
        string? entityType,
        Guid? entityId,
        string idempotencyKey,
        Guid? organizationId,
        string? correlationId,
        CancellationToken cancellationToken);
}

public sealed class NotificationOutbox(
    CivicFlowDbContext dbContext,
    IConfiguration configuration,
    TimeProvider timeProvider) : INotificationOutbox
{
    public async Task<Guid?> EnqueueAsync(
        Guid recipientUserId,
        string type,
        string title,
        string message,
        string? entityType,
        Guid? entityId,
        string idempotencyKey,
        Guid? organizationId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Notifications.AnyAsync(
                notification => notification.IdempotencyKey == idempotencyKey,
                cancellationToken) ||
            dbContext.Notifications.Local.Any(
                notification => notification.IdempotencyKey == idempotencyKey))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var retentionDays = Math.Clamp(
            configuration.GetValue("BackgroundJobs:RetentionDays", 90),
            1,
            3650);
        var notification = Notification.Create(
            recipientUserId,
            type,
            title,
            message,
            entityType,
            entityId,
            idempotencyKey,
            now,
            now.AddDays(retentionDays));
        var payload = JsonSerializer.Serialize(
            new NotificationEmailPayload(notification.Id));
        var job = BackgroundJob.Create(
            BackgroundJobTypes.SendNotificationEmail,
            payload,
            $"email:{idempotencyKey}",
            correlationId ?? Guid.NewGuid().ToString("N"),
            organizationId,
            entityType,
            entityId,
            now,
            Math.Clamp(
                configuration.GetValue("BackgroundJobs:MaxAttempts", 5),
                1,
                20));

        notification.AssociateJob(job.Id);
        dbContext.Notifications.Add(notification);
        dbContext.BackgroundJobs.Add(job);
        return notification.Id;
    }

    public sealed record NotificationEmailPayload(Guid NotificationId);
}
