using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed record UpdateCaseRequest(
    [param: Required]
    [param: StringLength(200, MinimumLength = 3)]
    string Title,
    [param: Required]
    [param: StringLength(5000, MinimumLength = 10)]
    string Description);

public sealed record ConfirmCaseDeletionRequest(bool Confirmed);
