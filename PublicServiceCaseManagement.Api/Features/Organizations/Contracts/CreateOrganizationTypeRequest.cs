using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

public sealed record CreateOrganizationTypeRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string Name,
    [Required]
    [StringLength(50, MinimumLength = 1)]
    string Code);
