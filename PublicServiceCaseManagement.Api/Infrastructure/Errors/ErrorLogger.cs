using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PublicServiceCaseManagement.Api.Domain.Errors;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using PublicServiceCaseManagement.Api.Infrastructure.Pagination;

namespace PublicServiceCaseManagement.Api.Infrastructure.Errors;

public sealed class ErrorLogger(
    CivicFlowDbContext dbContext,
    ILogger<ErrorLogger> logger) : IErrorLogger
{
    public async Task LogErrorAsync(Exception exception, Guid? userId = null, Guid? organizationId = null,
        string? additionalData = null)
    {
        try
        {
            // Detach failed writes so the error row can be saved without replaying them.
            foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
            {
                entry.State = EntityState.Detached;
            }

            var error = new ApplicationError
            {
                Id = Guid.NewGuid(),
                Message = exception.Message,
                StackTrace = exception.StackTrace ?? string.Empty,
                Source = exception.Source,
                MethodName = exception.TargetSite?.Name,
                AdditionalData = additionalData,
                UserId = userId,
                OrganizationId = organizationId,
                IsHandled = false,
                OccurredAt = DateTime.UtcNow
            };

            dbContext.ApplicationErrors.Add(error);
            await dbContext.SaveChangesAsync();
        }
        catch (Exception loggingException)
        {
            logger.LogError(
                loggingException,
                "Failed to persist application error {OriginalMessage}",
                exception.Message);
        }
    }

    public async Task<PagedResponse<ApplicationError>> GetErrorsAsync(
        bool? isHandled = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ApplicationErrors.AsQueryable();

        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 200);

        if (isHandled.HasValue)
        {
            query = query.Where(e => e.IsHandled == isHandled.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResponse<ApplicationError>(
            items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize));
    }

    public async Task<ApplicationError?> GetErrorAsync(
        Guid errorId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ApplicationErrors
            .FirstOrDefaultAsync(
                e => e.Id == errorId,
                cancellationToken);
    }

    public async Task<bool> MarkAsHandledAsync(
        Guid errorId,
        CancellationToken cancellationToken = default)
    {
        var error = await dbContext.ApplicationErrors
            .FirstOrDefaultAsync(
                e => e.Id == errorId,
                cancellationToken);

        if (error is null)
            return false;

        error.IsHandled = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}