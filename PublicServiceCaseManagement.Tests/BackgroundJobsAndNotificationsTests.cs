using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PublicServiceCaseManagement.Api.Domain.BackgroundJobs;
using PublicServiceCaseManagement.Api.Domain.Notifications;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Features.Notifications;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Xunit;

namespace PublicServiceCaseManagement.Tests;

public sealed class BackgroundJobsAndNotificationsTests
{
    [Fact]
    public async Task NotificationOutbox_PersistsInAppNotificationAndIdOnlyEmailJob()
    {
        await using var factory = new CivicFlowApiFactory(OrganizationRole.CaseWorker);
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
        var outbox = scope.ServiceProvider.GetRequiredService<INotificationOutbox>();
        var idempotencyKey = $"test:{Guid.NewGuid():N}";

        var notificationId = await outbox.EnqueueAsync(
            TestAuthenticationHandler.DefaultUserId,
            NotificationTypes.CaseSubmitted,
            "Case received",
            "Your case is awaiting review.",
            "Case",
            Guid.NewGuid(),
            idempotencyKey,
            Guid.NewGuid(),
            "test-correlation",
            CancellationToken.None);
        var duplicateId = await outbox.EnqueueAsync(
            TestAuthenticationHandler.DefaultUserId,
            NotificationTypes.CaseSubmitted,
            "Case received",
            "Your case is awaiting review.",
            "Case",
            Guid.NewGuid(),
            idempotencyKey,
            null,
            null,
            CancellationToken.None);

        Assert.NotNull(notificationId);
        Assert.Null(duplicateId);
        await dbContext.SaveChangesAsync();

        var notification = await dbContext.Notifications
            .SingleAsync(item => item.Id == notificationId);
        var job = await dbContext.BackgroundJobs
            .SingleAsync(item => item.Id == notification.BackgroundJobId);
        Assert.Equal(NotificationDeliveryStatuses.Queued, notification.DeliveryStatus);
        Assert.Contains(notification.Id.ToString(), job.PayloadJson);
        Assert.DoesNotContain("email", job.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", job.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(BackgroundJobStatuses.Queued, job.Status);
    }

    [Fact]
    public async Task Notifications_AreRecipientScopedAndCanBeMarkedRead()
    {
        await using var factory = new CivicFlowApiFactory(OrganizationRole.CaseWorker);
        using var client = factory.CreateClient();
        var own = Notification.Create(
            TestAuthenticationHandler.DefaultUserId,
            NotificationTypes.CaseSubmitted,
            "Own",
            "Own message",
            null,
            null,
            $"own:{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);
        var other = Notification.Create(
            Guid.NewGuid(),
            NotificationTypes.CaseSubmitted,
            "Other",
            "Other message",
            null,
            null,
            $"other:{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);
        var ownUnread = Notification.Create(
            TestAuthenticationHandler.DefaultUserId,
            NotificationTypes.CaseAssigned,
            "Second own item",
            "Second own message",
            null,
            null,
            $"own-unread:{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
            dbContext.Notifications.AddRange(own, other, ownUnread);
            await dbContext.SaveChangesAsync();
        }

        var page = await client.GetFromJsonAsync<JsonElement>("/api/notifications?pageSize=10");
        var items = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Contains(
            items,
            item => item.GetProperty("id").GetGuid() == own.Id);

        var forbiddenRead = await client.PatchAsync($"/api/notifications/{other.Id}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenRead.StatusCode);
        var read = await client.PatchAsync($"/api/notifications/{own.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        var bulkRead = await client.PostAsync("/api/notifications/read", null);
        Assert.Equal(HttpStatusCode.OK, bulkRead.StatusCode);
        using (var verifyScope = factory.Services.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
            var otherNotification = await verifyDb.Notifications
                .SingleAsync(item => item.Id == other.Id);
            Assert.False(otherNotification.IsRead);
        }
        var unread = await client.GetFromJsonAsync<JsonElement>("/api/notifications/unread-count");
        Assert.Equal(0, unread.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/notifications?page=0")).StatusCode);
    }

    [Fact]
    public async Task JobManagement_IsForbiddenForOrganizationStaff()
    {
        await using var factory = new CivicFlowApiFactory(OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/platform/jobs")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsync($"/api/platform/jobs/{Guid.NewGuid()}/retry", null)).StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_CanInspectAndCancelAQueuedJob()
    {
        await using var factory = new CivicFlowApiFactory(OrganizationRole.OrganizationAdmin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Role",
            TestAuthenticationHandler.SuperAdminRole);
        var job = BackgroundJob.Create(
            BackgroundJobTypes.SendNotificationEmail,
            """{"notificationId":"00000000-0000-0000-0000-000000000001"}""",
            $"email:{Guid.NewGuid():N}",
            "admin-test",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            maxAttempts: 3);
        var failedJob = BackgroundJob.Create(
            BackgroundJobTypes.SendNotificationEmail,
            """{"notificationId":"00000000-0000-0000-0000-000000000002"}""",
            $"email:{Guid.NewGuid():N}",
            "admin-test",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            maxAttempts: 3);
        failedJob.MarkStarted("test-worker", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(1));
        failedJob.MarkFailed(DateTimeOffset.UtcNow, "delivery_failed", "Sanitized.");
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
            dbContext.BackgroundJobs.AddRange(job, failedJob);
            await dbContext.SaveChangesAsync();
        }

        var page = await client.GetFromJsonAsync<JsonElement>("/api/platform/jobs");
        Assert.Contains(
            page.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == job.Id);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsync($"/api/platform/jobs/{job.Id}/cancel", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsync($"/api/platform/jobs/{failedJob.Id}/retry", null)).StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<CivicFlowDbContext>();
        Assert.Equal(
            BackgroundJobStatuses.Cancelled,
            (await verifyDb.BackgroundJobs.SingleAsync(item => item.Id == job.Id)).Status);
        Assert.Equal(
            BackgroundJobStatuses.Queued,
            (await verifyDb.BackgroundJobs.SingleAsync(item => item.Id == failedJob.Id)).Status);
    }

    [Fact]
    public void BackgroundJob_EnforcesStateTransitionsAndMonotonicAttemptHistory()
    {
        var job = BackgroundJob.Create(
            BackgroundJobTypes.SendNotificationEmail,
            """{"notificationId":"00000000-0000-0000-0000-000000000001"}""",
            $"email:{Guid.NewGuid():N}",
            "test",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            maxAttempts: 1);
        var now = DateTimeOffset.UtcNow;
        job.MarkStarted("worker", now, now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => job.Cancel(now));
        job.MarkFailed(now.AddSeconds(1), "failed", "Sanitized.");
        job.Retry(now.AddSeconds(2));

        Assert.Equal(1, job.AttemptCount);
        Assert.Equal(2, job.MaxAttempts);
        Assert.Equal(BackgroundJobStatuses.Queued, job.Status);
        job.Cancel(now.AddSeconds(3));
        Assert.Equal(BackgroundJobStatuses.Cancelled, job.Status);
    }

    [Fact]
    public void BackgroundJob_AdminRetriesCannotExceedTwentyTotalAttempts()
    {
        var job = BackgroundJob.Create(
            BackgroundJobTypes.SendNotificationEmail,
            """{"notificationId":"00000000-0000-0000-0000-000000000001"}""",
            $"email:{Guid.NewGuid():N}",
            "test",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            maxAttempts: 20);
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            job.MarkStarted("worker", now, now.AddMinutes(1));
            job.MarkFailed(now.AddSeconds(attempt), "failed", "Sanitized.");
            if (attempt < 20)
                job.Retry(now.AddSeconds(attempt + 1));
        }

        Assert.Equal(20, job.AttemptCount);
        Assert.Equal(20, job.MaxAttempts);
        Assert.Throws<InvalidOperationException>(() => job.Retry(now.AddMinutes(1)));
    }
}
