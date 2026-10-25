using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PublicServiceCaseManagement.Api.Features.Invitations;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Features.Auth.Contracts;
using PublicServiceCaseManagement.Api.Features.Auth.Services;
using PublicServiceCaseManagement.Api.Features.Invitations.Contracts;
using PublicServiceCaseManagement.Api.Features.Invitations.Services;

namespace PublicServiceCaseManagement.Api.Features.Auth;

[ApiController]
[Route(AuthRoutes.Base)]
public sealed class AuthController(
    IAuthService authService,
    IInvitationService invitationService,
    IAntiforgery antiforgery,
    IConfiguration configuration) : ControllerBase
{
    private string FrontendBaseUrl =>
        configuration["Frontend:BaseUrl"]?.TrimEnd('/')
        ?? "http://localhost:5173";

    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        return Ok(new { token = tokens.RequestToken });
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthOperationResult>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(
            request,
            BuildVerifyLink,
            cancellationToken);

        return result.Succeeded
            ? Ok(result)
            : BadRequest(result);
    }

    /// <summary>
    /// Citizen portal sign-in. Only accounts with the citizen role are
    /// accepted here, so a staff account cannot open the citizen dashboard.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        return await SignIn(request, SignInPortal.Citizen, cancellationToken);
    }

    /// <summary>
    /// Organization portal sign-in. Only staff accounts are accepted here, so
    /// a citizen account cannot open the organization workspace and a super
    /// administrator cannot reach platform administration through it.
    /// </summary>
    [HttpPost("login/organization")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> OrganizationLogin(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        return await SignIn(
            request,
            SignInPortal.Organization,
            cancellationToken);
    }

    /// <summary>
    /// Dedicated platform administration portal. It is served at an unlisted
    /// route and only accepts super-administrator accounts.
    /// </summary>
    [HttpPost("login/platform-admin")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> PlatformAdminLogin(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        return await SignIn(
            request,
            SignInPortal.PlatformAdmin,
            cancellationToken);
    }

    /// <summary>
    /// Resolves the safe, non-secret details of an invitation so the
    /// password-setup page can show who was invited and where.
    /// </summary>
    [HttpPost("invitations/lookup")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LookupInvitation(
        [FromBody] InvitationLookupRequest request,
        CancellationToken cancellationToken)
    {
        var details = await invitationService.GetDetailsAsync(
            request.Token,
            cancellationToken);

        if (details is null)
        {
            return StatusCode(
                StatusCodes.Status410Gone,
                new
                {
                    code = "invitation_unavailable",
                    error = "This invitation link is invalid, expired, or has already been used."
                });
        }

        return Ok(details);
    }

    /// <summary>
    /// Redeems a single-use invitation and lets the invitee choose a password.
    /// The invitation is marked used before any session can be created, and
    /// no password is ever generated or emailed.
    /// </summary>
    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    [EnableRateLimiting("password")]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var outcome = await invitationService.AcceptAsync(
            request,
            cancellationToken);

        if (outcome.Succeeded)
        {
            return Ok(new AcceptInvitationResponse(
                true,
                outcome.Email ?? string.Empty,
                "organization",
                outcome.Message));
        }

        return outcome.Code switch
        {
            "invitation_unavailable" => StatusCode(
                StatusCodes.Status410Gone,
                new { code = outcome.Code, error = outcome.Message }),
            "account_inactive" => StatusCode(
                StatusCodes.Status403Forbidden,
                new { code = outcome.Code, error = outcome.Message }),
            _ => BadRequest(new { code = outcome.Code, error = outcome.Message })
        };
    }

    private async Task<IActionResult> SignIn(
        LoginRequest request,
        SignInPortal portal,
        CancellationToken cancellationToken)
    {
        var outcome = await authService.LoginAsync(
            request,
            portal,
            cancellationToken);

        if (outcome.Succeeded)
        {
            return Ok(new
            {
                authenticated = true,
                portal = portal.ToString().ToLowerInvariant(),
                roles = outcome.Roles
            });
        }

        return outcome.FailureReason switch
        {
            LoginFailureReason.LockedOut => StatusCode(
                StatusCodes.Status423Locked,
                new
                {
                    code = outcome.Code,
                    error = outcome.Message
                }),
            LoginFailureReason.EmailNotConfirmed => StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    code = outcome.Code,
                    error = outcome.Message
                }),
            LoginFailureReason.WrongPortal => StatusCode(
                StatusCodes.Status403Forbidden,
                new
                {
                    code = outcome.Code,
                    error = outcome.Message
                }),
            _ => Unauthorized(new
            {
                code = outcome.Code,
                error = outcome.Message
            })
        };
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthOperationResult>> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.VerifyEmailAsync(
            request,
            cancellationToken);

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<AuthOperationResult>> ResendVerification(
        [FromBody] ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ResendVerificationAsync(
            request,
            BuildVerifyLink,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password")]
    public async Task<ActionResult<AuthOperationResult>> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ForgotPasswordAsync(
            request,
            BuildResetLink,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("password")]
    public async Task<ActionResult<AuthOperationResult>> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.ResetPasswordAsync(
            request,
            cancellationToken);

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("password")]
    public async Task<ActionResult<AuthOperationResult>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await authService.ChangePasswordAsync(
            userId.Value,
            request,
            cancellationToken);

        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> Me(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var current = await authService.GetCurrentUserAsync(
            userId.Value,
            cancellationToken);

        return current is null ? Unauthorized() : Ok(current);
    }

    private Guid? GetUserId()
    {
        var identifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(identifier, out var userId) && userId != Guid.Empty
            ? userId
            : null;
    }

    private string BuildVerifyLink(string email, string token) =>
        $"{FrontendBaseUrl}/citizen/verify-email" +
        $"?email={Uri.EscapeDataString(email)}" +
        $"&token={Uri.EscapeDataString(token)}";

    private string BuildResetLink(string email, string token) =>
        $"{FrontendBaseUrl}/reset-password" +
        $"?email={Uri.EscapeDataString(email)}" +
        $"&token={Uri.EscapeDataString(token)}";
}
