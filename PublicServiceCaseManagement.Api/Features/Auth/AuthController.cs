using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Api.Features.Auth;

[ApiController]
[Route(AuthRoutes.Base)]
public sealed class AuthController(
    IAuthService authService,
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
    /// Organization portal sign-in. Only staff and super administrator
    /// accounts are accepted here, so a citizen account cannot open the
    /// organization workspace.
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
