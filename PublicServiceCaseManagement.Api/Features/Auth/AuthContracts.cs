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
