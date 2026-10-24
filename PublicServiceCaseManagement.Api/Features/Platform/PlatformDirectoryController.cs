using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Platform;

/// <summary>
/// The platform-wide account and staff directory. These endpoints are the
/// only place where data across organizations is visible, so they are
/// restricted to platform administrators.
/// </summary>
[ApiController]
[Authorize(Policy = AppRoles.StaffPolicy)]
public sealed class PlatformDirectoryController(
    IPlatformDirectoryService platformDirectoryService,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpGet("api/platform/users")]
    public async Task<ActionResult<IReadOnlyList<PlatformUserSummary>>>
        GetUsers(CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var users = await platformDirectoryService.GetUsersAsync(
            cancellationToken);

        return Ok(users);
    }

    [HttpGet("api/platform/staff")]
    public async Task<ActionResult<IReadOnlyList<PlatformStaffSummary>>>
        GetStaff(CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var staff = await platformDirectoryService.GetStaffAsync(
            cancellationToken);

        return Ok(staff);
    }

    [HttpGet("api/platform/overview")]
    public async Task<ActionResult<PlatformOverviewResponse>> GetOverview(
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var overview = await platformDirectoryService.GetOverviewAsync(
            cancellationToken);

        return Ok(overview);
    }

    [HttpPatch("api/platform/users/{userId:guid}/activate")]
    public async Task<IActionResult> ActivateUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        await platformDirectoryService.SetUserActiveStatusAsync(
            userId,
            true,
            cancellationToken);

        return NoContent();
    }

    [HttpPatch("api/platform/users/{userId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateUser(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        await platformDirectoryService.SetUserActiveStatusAsync(
            userId,
            false,
            cancellationToken);

        return NoContent();
    }

    [HttpPatch("api/platform/users/{userId:guid}/platform-role")]
    public async Task<IActionResult> ChangePlatformRole(
        Guid userId,
        [FromBody] ChangePlatformRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            await platformDirectoryService.ChangePlatformRoleAsync(
                userId,
                request.PlatformRole,
                cancellationToken);

            return NoContent();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }

    private async Task<bool> IsPlatformSuperAdminAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        return role == PlatformRole.SuperAdmin;
    }
}
