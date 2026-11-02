using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Cases.Contracts;

/// <summary>
/// Creates a case in the authenticated staff member's active organization.
/// The organization and creator are derived by the API, not supplied here.
/// </summary>
public sealed class CreateCaseRequest
{
    /// <summary>Required trimmed title within the domain's case-title limit.</summary>
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(
        Case.MaximumTitleLength,
        MinimumLength = Case.MinimumTitleLength)]
    public string Title { get; init; } = string.Empty;

    /// <summary>Required trimmed description within the domain's case-description limit.</summary>
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(
        Case.MaximumDescriptionLength,
        MinimumLength = Case.MinimumDescriptionLength)]
    public string Description { get; init; } = string.Empty;
}