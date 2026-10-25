using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

public sealed record CreateOrganizationRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string Name,
    [Required]
    [StringLength(50, MinimumLength = 1)]
    string Code,
    Guid? OrganizationTypeId = null);
