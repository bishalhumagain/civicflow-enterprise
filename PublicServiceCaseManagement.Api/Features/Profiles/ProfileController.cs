using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Features.Profiles.Contracts;
using PublicServiceCaseManagement.Api.Features.Profiles.Services;

namespace PublicServiceCaseManagement.Api.Features.Profiles;

/// <summary>
/// The self-service profile of the signed-in account. The same contract
/// serves citizens and organization staff; platform administrators are
/// excluded because their identity is managed centrally.
/// </summary>
[ApiController]
[Authorize(Policy = AppRoles.ProfilePolicy)]
[Route(ProfileRoutes.Base)]
public sealed class ProfileController(
    IProfileService profileService,
    IAuthenticatedOrganizationResolver organizationResolver,
    IOrganizationTransactionContext organizationTransactionContext,
    IBusinessEndpointAccess businessEndpointAccess) : ControllerBase
{
    /// <summary>Returns the profile resolved from the authenticated principal.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<ProfileResponse>> GetMe(
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var context = await BuildContextAsync(cancellationToken);

        if (context is null)
            return Unauthorized();

        var profile = await profileService.GetAsync(context, cancellationToken);

        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Updates only the profile fields the authenticated account may maintain.</summary>
    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateMe(
        [FromBody] UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var context = await BuildContextAsync(cancellationToken);

        if (context is null)
            return Unauthorized();

        try
        {
            var updated = await profileService.UpdateAsync(
                context,
                request,
                cancellationToken);

            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private async Task<ProfileContext?> BuildContextAsync(
        CancellationToken cancellationToken)
    {
        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return null;

        var roles = User
            .FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .ToArray();

        var accountType = await profileService.GetAccountTypeAsync(
            userId.Value,
            roles,
            cancellationToken);

        Guid? organizationId = null;

        if (accountType == ProfileAccountType.Staff)
        {
            var preferredOrganizationId = await organizationResolver.ResolveAsync(
                cancellationToken);

            organizationId = await profileService.ResolveOrganizationIdAsync(
                userId.Value,
                preferredOrganizationId,
                cancellationToken);

            if (organizationId.HasValue)
            {
                // Staff records are tenant scoped, so start the organization transaction.
                await organizationTransactionContext.BeginAsync(
                    organizationId.Value,
                    cancellationToken);
            }
        }

        return new ProfileContext(
            userId.Value,
            accountType,
            roles,
            organizationId);
    }
}