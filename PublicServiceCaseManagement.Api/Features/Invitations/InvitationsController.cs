using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Invitations;

[ApiController]
[Authorize(Policy = AppRoles.StaffPolicy)]
[Route("api/organizations/{organizationId:guid}/invitations")]
public sealed class InvitationsController(
    IInvitationService invitationService,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<InvitationResponse>> Create(
        Guid organizationId,
        [FromBody] CreateInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
        {
            return NotFound();
        }

        if (!await CanAdministerOrganizationAsync(
                organizationId,
                cancellationToken))
        {
            return Forbid();
        }

        var currentUserId =
            await organizationContext.GetApplicationUserIdAsync(
                cancellationToken);

        if (currentUserId is null)
        {
            return Forbid();
        }

        try
        {
            var invitation = await invitationService.CreateAsync(
                organizationId,
                currentUserId.Value,
                request,
                cancellationToken);

            return Created(
                $"/api/organizations/{organizationId}/invitations/{invitation.Id}",
                invitation);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvitationResponse>>> GetAll(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
        {
            return NotFound();
        }

        if (!await CanAdministerOrganizationAsync(
                organizationId,
                cancellationToken))
        {
            return Forbid();
        }

        var invitations = await invitationService.GetAllAsync(
            organizationId,
            cancellationToken);

        return Ok(invitations);
    }

    [HttpPost("{invitationId:guid}/resend")]
    public async Task<ActionResult<InvitationResponse>> Resend(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
        {
            return NotFound();
        }

        if (!await CanAdministerOrganizationAsync(
                organizationId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var invitation = await invitationService.ResendAsync(
                organizationId,
                invitationId,
                cancellationToken);

            return invitation is null ? NotFound() : Ok(invitation);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid organizationId,
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
        {
            return NotFound();
        }

        if (!await CanAdministerOrganizationAsync(
                organizationId,
                cancellationToken))
        {
            return Forbid();
        }

        try
        {
            var revoked = await invitationService.RevokeAsync(
                organizationId,
                invitationId,
                cancellationToken);

            return revoked ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private async Task<bool> CanAdministerOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var platformRole = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        if (platformRole == PlatformRole.SuperAdmin)
        {
            return true;
        }

        var currentOrganizationId =
            await organizationContext.GetOrganizationIdAsync(cancellationToken);

        if (currentOrganizationId != organizationId)
        {
            return false;
        }

        var organizationRole =
            await organizationContext.GetOrganizationRoleAsync(cancellationToken);

        return organizationRole == OrganizationRole.OrganizationAdmin;
    }
}
