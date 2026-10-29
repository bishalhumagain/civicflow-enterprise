using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PublicServiceCaseManagement.Api.Features.Auth.Contracts;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Api.Features.Auth;

/// <summary>
/// Exchanges a valid personal access token for a short-lived signed JWT.
/// Only the PAT scheme is accepted; cookie-authenticated browser sessions
/// cannot mint bearer tokens, and the endpoint never accepts anonymity.
/// </summary>
[ApiController]
[Route(AccessTokenRoutes.Base)]
public sealed class AccessTokensController(
    IJwtAccessTokenIssuer tokenIssuer) : ControllerBase
{
    [HttpPost]
    [Authorize(
        AuthenticationSchemes =
            PersonalAccessTokenAuthenticationHandler.SchemeName)]
    [EnableRateLimiting("auth")]
    public ActionResult<AccessTokenResponse> Create()
    {
        var identifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(identifier, out var userId) ||
            userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var fullName = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
        var roles = User
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();

        var issued = tokenIssuer.Issue(userId, fullName, email, roles);

        return Ok(new AccessTokenResponse(
            issued.Token,
            "Bearer",
            issued.ExpiresInSeconds,
            issued.ExpiresAt));
    }
}
