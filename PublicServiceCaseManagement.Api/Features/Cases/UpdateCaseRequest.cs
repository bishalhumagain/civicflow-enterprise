using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record UpdateCaseRequest(
    [param: Required]
    [param: NotWhiteSpace]
    [param: TrimmedStringLength(
        Case.MaximumTitleLength,
        MinimumLength = Case.MinimumTitleLength)]
    string Title,
    [param: Required]
    [param: NotWhiteSpace]
    [param: TrimmedStringLength(
        Case.MaximumDescriptionLength,
        MinimumLength = Case.MinimumDescriptionLength)]
    string Description);

public sealed record ConfirmCaseDeletionRequest(bool Confirmed);
