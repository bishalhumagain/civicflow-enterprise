using PublicServiceCaseManagement.Api.Domain.Errors;

namespace PublicServiceCaseManagement.Api.Infrastructure.Errors;

public interface IErrorLogger
{
    Task LogErrorAsync(Exception exception, Guid? userId = null, Guid? organizationId = null, string? additionalData = null);
    Task<List<ApplicationError>> GetErrorsAsync(bool? isHandled = null, int page = 1, int pageSize = 25);
    Task<ApplicationError?> GetErrorAsync(Guid errorId);
    Task MarkAsHandledAsync(Guid errorId);
}
