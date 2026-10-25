using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PublicServiceCaseManagement.Api.Features.Auth.Contracts;
using PublicServiceCaseManagement.Api.Features.Auth.Services;

namespace PublicServiceCaseManagement.Api.Features.Auth;

/// <summary>
/// Lets an authenticated user manage their own personal access tokens.
/// Listing, creating, and revoking require a valid session (cookie or an
/// existing token); the plaintext value is returned only by create.
/// </summary>
[ApiController]
[Route(PersonalAccessTokenRoutes.Base)]
[Authorize]
public sealed class PersonalAccessTokensController(
    IPersonalAccessTokenService tokenService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PersonalAccessTokenResponse>>>
        List(CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var tokens = await tokenService.ListAsync(userId.Value, cancellationToken);

        return Ok(tokens);
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<CreatePersonalAccessTokenResponse>> Create(
        [FromBody] CreatePersonalAccessTokenRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        try
        {
            var created = await tokenService.CreateAsync(
                userId.Value,
                request,
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("{tokenId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid tokenId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var revoked = await tokenService.RevokeAsync(
            userId.Value,
            tokenId,
            cancellationToken);

        return revoked ? NoContent() : NotFound();
    }

    private Guid? GetUserId()
    {
        var identifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(identifier, out var userId) && userId != Guid.Empty
            ? userId
            : null;
    }
}
