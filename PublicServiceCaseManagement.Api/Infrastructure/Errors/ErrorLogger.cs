using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PublicServiceCaseManagement.Api.Domain.Errors;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

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
            // A prior SaveChanges on the same scoped context (for example an
            // RLS rejection inside the invitation accept flow) leaves the
            // change tracker holding the entities that just failed. Retrying
            // SaveChanges here would replay those writes and throw again, so
            // we detach everything tracked and let the ApplicationError row
            // be the only pending change.
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
            // If error logging fails, we don't want to cause an infinite loop.
            // Record the original exception and the logging failure through
            // the standard logger so operators can see what happened.
            logger.LogError(
                loggingException,
                "Failed to persist application error {OriginalMessage}",
                exception.Message);
        }
    }

    public async Task<List<ApplicationError>> GetErrorsAsync(
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

        return await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
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