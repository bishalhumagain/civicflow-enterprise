namespace PublicServiceCaseManagement.Api.Features.Platform;

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
    string FullName,
    string? PhoneNumber,
    string? NIDNumber,
    string? CitizenshipNumber,
    int? Age,
    string? Gender,
    string? PermanentAddress,
    string? TemporaryAddress);
