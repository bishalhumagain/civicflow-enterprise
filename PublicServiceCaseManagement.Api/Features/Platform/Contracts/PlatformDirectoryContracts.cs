using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Platform.Contracts;

/// <summary>
/// The organization a user belongs to, as seen by a platform administrator.
/// </summary>
public sealed record PlatformUserOrganization(
    Guid MembershipId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationCode,
    string Role,
    bool IsActive);

/// <summary>
/// Everything a platform administrator can see about one account.
/// </summary>
public sealed record PlatformUserSummary(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool EmailConfirmed,
    bool IsActive,
    bool IsCitizen,
    bool IsStaff,
    string PlatformRole,
    IReadOnlyList<string> Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<PlatformUserOrganization> Organizations);

/// <summary>
/// A staff member of an organization, as seen by a platform administrator.
/// </summary>
public sealed record PlatformStaffSummary(
    Guid UserId,
    Guid MembershipId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationCode,
    string FullName,
    string Email,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    bool EmailConfirmed,
    bool AccountIsActive,
    DateTimeOffset MembershipCreatedAt);

/// <summary>
/// The account fields a platform administrator may maintain for a staff
/// account. Organization roles and memberships are managed separately in the
/// organization context.
/// </summary>
public sealed record UpdatePlatformStaffRequest(
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(150, MinimumLength = 1)]
    string FullName,
    [PhoneNumber]
    string? PhoneNumber);

/// <summary>
/// A compact platform overview for the administration dashboard.
/// </summary>
public sealed record PlatformOverviewResponse(
    int UserCount,
    int ActiveUserCount,
    int StaffCount,
    int ActiveStaffCount,
    int OrganizationCount,
    int ActiveOrganizationCount,
    int InvitationCount,
    int PendingInvitationCount);
