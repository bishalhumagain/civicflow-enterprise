
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
    IOrganizationTypeService organizationTypeService,
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
                cancellationToken,
                request.OrganizationTypeId);

            return Created(
                $"/api/organizations/{organization.Id}",
                await ToResponseAsync(organization, cancellationToken));
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

        if (await IsPlatformSuperAdminAsync(cancellationToken))
        {
            var allOrganizations = await organizationService.GetAllAsync(
                cancellationToken);

            return Ok(await ToResponsesAsync(
                allOrganizations,
                cancellationToken));
        }

        var userId = await organizationContext.GetApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Forbid();

        var managedOrganizations =
            await organizationService.GetManagedOrganizationsForUserAsync(
                userId.Value,
                cancellationToken);

        return Ok(await ToResponsesAsync(
            managedOrganizations,
            cancellationToken));
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

        return Ok(await ToResponseAsync(organization, cancellationToken));
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
                cancellationToken,
                request.OrganizationTypeId);

            if (organization is null)
                return NotFound();

            return Ok(await ToResponseAsync(organization, cancellationToken));
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

        return Ok(await ToResponseAsync(organization, cancellationToken));
    }

    private async Task<bool> IsPlatformSuperAdminAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        return role == PlatformRole.SuperAdmin;
    }

    private async Task<OrganizationResponse> ToResponseAsync(
        Domain.Organizations.Organization organization,
        CancellationToken cancellationToken)
    {
        var names = await organizationTypeService.GetNamesByIdsAsync(
            new[] { organization.OrganizationTypeId },
            cancellationToken);

        return new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Code,
            organization.OrganizationTypeId,
            names.GetValueOrDefault(organization.OrganizationTypeId),
            organization.IsActive,
            organization.CreatedAt,
            organization.UpdatedAt);
    }

    private async Task<IReadOnlyList<OrganizationResponse>> ToResponsesAsync(
        IReadOnlyList<Domain.Organizations.Organization> organizations,
        CancellationToken cancellationToken)
    {
        if (organizations.Count == 0)
            return Array.Empty<OrganizationResponse>();

        var names = await organizationTypeService.GetNamesByIdsAsync(
            organizations.Select(organization =>
                organization.OrganizationTypeId),
            cancellationToken);

        return organizations
            .Select(organization => new OrganizationResponse(
                organization.Id,
                organization.Name,
                organization.Code,
                organization.OrganizationTypeId,
                names.GetValueOrDefault(organization.OrganizationTypeId),
                organization.IsActive,
                organization.CreatedAt,
                organization.UpdatedAt))
            .ToList();
    }
}
