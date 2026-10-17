using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record ChangeCaseStatusResponse(
    Guid Id,
    string ReferenceNumber,
    CaseStatus Status,
    DateTimeOffset UpdatedAt);