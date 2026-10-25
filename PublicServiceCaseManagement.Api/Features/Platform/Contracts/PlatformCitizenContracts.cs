using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Platform.Contracts;

/// <summary>
/// A citizen account as seen by a platform administrator. Authentication
/// secrets, password hashes, and security tokens are never part of this
/// contract.
/// </summary>
public sealed record PlatformCitizenSummary(
    Guid UserId,
    Guid ProfileId,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool EmailConfirmed,
    bool IsActive,
    string? NIDNumber,
    string? CitizenshipNumber,
    int? Age,
    string? Gender,
    string? PermanentAddress,
    string? TemporaryAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// The profile fields a platform administrator may maintain. The email
/// address is not editable here because changing it alters the account
/// identity and requires re-verification.
/// </summary>
public sealed record UpdatePlatformCitizenRequest(
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(150, MinimumLength = 1)]
    string FullName,
    [PhoneNumber]
    string? PhoneNumber,
    [StringLength(50)]
    string? NIDNumber,
    [StringLength(50)]
    string? CitizenshipNumber,
    [Range(0, 150)]
    int? Age,
    [StringLength(20)]
    string? Gender,
    [StringLength(500)]
    string? PermanentAddress,
    [StringLength(500)]
    string? TemporaryAddress);
