using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Staff.Contracts;

public sealed record CreateStaffMemberRequest(
    [Required]
    [StringLength(150, MinimumLength = 1)]
    string FullName,
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Phone][StringLength(30)] string? PhoneNumber);

public sealed record UpdateStaffPhoneRequest(
    [Phone][StringLength(30)] string? PhoneNumber);