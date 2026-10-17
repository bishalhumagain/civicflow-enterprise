using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class ChangeCaseStatusRequest
{
    [EnumDataType(typeof(CaseStatus))]
    public CaseStatus NewStatus { get; init; }
}