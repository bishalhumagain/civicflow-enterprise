using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailSender emailSender,
    IAuditLogger auditLogger,
    CivicFlowDbContext dbContext,
    TimeProvider timeProvider) : IAuthService
{
    private const string GenericRegistrationMessage =
        "If the email address is eligible, a verification message has been sent.";

    private const string GenericRecoveryMessage =
        "If the email address is registered, a recovery message has been sent.";

    public async Task<AuthOperationResult> RegisterAsync(
        RegisterRequest request,
        Func<string, string, string> linkBuilder,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var existing = await userManager.FindByEmailAsync(email);

        if (existing is not null)
        {
            // Do not reveal that the account already exists.
            return new AuthOperationResult(true, GenericRegistrationMessage);
        }

        var nid = request.NidNumber.Trim();
        var citizenship = request.CitizenshipNumber.Trim();

        if (await dbContext.CitizenProfiles.AnyAsync(
                profile => profile.NIDNumber == nid,
                cancellationToken))
        {
            return new AuthOperationResult(
                false,
                "A citizen profile with this NID number already exists.");
        }

        if (await dbContext.CitizenProfiles.AnyAsync(
                profile => profile.CitizenshipNumber == citizenship,
                cancellationToken))
        {
            return new AuthOperationResult(
                false,
                "A citizen profile with this citizenship number already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var user = ApplicationUser.Create(request.FullName, email, now);

        var createResult = await userManager.CreateAsync(
            user,
            request.Password);

        if (!createResult.Succeeded)
        {
            return new AuthOperationResult(
                false,
                DescribeFailures(createResult));
        }

        await userManager.AddToRoleAsync(user, AppRoles.Citizen);

        var profileRecord = CitizenProfile.Create(
            user.Id,
            request.FullName,
            email,
            request.PhoneNumber,
            nid,
            citizenship,
            age: null,
            gender: null,
            permanentAddress: null,
            temporaryAddress: null,
            now);

        dbContext.CitizenProfiles.Add(profileRecord);
        await dbContext.SaveChangesAsync(cancellationToken);

        auditLogger.AccountLifecycle(
            user.Email ?? email,
            user.Id,
            "registered");

        await SendVerificationEmailAsync(user, linkBuilder);

        return new AuthOperationResult(true, GenericRegistrationMessage);
    }

    public async Task<SignInResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !user.IsActive)
        {
            // Fail as if the password were wrong to avoid account
            // enumeration, while never leaking the deactivation state.
            auditLogger.AuthenticationFailed(email, "invalid_or_inactive");
            return SignInResult.Failed;
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            auditLogger.AuthenticationSucceeded(
                email,
                user.Id,
                "password");
        }
        else if (result.IsLockedOut)
        {
            auditLogger.AuthenticationFailed(email, "locked_out");
        }
        else if (result.IsNotAllowed)
        {
            auditLogger.AuthenticationFailed(email, "not_allowed");
        }
        else
        {
            auditLogger.AuthenticationFailed(email, "invalid_credentials");
        }

        return result;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        await signInManager.SignOutAsync();
    }

    public async Task<AuthOperationResult> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return new AuthOperationResult(
                false,
                "The verification request is invalid or has expired.");
        }

        if (user.EmailConfirmed)
        {
            return new AuthOperationResult(true, "The email is already verified.");
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);

        return result.Succeeded
            ? new AuthOperationResult(true, "The email has been verified.")
            : new AuthOperationResult(
                false,
                "The verification request is invalid or has expired.");
    }

    public async Task<AuthOperationResult> ResendVerificationAsync(
        ResendVerificationRequest request,
        Func<string, string, string> linkBuilder,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null && !user.EmailConfirmed && user.IsActive)
        {
            await SendVerificationEmailAsync(user, linkBuilder);
        }

        return new AuthOperationResult(true, GenericRegistrationMessage);
    }

    public async Task<AuthOperationResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        Func<string, string, string> linkBuilder,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null && user.IsActive)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var link = linkBuilder(email, token);

            await emailSender.SendAsync(
                email,
                "Reset your CivicFlow password",
                BuildBody(
                    "A password reset was requested for your CivicFlow account.",
                    link),
                cancellationToken);
        }

        return new AuthOperationResult(true, GenericRecoveryMessage);
    }

    public async Task<AuthOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || !user.IsActive)
        {
            return new AuthOperationResult(
                false,
                "The reset request is invalid or has expired.");
        }

        var result = await userManager.ResetPasswordAsync(
            user,
            request.Token,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return new AuthOperationResult(
                false,
                DescribeFailures(result));
        }

        // Invalidate any existing sessions after a credential change.
        await userManager.UpdateSecurityStampAsync(user);

        auditLogger.AccountLifecycle(
            email,
            user.Id,
            "password_reset");

        return new AuthOperationResult(true, "The password has been reset.");
    }

    public async Task<AuthOperationResult> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null || !user.IsActive)
        {
            return new AuthOperationResult(false, "The account is not available.");
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return new AuthOperationResult(false, DescribeFailures(result));
        }

        await userManager.UpdateSecurityStampAsync(user);

        auditLogger.AccountLifecycle(
            user.Email ?? string.Empty,
            user.Id,
            "password_changed");

        return new AuthOperationResult(true, "The password has been changed.");
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.ApplicationUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == userId,
                cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);

        var permissions = roles
            .Select(role => role switch
            {
                AppRoles.SuperAdmin => "platform.manage",
                AppRoles.Staff => "organization.manage",
                AppRoles.Citizen => "citizen.self",
                _ => "unknown"
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();

        return new CurrentUserResponse(
            user.Id,
            user.FullName,
            user.Email ?? string.Empty,
            user.PlatformRole.ToString(),
            user.EmailConfirmed,
            roles.OrderBy(role => role, StringComparer.Ordinal).ToList(),
            permissions);
    }

    private async Task SendVerificationEmailAsync(
        ApplicationUser user,
        Func<string, string, string> linkBuilder)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var email = user.Email ?? string.Empty;
        var link = linkBuilder(email, token);

        await emailSender.SendAsync(
            email,
            "Verify your CivicFlow email",
            BuildBody(
                "Confirm your email address to activate your CivicFlow account.",
                link));
    }

    private static string BuildBody(string preamble, string link) =>
        $"{preamble}{Environment.NewLine}{Environment.NewLine}{link}";

    private static string DescribeFailures(IdentityResult result) =>
        string.Join(
            " ",
            result.Errors
                .Select(error => error.Description)
                .Distinct(StringComparer.Ordinal));
}
