
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

[ApiController]
[Authorize(Policy = "StaffClient")]
[Route("api/organizations")]
public sealed class OrganizationsController(
    IOrganizationService organizationService,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext,
    IOrganizationTransactionContext organizationTransactionContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrganizationResponse>> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            var organization = await organizationService.CreateAsync(
                request.Name,
                request.Code,
                cancellationToken);

            return Created(
                $"/api/organizations/{organization.Id}",
                ToResponse(organization));
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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var organizations = await organizationService.GetAllAsync(
            cancellationToken);

        return Ok(organizations.Select(ToResponse).ToList());
    }

    [HttpGet("me")]
    public async Task<ActionResult<IReadOnlyList<OrganizationWorkspaceOption>>>
        GetMyMemberships(CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var userId = await organizationContext.GetApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Forbid();

        var memberships = await organizationService.GetMembershipsForUserAsync(
            userId.Value,
            cancellationToken);

        return Ok(memberships);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var organization = await organizationService.GetByIdAsync(
            id,
            cancellationToken);

        if (organization is null)
            return NotFound();

        return Ok(ToResponse(organization));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrganizationResponse>> Update(
        Guid id,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            var organization = await organizationService.UpdateAsync(
                id,
                request.Name,
                request.Code,
                cancellationToken);

            if (organization is null)
                return NotFound();

            return Ok(ToResponse(organization));
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

    [HttpPatch("{id:guid}/deactivate")]
    public Task<ActionResult<OrganizationResponse>> Deactivate(
        Guid id,
        CancellationToken cancellationToken) =>
        SetActiveStatus(id, false, cancellationToken);

    [HttpPatch("{id:guid}/reactivate")]
    public Task<ActionResult<OrganizationResponse>> Reactivate(
        Guid id,
        CancellationToken cancellationToken) =>
        SetActiveStatus(id, true, cancellationToken);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        // The staff records of an organization are tenant isolated at the
        // database level, so the deletion has to run inside that
        // organization context.
        await organizationTransactionContext.BeginAsync(
            id,
            cancellationToken);

        try
        {
            var deleted = await organizationService.DeleteAsync(
                id,
                cancellationToken);

            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private async Task<ActionResult<OrganizationResponse>> SetActiveStatus(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var organization = await organizationService.SetActiveStatusAsync(
            id,
            isActive,
            cancellationToken);

        if (organization is null)
            return NotFound();

        return Ok(ToResponse(organization));
    }

    private async Task<bool> IsPlatformSuperAdminAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        return role == PlatformRole.SuperAdmin;
    }

    private static OrganizationResponse ToResponse(
        Domain.Organizations.Organization organization) =>
        new(
            organization.Id,
            organization.Name,
            organization.Code,
            organization.IsActive,
            organization.CreatedAt,
            organization.UpdatedAt);
}
