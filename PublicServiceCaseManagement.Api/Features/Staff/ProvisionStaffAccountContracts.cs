using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Staff;

public sealed record ProvisionStaffAccountRequest(
    [property: Required]
    [property: StringLength(150, MinimumLength = 1)]
    string FullName,
    [property: Required]
    [property: EmailAddress]
    [property: StringLength(254)]
    string Email,
    [property: Required]
    [property: StringLength(128, MinimumLength = 12)]
    string TemporaryPassword,
    [property: Required]
    string Role);

public sealed record ProvisionStaffAccountResponse(
    Guid UserId,
    Guid MembershipId,
    Guid StaffMemberId,
    string FullName,
    string Email,
    string Role,
    OrganizationRole OrganizationRole);
