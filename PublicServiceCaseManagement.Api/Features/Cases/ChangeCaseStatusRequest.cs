using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class ChangeCaseStatusRequest
{
    [EnumDataType(typeof(CaseStatus))]
    public CaseStatus NewStatus { get; init; }

    [StringLength(CaseStatusHistory.MaximumExplanationLength)]
    public string? Explanation { get; init; }

    /// <summary>
    /// When true the explanation is included in the citizen-visible timeline.
    /// The transition itself is always visible; the note stays internal when
    /// this flag is false.
    /// </summary>
    public bool ExplanationVisibleToCitizen { get; init; }
}
