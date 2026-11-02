using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

/// <summary>Creates an organization using the platform administration API.</summary>
/// <param name="Name">Required organization name, 1–200 characters.</param>
/// <param name="Code">Required organization code, 1–50 characters.</param>
/// <param name="OrganizationTypeId">Optional active organization-type ID; omitted values use the Uncategorized type.</param>
public sealed record CreateOrganizationRequest(
    [Required]
    [StringLength(200, MinimumLength = 1)]
    string Name,
    [Required]
    [StringLength(50, MinimumLength = 1)]
    string Code,
    Guid? OrganizationTypeId = null);