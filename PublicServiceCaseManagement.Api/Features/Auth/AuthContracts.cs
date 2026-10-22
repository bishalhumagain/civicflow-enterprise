using System.ComponentModel.DataAnnotations;

namespace PublicServiceCaseManagement.Api.Features.Auth;

public static class AuthRoutes
{
    public const string Base = "api/auth";
}

public sealed record RegisterRequest(
    [Required]
    [StringLength(150, MinimumLength = 1)]
    string FullName,
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required]
    [Phone]
    [StringLength(32)]
    string PhoneNumber,
    [Required]
    [StringLength(64, MinimumLength = 1)]
    string NidNumber,
    [Required]
    [StringLength(64, MinimumLength = 1)]
    string CitizenshipNumber,
    [Required]
    [StringLength(128, MinimumLength = 12)]
    string Password);

public sealed record LoginRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required]
    [StringLength(128, MinimumLength = 1)]
    string Password,
    [StringLength(128)]
    string? OrganizationId = null);

/// <summary>
/// The sign-in surface a request is bound to. Each portal accepts only the
/// roles that are meant to use it, so the same credentials can never open
/// both dashboards.
/// </summary>
public enum SignInPortal
{
    /// <summary>The public citizen portal: accounts with the citizen role.</summary>
    Citizen,

    /// <summary>The organization portal: staff and super administrators.</summary>
    Organization
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

public sealed record VerifyEmailRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required]
    string Token);

public sealed record ResendVerificationRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email);

public sealed record ForgotPasswordRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email);

public sealed record ResetPasswordRequest(
    [Required]
    [EmailAddress]
    [StringLength(254)]
    string Email,
    [Required]
    string Token,
    [Required]
    [StringLength(128, MinimumLength = 12)]
    string NewPassword);

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

public sealed record CurrentUserResponse(
    Guid Id,
    string FullName,
    string Email,
    string PlatformRole,
    bool EmailConfirmed,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
