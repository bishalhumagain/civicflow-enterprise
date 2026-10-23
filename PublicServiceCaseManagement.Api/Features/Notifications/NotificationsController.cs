using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Notifications;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(
    CivicFlowDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationPageResponse>> GetPage(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? unreadOnly = null,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            return BadRequest(new { error = "Pagination values are out of range." });

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var now = timeProvider.GetUtcNow();
        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(notification =>
                notification.RecipientUserId == user.Id &&
                (notification.ExpiresAt == null || notification.ExpiresAt > now));
        var unreadCount = await query.CountAsync(
            notification => !notification.IsRead,
            cancellationToken);

        if (unreadOnly == true)
            query = query.Where(notification => !notification.IsRead);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.RelatedEntityType,
                notification.RelatedEntityId,
                notification.IsRead,
                notification.ReadAt,
                notification.CreatedAt,
                notification.DeliveryStatus,
                notification.DeliveryErrorCode))
            .ToListAsync(cancellationToken);

        return Ok(new NotificationPageResponse(
            items,
            page,
            pageSize,
            totalCount,
            unreadCount));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadNotificationCountResponse>> GetUnreadCount(
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var now = timeProvider.GetUtcNow();
        var count = await dbContext.Notifications
            .CountAsync(
                notification =>
                    notification.RecipientUserId == user.Id &&
                    !notification.IsRead &&
                    (notification.ExpiresAt == null || notification.ExpiresAt > now),
                cancellationToken);
        return Ok(new UnreadNotificationCountResponse(count));
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(
                item => item.Id == id && item.RecipientUserId == user.Id,
                cancellationToken);
        if (notification is null)
            return NotFound();

        notification.MarkRead(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("read")]
    public async Task<ActionResult<MarkNotificationsReadResponse>> MarkAllRead(
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var now = timeProvider.GetUtcNow();
        var query = dbContext.Notifications
            .Where(item =>
                item.RecipientUserId == user.Id &&
                !item.IsRead &&
                (item.ExpiresAt == null || item.ExpiresAt > now));
        if (dbContext.Database.IsNpgsql())
        {
            var updatedCount = await query.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsRead, true)
                    .SetProperty(item => item.ReadAt, now),
                cancellationToken);
            return Ok(new MarkNotificationsReadResponse(updatedCount));
        }

        var notifications = await query
            .ToListAsync(cancellationToken);
        foreach (var notification in notifications)
            notification.MarkRead(now);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new MarkNotificationsReadResponse(notifications.Count));
    }
}

public sealed record NotificationResponse(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? RelatedEntityType,
    Guid? RelatedEntityId,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt,
    string DeliveryStatus,
    string? DeliveryErrorCode);

public sealed record NotificationPageResponse(
    IReadOnlyList<NotificationResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnreadCount);

public sealed record UnreadNotificationCountResponse(int Count);
public sealed record MarkNotificationsReadResponse(int UpdatedCount);
