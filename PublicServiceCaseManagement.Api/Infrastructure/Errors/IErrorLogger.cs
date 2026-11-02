using PublicServiceCaseManagement.Api.Domain.Errors;
using PublicServiceCaseManagement.Api.Infrastructure.Pagination;

namespace PublicServiceCaseManagement.Api.Infrastructure.Errors;

public interface IErrorLogger
{
    Task LogErrorAsync(Exception exception, Guid? userId = null, Guid? organizationId = null,
        string? additionalData = null);

    Task<PagedResponse<ApplicationError>> GetErrorsAsync(
        bool? isHandled = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken cancellationToken = default);

    Task<ApplicationError?> GetErrorAsync(
        Guid errorId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsHandledAsync(
        Guid errorId,
        CancellationToken cancellationToken = default);
}