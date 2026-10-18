
using Microsoft.AspNetCore.Mvc;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

[ApiController]
[Route("api/organizations")]
public sealed class OrganizationsController(
    IOrganizationService organizationService,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrganizationResponse>> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            var organization = await organizationService.CreateAsync(
                request.Name,
                request.Code,
                cancellationToken);

            var response = ToResponse(organization);

            return Created(
                $"/api/organizations/{organization.Id}",
                response);
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

        var organization = await organizationService.SetActiveStatusAsync(
            id,
            isActive,
            cancellationToken);

        if (organization is null)
            return NotFound();

        return Ok(ToResponse(organization));
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
