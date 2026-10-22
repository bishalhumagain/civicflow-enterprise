
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Api.Features.Staff;

[ApiController]
[Authorize(Policy = "StaffClient")]
[Route("api/staff")]
public sealed class StaffController(
    IStaffService staffService,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    // Platform admin endpoint to manage staff across all organizations
    [HttpGet("platform")]
    [Authorize(Policy = AppRoles.SuperAdminPolicy)]
    public async Task<IActionResult> GetAllPlatformStaff(
        [FromQuery] Guid? organizationId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var staffMembers = await staffService.GetAllPlatformStaffAsync(
            organizationId,
            isActive,
            cancellationToken);

        return Ok(staffMembers.Select(staff => new
        {
            staff.Id,
            staff.FullName,
            staff.Email,
            staff.PhoneNumber,
            staff.OrganizationId,
            staff.IsActive,
            staff.CreatedAt,
            staff.UpdatedAt
        }));
    }

    [HttpPatch("platform/{id:guid}/deactivate")]
    [Authorize(Policy = AppRoles.SuperAdminPolicy)]
    public async Task<IActionResult> DeactivatePlatformStaff(
        Guid id,
        [FromQuery] Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var staffMember = await staffService.SetActiveStatusAsync(
            organizationId,
            id,
            false,
            cancellationToken);

        if (staffMember is null)
            return NotFound();

        return Ok(new
        {
            staffMember.Id,
            staffMember.FullName,
            staffMember.Email,
            staffMember.PhoneNumber,
            staffMember.IsActive,
            staffMember.UpdatedAt
        });
    }

    [HttpPatch("platform/{id:guid}/reactivate")]
    [Authorize(Policy = AppRoles.SuperAdminPolicy)]
    public async Task<IActionResult> ReactivatePlatformStaff(
        Guid id,
        [FromQuery] Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var staffMember = await staffService.SetActiveStatusAsync(
            organizationId,
            id,
            true,
            cancellationToken);

        if (staffMember is null)
            return NotFound();

        return Ok(new
        {
            staffMember.Id,
            staffMember.FullName,
            staffMember.Email,
            staffMember.PhoneNumber,
            staffMember.IsActive,
            staffMember.UpdatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsOrganizationAdminAsync(cancellationToken))
            return Forbid();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        try
        {
            var staffMember = await staffService.CreateAsync(
                organizationId.Value,
                request.FullName,
                request.Email,
                request.PhoneNumber,
                cancellationToken);

            return Created(
                $"/api/staff/{staffMember.Id}",
                new
                {
                    staffMember.Id,
                    staffMember.FullName,
                    staffMember.Email,
                    staffMember.PhoneNumber,
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
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var staffMembers = await staffService.GetAllAsync(
            organizationId.Value,
            isActive,
            cancellationToken);

        return Ok(staffMembers.Select(staff => new
        {
            staff.Id,
            staff.FullName,
            staff.Email,
            staff.PhoneNumber,
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

    [HttpPatch("{id:guid}/phone")]
    public async Task<IActionResult> UpdatePhone(
        Guid id,
        [FromBody] UpdateStaffPhoneRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsOrganizationAdminAsync(cancellationToken))
            return Forbid();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        try
        {
            var staffMember = await staffService.UpdatePhoneNumberAsync(
                organizationId.Value,
                id,
                request.PhoneNumber,
                cancellationToken);

            if (staffMember is null)
                return NotFound();

            return Ok(new
            {
                staffMember.Id,
                staffMember.FullName,
                staffMember.Email,
                staffMember.PhoneNumber,
                staffMember.IsActive,
                staffMember.UpdatedAt
            });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private async Task<IActionResult> SetActiveStatus(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsOrganizationAdminAsync(cancellationToken))
            return Forbid();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var staffMember = await staffService.SetActiveStatusAsync(
            organizationId.Value,
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
            staffMember.PhoneNumber,
            staffMember.IsActive,
            staffMember.CreatedAt,
            staffMember.UpdatedAt
        });
    }

    private async Task<bool> IsOrganizationAdminAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetOrganizationRoleAsync(
            cancellationToken);

        return role is OrganizationRole.OrganizationAdmin;
    }
}
