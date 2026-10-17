using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record CreateCaseResponse(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    CaseStatus Status,
    CasePriority Priority,
    DateTimeOffset CreatedAt);