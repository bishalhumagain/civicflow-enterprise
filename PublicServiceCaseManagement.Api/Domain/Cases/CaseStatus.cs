namespace PublicServiceCaseManagement.Api.Domain.Cases;

public enum CaseStatus
{
    Submitted,
    UnderReview,
    Assigned,
    InProgress,
    AwaitingInformation,
    Resolved,
    Closed,
    Rejected
}