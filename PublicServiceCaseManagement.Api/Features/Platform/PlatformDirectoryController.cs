using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Pagination;
using PublicServiceCaseManagement.Api.Features.Platform.Contracts;
using PublicServiceCaseManagement.Api.Features.Platform.Services;

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
    public async Task<ActionResult<PagedResponse<PlatformUserSummary>>>
        GetUsers(
            [FromQuery, Range(1, 1_000_000)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 25,
            [FromQuery, StringLength(200)] string? search = null,
            [FromQuery] bool? isStaff = null,
            [FromQuery] bool? isCitizen = null,
            [FromQuery, StringLength(20)] string? sort = null,
            CancellationToken cancellationToken = default)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var users = await platformDirectoryService.GetUsersAsync(
            page,
            pageSize,
            search,
            isStaff,
            isCitizen,
            sort,
            cancellationToken: cancellationToken);

        return Ok(users);
    }

    [HttpGet("api/platform/staff")]
    public async Task<ActionResult<PagedResponse<PlatformStaffSummary>>>
        GetStaff(
            [FromQuery, Range(1, 1_000_000)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 25,
            [FromQuery] Guid? organizationId = null,
            [FromQuery] bool? isActive = null,
            [FromQuery, StringLength(200)] string? search = null,
            CancellationToken cancellationToken = default)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var staff = await platformDirectoryService.GetStaffAsync(
            page,
            pageSize,
            organizationId,
            isActive,
            search,
            cancellationToken);

        return Ok(staff);
    }

    [HttpPut("api/platform/staff/{userId:guid}")]
    public async Task<ActionResult<PlatformUserSummary>> UpdateStaffAccount(
        Guid userId,
        [FromBody] UpdatePlatformStaffRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            var user = await platformDirectoryService.UpdateStaffAccountAsync(
                userId,
                request,
                cancellationToken);

            return Ok(user);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }

    [HttpGet("api/platform/citizens")]
    public async Task<ActionResult<PagedResponse<PlatformCitizenSummary>>>
        GetCitizens(
            [FromQuery, Range(1, 1_000_000)] int page = 1,
            [FromQuery, Range(1, 100)] int pageSize = 25,
            [FromQuery, StringLength(200)] string? search = null,
            [FromQuery, StringLength(20)] string? sort = null,
            CancellationToken cancellationToken = default)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var citizens = await platformDirectoryService.GetCitizensAsync(
            page,
            pageSize,
            search,
            sort,
            cancellationToken: cancellationToken);

        return Ok(citizens);
    }

    [HttpPut("api/platform/citizens/{userId:guid}")]
    public async Task<ActionResult<PlatformCitizenSummary>> UpdateCitizen(
        Guid userId,
        [FromBody] UpdatePlatformCitizenRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            var citizen = await platformDirectoryService.UpdateCitizenAsync(
                userId,
                request,
                cancellationToken);

            return Ok(citizen);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }

    [HttpPatch("api/platform/citizens/{userId:guid}/activate")]
    public Task<IActionResult> ActivateCitizen(
        Guid userId,
        CancellationToken cancellationToken) =>
        SetCitizenActiveStatus(userId, true, cancellationToken);

    [HttpPatch("api/platform/citizens/{userId:guid}/deactivate")]
    public Task<IActionResult> DeactivateCitizen(
        Guid userId,
        CancellationToken cancellationToken) =>
        SetCitizenActiveStatus(userId, false, cancellationToken);

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

    private async Task<IActionResult> SetCitizenActiveStatus(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            await platformDirectoryService.SetUserActiveStatusAsync(
                userId,
                isActive,
                cancellationToken);

            return NoContent();
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
