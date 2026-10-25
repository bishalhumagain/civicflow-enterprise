using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases.Contracts;

public sealed class ChangeCasePriorityRequest
{
    [EnumDataType(typeof(CasePriority))]
    public CasePriority NewPriority { get; init; }
}
