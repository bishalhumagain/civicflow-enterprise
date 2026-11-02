using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Infrastructure.Validation;

namespace PublicServiceCaseManagement.Api.Features.Auth.Contracts;

public static class AuthRoutes
{
    public const string Base = "api/auth";
}

/// <summary>
/// Creates a citizen account. The password is submitted only for Identity to
/// hash; it is never returned by the API.
/// </summary>
/// <param name="FullName">Required display name, up to 150 characters.</param>
/// <param name="Email">Required email address, up to 254 characters.</param>
/// <param name="PhoneNumber">Required phone number in the accepted phone format.</param>
/// <param name="NidNumber">Required national identity number, up to 50 characters.</param>
/// <param name="CitizenshipNumber">Required citizenship number, up to 50 characters.</param>
/// <param name="Password">Required password of 12–128 characters, subject to Identity policy.</param>
public sealed record RegisterRequest(
    [Required]
    [NotWhiteSpace]
    [TrimmedStringLength(150, MinimumLength = 1)]
    string FullName,
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required]
    [NotWhiteSpace]
    [PhoneNumber]
    string PhoneNumber,
    [Required]
    [NotWhiteSpace]
    [StringLength(50, MinimumLength = 1)]
    string NidNumber,
    [Required]
    [NotWhiteSpace]
    [StringLength(50, MinimumLength = 1)]
    string CitizenshipNumber,
    [Required]
    [StringLength(128, MinimumLength = 12)]
    string Password);

/// <summary>
/// Starts a session for the selected portal. The optional organization ID is a
/// membership selector and is checked against the signed-in account.
/// </summary>
/// <param name="Email">Account email address.</param>
/// <param name="Password">Account password.</param>
/// <param name="OrganizationId">Optional organization identifier for staff with multiple memberships.</param>
public sealed record LoginRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required]
    [StringLength(128, MinimumLength = 1)]
    string Password,
    [StringLength(64)] string? OrganizationId = null);

/// <summary>
/// The sign-in surface a request is bound to. Each portal accepts only the
/// roles that are meant to use it, so the same credentials can never open
/// both dashboards.
/// </summary>
public enum SignInPortal
{
    /// <summary>The public citizen portal: accounts with the citizen role.</summary>
    Citizen,

    /// <summary>The organization portal: staff accounts only.</summary>
    Organization,

    /// <summary>
    /// The platform administration portal: super-administrator accounts only.
    /// It is a separate, unlisted surface so an organization login can never
    /// reach platform-wide administration.
    /// </summary>
    PlatformAdmin
}

/// <summary>
/// Why a sign-in attempt did not produce a session.
/// </summary>
public enum LoginFailureReason
{
    None,
    InvalidCredentials,
    LockedOut,
    EmailNotConfirmed,
    WrongPortal
}

/// <summary>
/// Outcome of a sign-in attempt, including the reason it failed so the API
/// can answer with a stable status code and machine-readable code.
/// </summary>
public sealed record LoginOutcome(
    bool Succeeded,
    LoginFailureReason FailureReason,
    string? Code,
    string? Message,
    IReadOnlyList<string> Roles)
{
    public static LoginOutcome Success(IEnumerable<string> roles) =>
        new(
            true,
            LoginFailureReason.None,
            null,
            null,
            roles.ToArray());

    public static LoginOutcome Failed(
        LoginFailureReason reason,
        string code,
        string message) =>
        new(false, reason, code, message, Array.Empty<string>());
}

/// <summary>Confirms a citizen account using a one-time email token.</summary>
/// <param name="Email">Email address associated with the account.</param>
/// <param name="Token">One-time Identity confirmation token.</param>
public sealed record VerifyEmailRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required] string Token);

public sealed record ResendVerificationRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email);

/// <summary>Requests a password-reset link without disclosing account existence.</summary>
/// <param name="Email">Email address to which a reset link may be sent.</param>
public sealed record ForgotPasswordRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email);

/// <summary>Changes an account password using a one-time reset token.</summary>
/// <param name="Email">Email address associated with the account.</param>
/// <param name="Token">One-time Identity password-reset token.</param>
/// <param name="NewPassword">New password of 12–128 characters, subject to Identity policy.</param>
public sealed record ResetPasswordRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required]
    [StringLength(2048, MinimumLength = 1)]
    string Token,
    [Required]
    [StringLength(128, MinimumLength = 12)]
    string NewPassword);

/// <summary>Changes the password of the currently authenticated account.</summary>
/// <param name="CurrentPassword">The account's current password.</param>
/// <param name="NewPassword">New password of 12–128 characters, subject to Identity policy.</param>
public sealed record ChangePasswordRequest(
    [Required]
    [StringLength(128, MinimumLength = 1)]
    string CurrentPassword,
    [Required]
    [StringLength(128, MinimumLength = 12)]
    string NewPassword);

/// <summary>
/// Uniform response returned by registration and password recovery flows so
/// the API never reveals whether an email address is registered.
/// </summary>
public sealed record AuthOperationResult(bool Succeeded, string Message);

/// <summary>
/// Safe account and authorization details for the currently authenticated
/// caller. It contains no credentials or bearer tokens.
/// </summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string FullName,
    string Email,
    string PlatformRole,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);