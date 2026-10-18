
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Staff;

[ApiController]
[Route("api/staff")]
public sealed class StaffController(
    IStaffService staffService,
    IHostEnvironment environment,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        try
        {
            var staffMember = await staffService.CreateAsync(
                organizationContext.OrganizationId,
                request.FullName,
                request.Email,
                cancellationToken);

            return Created(
                $"/api/staff/{staffMember.Id}",
                new
                {
                    staffMember.Id,
                    staffMember.FullName,
                    staffMember.Email,
                    staffMember.IsActive,
                    staffMember.CreatedAt,
                    staffMember.UpdatedAt
                });
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
    public async Task<IActionResult> GetAll(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var staffMembers = await staffService.GetAllAsync(
            organizationContext.OrganizationId,
            isActive,
            cancellationToken);

        return Ok(staffMembers.Select(staff => new
        {
            staff.Id,
            staff.FullName,
            staff.Email,
            staff.IsActive,
            staff.CreatedAt,
            staff.UpdatedAt
        }));
    }

    [HttpPatch("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(
        Guid id,
        CancellationToken cancellationToken) =>
        SetActiveStatus(id, false, cancellationToken);

    [HttpPatch("{id:guid}/reactivate")]
    public Task<IActionResult> Reactivate(
        Guid id,
        CancellationToken cancellationToken) =>
        SetActiveStatus(id, true, cancellationToken);

    private async Task<IActionResult> SetActiveStatus(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var staffMember = await staffService.SetActiveStatusAsync(
            organizationContext.OrganizationId,
            id,
            isActive,
            cancellationToken);

        if (staffMember is null)
            return NotFound();

        return Ok(new
        {
            staffMember.Id,
            staffMember.FullName,
            staffMember.Email,
            staffMember.IsActive,
            staffMember.CreatedAt,
            staffMember.UpdatedAt
        });
    }
}
