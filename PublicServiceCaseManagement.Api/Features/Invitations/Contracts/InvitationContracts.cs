using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Invitations.Contracts;

/// <summary>Creates a single-use invitation for an organization membership.</summary>
/// <param name="FullName">Required invitee display name, up to 150 characters.</param>
/// <param name="Email">Required invitee email address, up to 254 characters.</param>
/// <param name="PhoneNumber">Optional invitee phone number in the accepted phone format.</param>
/// <param name="Role">Organization role permitted for the authenticated inviter.</param>
public sealed record CreateInvitationRequest(
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(150, MinimumLength = 1)]
    string FullName,
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [PhoneNumber] string? PhoneNumber,
    [Required]
    [NotWhiteSpace]
    [StringLength(32)]
    string Role);

/// <summary>Invitation metadata; the raw invitation token is never returned.</summary>
public sealed record InvitationResponse(
    Guid Id,
    Guid OrganizationId,
    string Email,
    string FullName,
    string? PhoneNumber,
    string Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsExpired,
    bool IsUsed,
    bool IsRevoked,
    bool DeliveryFailed);

public sealed record InvitationDetailsResponse(
    string Email,
    string FullName,
    string? PhoneNumber,
    string OrganizationName,
    string Role,
    DateTimeOffset ExpiresAt);

/// <summary>Resolves safe invitation details from a one-time invitation token.</summary>
/// <param name="Token">Opaque token from the invitation link; never a user or organization ID.</param>
public sealed record InvitationLookupRequest(
    [Required]
    [StringLength(200, MinimumLength = 16)]
    string Token);

/// <summary>Accepts a valid single-use invitation and sets the invitee's password.</summary>
/// <param name="Token">Opaque token from the invitation link.</param>
/// <param name="Password">Chosen password of 12–128 characters, subject to Identity policy.</param>
public sealed record AcceptInvitationRequest(
    [Required]
    [StringLength(200, MinimumLength = 16)]
    string Token,
    [Required]
    [StringLength(128, MinimumLength = 12)]
    string Password);

public sealed record AcceptInvitationResponse(
    bool Succeeded,
    string Email,
    string Portal,
    string Message);

/// <summary>
/// Operations that act on an invitation token before an account exists. They
/// are intentionally shaped as outcomes rather than exceptions because every
/// failure has to stay indistinguishable from an invalid link.
/// </summary>
public sealed record AcceptInvitationOutcome(
    bool Succeeded,
    string? Email,
    string Code,
    string Message)
{
    public static AcceptInvitationOutcome Success(string email) =>
        new(
            true,
            email,
            "invitation_accepted",
            "Your account is ready. Sign in with the password you just chose.");

    public static AcceptInvitationOutcome Unavailable() =>
        new(
            false,
            null,
            "invitation_unavailable",
            "This invitation link is invalid, expired, or has already been used.");

    public static AcceptInvitationOutcome InvalidPassword(string message) =>
        new(false, null, "invalid_password", message);

    public static AcceptInvitationOutcome AccountInactive() =>
        new(
            false,
            null,
            "account_inactive",
            "The account for this email address is inactive. Contact your administrator.");
}