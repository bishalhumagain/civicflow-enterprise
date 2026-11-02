namespace PublicServiceCaseManagement.Api.Infrastructure.Pagination;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
