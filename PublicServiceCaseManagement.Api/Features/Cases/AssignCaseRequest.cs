using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record AssignCaseRequest(
    Guid StaffMemberId,
    CaseAssignmentRole Role = CaseAssignmentRole.CaseWorker);