using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

public sealed record UpdateOrganizationRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string Name,
    [Required]
    [StringLength(50, MinimumLength = 1)]
    string Code,
    Guid? OrganizationTypeId = null);
