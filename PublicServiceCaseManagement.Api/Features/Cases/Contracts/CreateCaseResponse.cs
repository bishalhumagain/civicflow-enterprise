using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases.Contracts;

public sealed record CreateCaseResponse(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    CaseStatus Status,
    CasePriority Priority,
    DateTimeOffset CreatedAt,
    Guid? AssignedToStaffMemberId = null,
    Domain.Cases.CaseAssignmentRole? AssignedRole = null);