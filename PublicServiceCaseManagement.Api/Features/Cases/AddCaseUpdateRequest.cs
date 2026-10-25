using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class AddCaseUpdateRequest
{
    [EnumDataType(typeof(CaseUpdateKind))]
    public CaseUpdateKind Kind { get; init; }

    [Required]
    [StringLength(CaseUpdate.MaximumMessageLength, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;
}
