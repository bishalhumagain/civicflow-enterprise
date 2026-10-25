using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Audit;
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
        var email = TextNormalization.NormalizeEmail(request.Email);
        var fullName = TextNormalization.NormalizeName(request.FullName);
        var phoneNumber = PhoneNumbers.Normalize(request.PhoneNumber);

        var existing = await FindUserByEmailAsync(email, cancellationToken);

        if (existing is not null)
        {
            // Do not reveal that the account already exists.
            return new AuthOperationResult(true, GenericRegistrationMessage);
        }

        var nid = TextNormalization.NormalizeOptionalIdentifier(
            request.NidNumber) ?? string.Empty;
        var citizenship = TextNormalization.NormalizeOptionalIdentifier(
            request.CitizenshipNumber) ?? string.Empty;

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
        var user = ApplicationUser.Create(fullName, email, now);

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
            fullName,
            email,
            phoneNumber,
            nid,
            citizenship,
            age: null,
            gender: null,
            permanentAddress: null,
            temporaryAddress: null,
            now);

        dbContext.CitizenProfiles.Add(profileRecord);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogger.AccountLifecycleAsync(
            user.Email ?? email,
            user.Id,
            AuditActions.AccountRegistered,
            cancellationToken: cancellationToken);

        await SendVerificationEmailAsync(user, linkBuilder);

        return new AuthOperationResult(true, GenericRegistrationMessage);
    }

    public async Task<LoginOutcome> LoginAsync(
        LoginRequest request,
        SignInPortal portal,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await FindUserByEmailAsync(email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            // Fail as if the password were wrong to avoid account
            // enumeration, while never leaking the deactivation state.
            await auditLogger.AuthenticationFailedAsync(
                email,
                "invalid_or_inactive",
                cancellationToken: cancellationToken);
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var roles = await userManager.GetRolesAsync(user);

            if (!IsAllowedForPortal(roles, portal))
            {
                // PasswordSignInAsync already issued the session cookie, so it
                // has to be dropped again. A wrong-portal attempt must never
                // leave a usable session behind.
                await signInManager.SignOutAsync();

                await auditLogger.AuthenticationFailedAsync(
                    email,
                    $"wrong_portal:{portal.ToString().ToLowerInvariant()}",
                    cancellationToken: cancellationToken);

                return WrongPortal(portal, roles);
            }

            await auditLogger.AuthenticationSucceededAsync(
                email,
                user.Id,
                "password",
                cancellationToken: cancellationToken);

            return LoginOutcome.Success(roles);
        }

        if (result.IsLockedOut)
        {
            await auditLogger.AuthenticationFailedAsync(
                email,
                "locked_out",
                cancellationToken: cancellationToken);

            return LoginOutcome.Failed(
                LoginFailureReason.LockedOut,
                "account_locked",
                "The account is temporarily locked.");
        }

        if (result.IsNotAllowed)
        {
            await auditLogger.AuthenticationFailedAsync(
                email,
                "not_allowed",
                cancellationToken: cancellationToken);

            return LoginOutcome.Failed(
                LoginFailureReason.EmailNotConfirmed,
                "email_not_confirmed",
                "The account is not permitted to sign in. " +
                "Confirm your email address first.");
        }

        await auditLogger.AuthenticationFailedAsync(
            email,
            "invalid_credentials",
            cancellationToken: cancellationToken);

        return InvalidCredentials();
    }

    /// <summary>
    /// Decides whether the account's roles may use the requested portal.
    /// Every portal is exclusive: citizens use the citizen portal, staff use
    /// the organization portal, and super administrators use the dedicated
    /// platform administration portal.
    /// </summary>
    private static bool IsAllowedForPortal(
        IEnumerable<string> roles,
        SignInPortal portal)
    {
        var roleSet = roles.ToHashSet(StringComparer.Ordinal);

        return portal switch
        {
            SignInPortal.Citizen => roleSet.Contains(AppRoles.Citizen),
            SignInPortal.Organization => roleSet.Contains(AppRoles.Staff),
            SignInPortal.PlatformAdmin => roleSet.Contains(AppRoles.SuperAdmin),
            _ => false
        };
    }

    private static LoginOutcome InvalidCredentials() =>
        LoginOutcome.Failed(
            LoginFailureReason.InvalidCredentials,
            "invalid_credentials",
            "Invalid credentials.");

    private static LoginOutcome WrongPortal(
        SignInPortal portal,
        IEnumerable<string> roles)
    {
        var roleSet = roles.ToHashSet(StringComparer.Ordinal);

        if (portal == SignInPortal.Citizen)
        {
            // The message has to name the portal the account can actually
            // use, otherwise a super administrator would be sent to a portal
            // that refuses them just as well.
            var target = roleSet.Contains(AppRoles.SuperAdmin)
                ? "the platform administration portal"
                : roleSet.Contains(AppRoles.Staff)
                    ? "the organization portal"
                    : "the correct portal";

            return LoginOutcome.Failed(
                LoginFailureReason.WrongPortal,
                "not_a_citizen_account",
                $"This account is not a citizen account. " +
                $"Sign in through {target} instead.");
        }

        if (portal == SignInPortal.Organization &&
            roleSet.Contains(AppRoles.SuperAdmin))
        {
            return LoginOutcome.Failed(
                LoginFailureReason.WrongPortal,
                "use_platform_admin_portal",
                "Super administrator accounts sign in through the dedicated " +
                "platform administration portal.");
        }

        if (portal == SignInPortal.PlatformAdmin)
        {
            var target = roleSet.Contains(AppRoles.Staff)
                ? "the organization portal"
                : roleSet.Contains(AppRoles.Citizen)
                    ? "the citizen portal"
                    : "the correct portal";

            return LoginOutcome.Failed(
                LoginFailureReason.WrongPortal,
                "not_a_platform_admin",
                $"This account is not a platform administrator account. " +
                $"Sign in through {target} instead.");
        }

        return LoginOutcome.Failed(
            LoginFailureReason.WrongPortal,
            "not_an_organization_account",
            "This account is not an organization account. " +
            "Sign in through the citizen portal instead.");
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
        var user = await FindUserByEmailAsync(email, cancellationToken);

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
        var user = await FindUserByEmailAsync(email, cancellationToken);

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
        var user = await FindUserByEmailAsync(email, cancellationToken);

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
        var user = await FindUserByEmailAsync(email, cancellationToken);

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

        await auditLogger.AccountLifecycleAsync(
            email,
            user.Id,
            AuditActions.AccountPasswordReset,
            cancellationToken: cancellationToken);

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

        // The stamp rotation invalidates every existing session. Because this
        // is a self-service change made from an already-authenticated browser,
        // refresh the sign-in so the current session keeps working while all
        // other sessions (still carrying the old stamp) are revoked.
        await signInManager.RefreshSignInAsync(user);

        await auditLogger.AccountLifecycleAsync(
            user.Email ?? string.Empty,
            user.Id,
            AuditActions.AccountPasswordChanged,
            cancellationToken: cancellationToken);

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

    private async Task<ApplicationUser?> FindUserByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null)
        {
            return user;
        }

        var matches = await dbContext.ApplicationUsers
            .Where(candidate =>
                candidate.Email != null &&
                candidate.Email.ToLower() == email.ToLower())
            .Take(2)
            .ToListAsync(cancellationToken);

        return matches.Count == 1 ? matches[0] : null;
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
