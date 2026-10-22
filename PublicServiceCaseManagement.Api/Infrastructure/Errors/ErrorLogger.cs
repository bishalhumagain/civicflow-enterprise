using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Errors;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Infrastructure.Errors;

public sealed class ErrorLogger(CivicFlowDbContext dbContext) : IErrorLogger
{
    public async Task LogErrorAsync(Exception exception, Guid? userId = null, Guid? organizationId = null, string? additionalData = null)
    {
        try
        {
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
        catch
        {
            // If error logging fails, we don't want to cause an infinite loop
            // Log to console as fallback
            Console.WriteLine($"Failed to log error to database: {exception.Message}");
        }
    }

    public async Task<List<ApplicationError>> GetErrorsAsync(bool? isHandled = null, int page = 1, int pageSize = 25)
    {
        var query = dbContext.ApplicationErrors.AsQueryable();

        if (isHandled.HasValue)
        {
            query = query.Where(e => e.IsHandled == isHandled.Value);
        }

        return await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<ApplicationError?> GetErrorAsync(Guid errorId)
    {
        return await dbContext.ApplicationErrors
            .FirstOrDefaultAsync(e => e.Id == errorId);
    }

    public async Task MarkAsHandledAsync(Guid errorId)
    {
        var error = await dbContext.ApplicationErrors
            .FirstOrDefaultAsync(e => e.Id == errorId);

        if (error != null)
        {
            error.IsHandled = true;
            await dbContext.SaveChangesAsync();
        }
    }
}
