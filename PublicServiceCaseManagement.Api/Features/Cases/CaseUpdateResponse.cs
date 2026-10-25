using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record CaseUpdateResponse(
    Guid Id,
    Guid CaseId,
    CaseUpdateKind Kind,
    string Message,
    bool IsVisibleToCitizen,
    Guid AuthorUserId,
    DateTimeOffset CreatedAt);
