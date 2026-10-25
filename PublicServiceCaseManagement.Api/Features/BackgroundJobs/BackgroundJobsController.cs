using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.BackgroundJobs;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.BackgroundJobs;

[ApiController]
[Authorize(Policy = AppRoles.SuperAdminPolicy)]
[EnableRateLimiting("job-admin")]
[Route("api/platform/jobs")]
public sealed class BackgroundJobsController(
    CivicFlowDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IAuditLogger auditLogger,
    TimeProvider timeProvider) : ControllerBase
{
    private static readonly string[] AllowedStatuses =
    [
        BackgroundJobStatuses.Queued,
        BackgroundJobStatuses.Running,
        BackgroundJobStatuses.Succeeded,
        BackgroundJobStatuses.RetryScheduled,
        BackgroundJobStatuses.Failed,
        BackgroundJobStatuses.Cancelled
    ];

    [HttpGet]
    public async Task<ActionResult<BackgroundJobPageResponse>> GetPage(
        [FromQuery, StringLength(32)] string? status,
        [FromQuery, StringLength(100)] string? type,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery, StringLength(200)] string? correlationId,
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1_000_000 || pageSize is < 1 or > 100)
            return BadRequest(new { error = "Pagination values are out of range." });
        if (status is not null &&
            !AllowedStatuses.Contains(status, StringComparer.Ordinal))
            return BadRequest(new { error = "The job status filter is invalid." });
        if (type is not null &&
            !string.Equals(
                type,
                BackgroundJobTypes.SendNotificationEmail,
                StringComparison.Ordinal))
            return BadRequest(new { error = "The job type filter is invalid." });
        if (from.HasValue && to.HasValue && from > to)
            return BadRequest(new { error = "The start date must not be later than the end date." });
        if (correlationId?.Length > 100)
            return BadRequest(new { error = "The correlation ID filter is too long." });

        var query = dbContext.BackgroundJobs.AsNoTracking();
        if (status is not null)
            query = query.Where(job => job.Status == status);
        if (type is not null)
            query = query.Where(job => job.Type == type);
        if (from.HasValue)
            query = query.Where(job => job.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(job => job.CreatedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(correlationId))
            query = query.Where(job => job.CorrelationId == correlationId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(job => job.CreatedAt)
            .ThenByDescending(job => job.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(job => new BackgroundJobSummaryResponse(
                job.Id,
                job.Type,
                job.Status,
                job.CorrelationId,
                job.EntityType,
                job.EntityId,
                job.CreatedAt,
                job.ScheduledAt,
                job.StartedAt,
                job.LastAttemptAt,
                job.CompletedAt,
                job.NextRetryAt,
                job.AttemptCount,
                job.MaxAttempts,
                job.ErrorCode,
                job.ErrorDetails))
            .ToListAsync(cancellationToken);

        return Ok(new BackgroundJobPageResponse(
            items,
            page,
            pageSize,
            totalCount));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BackgroundJobDetailsResponse>> GetDetails(
        Guid id,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.BackgroundJobs
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return NotFound();

        var attempts = await dbContext.BackgroundJobAttempts
            .AsNoTracking()
            .Where(attempt => attempt.JobId == id)
            .OrderBy(attempt => attempt.AttemptNumber)
            .Select(attempt => new BackgroundJobAttemptResponse(
                attempt.AttemptNumber,
                attempt.StartedAt,
                attempt.CompletedAt,
                attempt.Status,
                attempt.ErrorCode,
                attempt.ErrorDetails))
            .ToListAsync(cancellationToken);

        return Ok(new BackgroundJobDetailsResponse(
            new BackgroundJobSummaryResponse(
                job.Id,
                job.Type,
                job.Status,
                job.CorrelationId,
                job.EntityType,
                job.EntityId,
                job.CreatedAt,
                job.ScheduledAt,
                job.StartedAt,
                job.LastAttemptAt,
                job.CompletedAt,
                job.NextRetryAt,
                job.AttemptCount,
                job.MaxAttempts,
                job.ErrorCode,
                job.ErrorDetails),
            attempts));
    }

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(
        Guid id,
        CancellationToken cancellationToken) =>
        ChangeJobAsync(id, retry: false, cancellationToken);

    [HttpPost("{id:guid}/retry")]
    public Task<IActionResult> Retry(
        Guid id,
        CancellationToken cancellationToken) =>
        ChangeJobAsync(id, retry: true, cancellationToken);

    private async Task<IActionResult> ChangeJobAsync(
        Guid id,
        bool retry,
        CancellationToken cancellationToken)
    {
        var job = await dbContext.BackgroundJobs
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (job is null)
            return NotFound();

        try
        {
            if (retry)
                job.Retry(timeProvider.GetUtcNow());
            else
                job.Cancel(timeProvider.GetUtcNow());
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "The job changed while this operation was being applied." });
        }
        var actor = await userManager.GetUserAsync(User);
        if (actor is not null)
        {
            await auditLogger.AccountLifecycleAsync(
                actor.Email ?? "unknown",
                actor.Id,
                retry ? "BackgroundJobRetried" : "BackgroundJobCancelled",
                job.OrganizationId,
                cancellationToken);
        }

        return NoContent();
    }
}

public sealed record BackgroundJobSummaryResponse(
    Guid Id,
    string Type,
    string Status,
    string CorrelationId,
    string? EntityType,
    Guid? EntityId,
    DateTimeOffset CreatedAt,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? NextRetryAt,
    int AttemptCount,
    int MaxAttempts,
    string? ErrorCode,
    string? ErrorDetails);

public sealed record BackgroundJobAttemptResponse(
    int AttemptNumber,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    string? ErrorCode,
    string? ErrorDetails);

public sealed record BackgroundJobPageResponse(
    IReadOnlyList<BackgroundJobSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record BackgroundJobDetailsResponse(
    BackgroundJobSummaryResponse Job,
    IReadOnlyList<BackgroundJobAttemptResponse> Attempts);
