using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases.Contracts;

public sealed record ChangeCaseStatusResponse(
    Guid Id,
    string ReferenceNumber,
    CaseStatus Status,
    DateTimeOffset UpdatedAt);