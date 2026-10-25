using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Features.Organizations.Contracts;
using PublicServiceCaseManagement.Api.Features.Organizations.Services;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

/// <summary>
/// Platform-managed catalogue of organization types. Types are shared
/// references across every tenant, so only platform super admins can create,
/// update, deactivate, or delete them.
/// </summary>
[ApiController]
[Authorize(Policy = "StaffClient")]
[Route("api/platform/organization-types")]
public sealed class OrganizationTypesController(
    IOrganizationTypeService organizationTypeService,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationTypeResponse>>>
        GetAll(CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var types = await organizationTypeService.GetAllAsync(
            cancellationToken);

        return Ok(types);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationTypeResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var response = await organizationTypeService.GetResponseByIdAsync(
            id,
            cancellationToken);

        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<OrganizationTypeResponse>> Create(
        [FromBody] CreateOrganizationTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            var organizationType = await organizationTypeService.CreateAsync(
                request.Name,
                request.Code,
                cancellationToken);

            var response = await organizationTypeService.GetResponseByIdAsync(
                organizationType.Id,
                cancellationToken);

            return Created(
                $"/api/platform/organization-types/{organizationType.Id}",
                response!);
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

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrganizationTypeResponse>> Update(
        Guid id,
        [FromBody] UpdateOrganizationTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            var organizationType = await organizationTypeService.UpdateAsync(
                id,
                request.Name,
                request.Code,
                cancellationToken);

            if (organizationType is null)
                return NotFound();

            var response = await organizationTypeService.GetResponseByIdAsync(
                organizationType.Id,
                cancellationToken);

            return Ok(response);
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
    public Task<ActionResult<OrganizationTypeResponse>> Deactivate(
        Guid id,
        CancellationToken cancellationToken) =>
        SetActiveStatus(id, false, cancellationToken);

    [HttpPatch("{id:guid}/reactivate")]
    public Task<ActionResult<OrganizationTypeResponse>> Reactivate(
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

        try
        {
            var deleted = await organizationTypeService.DeleteAsync(
                id,
                cancellationToken);

            return deleted ? NoContent() : NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private async Task<ActionResult<OrganizationTypeResponse>> SetActiveStatus(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var organizationType = await organizationTypeService.SetActiveStatusAsync(
            id,
            isActive,
            cancellationToken);

        if (organizationType is null)
            return NotFound();

        var response = await organizationTypeService.GetResponseByIdAsync(
            organizationType.Id,
            cancellationToken);

        return Ok(response);
    }

    private async Task<bool> IsPlatformSuperAdminAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        return role == PlatformRole.SuperAdmin;
    }
}
