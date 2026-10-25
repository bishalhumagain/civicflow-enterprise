using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record AssignCaseRequest(
    [NotEmptyGuid]
    Guid StaffMemberId,
    [EnumDataType(typeof(CaseAssignmentRole))]
    CaseAssignmentRole Role = CaseAssignmentRole.CaseWorker);