using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public sealed class CreateCaseRequest
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(5000, MinimumLength = 10)]
    public string Description { get; init; } = string.Empty;
}