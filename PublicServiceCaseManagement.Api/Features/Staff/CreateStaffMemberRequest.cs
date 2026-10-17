using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Staff;

public sealed record CreateStaffMemberRequest(
    [Required]
    [StringLength(150, MinimumLength = 1)]
    string FullName,

    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email);