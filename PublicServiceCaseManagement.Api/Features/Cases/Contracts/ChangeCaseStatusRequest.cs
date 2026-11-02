using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Cases.Contracts;

/// <summary>Requests a valid case status transition.</summary>
public sealed class ChangeCaseStatusRequest
{
    /// <summary>Target status value.</summary>
    [EnumDataType(typeof(CaseStatus))]
    public CaseStatus NewStatus { get; init; }

    /// <summary>Optional explanation, bounded by the case-history limit.</summary>
    [TrimmedStringLength(CaseStatusHistory.MaximumExplanationLength)]
    public string? Explanation { get; init; }

    /// <summary>
    /// When true the explanation is included in the citizen-visible timeline.
    /// The transition itself is always visible; the note stays internal when
    /// this flag is false.
    /// </summary>
    public bool ExplanationVisibleToCitizen { get; init; }
}