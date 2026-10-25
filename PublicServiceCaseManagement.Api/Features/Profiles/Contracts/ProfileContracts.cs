using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Profiles.Contracts;

public static class ProfileRoutes
{
    public const string Base = "api/profile";
}

/// <summary>
/// The kind of account a self-service profile belongs to. Citizens keep the
/// civil registry details on their citizen profile, while organization staff
/// keep their contact details on the staff record of their organization.
/// </summary>
public enum ProfileAccountType
{
    Citizen,
    Staff
}

/// <summary>
/// The profile of the account that is currently signed in. Only the fields the
/// account owner is allowed to maintain are exposed; organization, role and
/// activation state stay under administrator control.
/// </summary>
public sealed record ProfileResponse(
    Guid UserId,
    string FullName,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    ProfileAccountType AccountType,
    IReadOnlyList<string> Roles,
    string? OrganizationId,
    string? OrganizationName,
    string? OrganizationRole,
    string? NIDNumber,
    string? CitizenshipNumber,
    int? Age,
    string? Gender,
    string? PermanentAddress,
    string? TemporaryAddress,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Everything the profile service needs to know about the caller: which
/// account backs the profile, which roles were granted and which
/// organization the staff record belongs to.
/// </summary>
public sealed record ProfileContext(
    Guid UserId,
    ProfileAccountType AccountType,
    IReadOnlyList<string> Roles,
    Guid? OrganizationId);

/// <summary>
/// The fields an account owner may change on their own profile. The email
/// address is deliberately absent: it identifies the account and is only
/// changed through a verified re-confirmation flow.
/// </summary>
public sealed record UpdateProfileRequest(
    [Required]
    [NotWhiteSpace(ErrorMessage = "Full name is required.")]
    [TrimmedStringLength(150, MinimumLength = 1)]
    string FullName,
    [PhoneNumber]
    string? PhoneNumber = null,
    [StringLength(50)]
    string? NIDNumber = null,
    [StringLength(50)]
    string? CitizenshipNumber = null,
    [Range(0, 150)]
    int? Age = null,
    [StringLength(20)]
    string? Gender = null,
    [StringLength(500)]
    string? PermanentAddress = null,
    [StringLength(500)]
    string? TemporaryAddress = null);
