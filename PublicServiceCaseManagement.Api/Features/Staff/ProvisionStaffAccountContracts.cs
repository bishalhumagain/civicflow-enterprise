using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Staff;

public sealed record ProvisionStaffAccountRequest(
    [Required]
    [StringLength(150, MinimumLength = 1)]
    string FullName,
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required]
    [StringLength(128, MinimumLength = 12)]
    string TemporaryPassword,
    [Required]
    string Role);

public sealed record ProvisionStaffAccountResponse(
    Guid UserId,
    Guid MembershipId,
    Guid StaffMemberId,
    string FullName,
    string Email,
    string Role,
    OrganizationRole OrganizationRole);
