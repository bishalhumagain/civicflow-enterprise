using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class CreateCaseRequest
{
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(
        Case.MaximumTitleLength,
        MinimumLength = Case.MinimumTitleLength)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(
        Case.MaximumDescriptionLength,
        MinimumLength = Case.MinimumDescriptionLength)]
    public string Description { get; init; } = string.Empty;
}