using Microsoft.AspNetCore.Identity;

namespace PublicServiceCaseManagement.Api.Features.Auth;

public interface IAuthService
{
    Task<AuthOperationResult> RegisterAsync(
        RegisterRequest request,
        Func<string, string, string> linkBuilder,
        CancellationToken cancellationToken = default);

    Task<SignInResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    Task<AuthOperationResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthOperationResult> ResendVerificationAsync(
        ResendVerificationRequest request,
        Func<string, string, string> linkBuilder,
        CancellationToken cancellationToken = default);

    Task<AuthOperationResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        Func<string, string, string> linkBuilder,
        CancellationToken cancellationToken = default);

    Task<AuthOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthOperationResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<CurrentUserResponse?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
