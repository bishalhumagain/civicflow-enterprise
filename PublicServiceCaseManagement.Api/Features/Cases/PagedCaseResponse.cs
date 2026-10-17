namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record PagedCasesResponse(
    IReadOnlyList<CreateCaseResponse> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);