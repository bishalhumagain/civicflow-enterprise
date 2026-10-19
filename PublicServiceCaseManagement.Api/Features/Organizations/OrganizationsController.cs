
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

[ApiController]
[Authorize(Policy = "StaffClient")]
[Route("api/organizations")]
public sealed class OrganizationsController(
    IOrganizationService organizationService,
    IHostEnvironment environment,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrganizationResponse>> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
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
        if (!environment.IsDevelopment())
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var organizations = await organizationService.GetAllAsync(
            cancellationToken);

        return Ok(organizations.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
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
        if (!environment.IsDevelopment())
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

    private async Task<ActionResult<OrganizationResponse>> SetActiveStatus(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
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
