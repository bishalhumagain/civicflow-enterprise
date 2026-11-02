using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Features.Staff.Contracts;
using PublicServiceCaseManagement.Api.Features.Staff.Services;
using PublicServiceCaseManagement.Api.Infrastructure.Pagination;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Staff;

[ApiController]
[Authorize(Policy = "StaffClient")]
[Route("api/staff")]
public sealed class StaffController(
    IStaffService staffService,
    CivicFlowDbContext dbContext,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpGet("platform")]
    [Authorize(Policy = AppRoles.SuperAdminPolicy)]
    public async Task<IActionResult> GetAllPlatformStaff(
        [FromQuery] Guid? organizationId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25,
        [FromQuery, StringLength(200)] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var result = await staffService.GetPageAsync(
            organizationId,
            isActive,
            page,
            pageSize,
            search,
            cancellationToken);

        return Ok(new PagedResponse<object>(
            result.Items.Select(staff => (object)new
        {
            staff.Id,
            staff.FullName,
            staff.Email,
            staff.PhoneNumber,
            staff.OrganizationId,
            staff.IsActive,
            staff.CreatedAt,
            staff.UpdatedAt
        }).ToList(),
            result.Page, result.PageSize, result.TotalCount, result.TotalPages));
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
        [FromQuery] bool? isActive = null,
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25,
        [FromQuery, StringLength(200)] string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationRole =
            await organizationContext.GetOrganizationRoleAsync(cancellationToken);
        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null || organizationRole is null)
            return Forbid();

        var result = await staffService.GetPageAsync(
            organizationId.Value,
            isActive,
            page,
            pageSize,
            search,
            cancellationToken);
        var rolesByEmail = await GetOrganizationRolesAsync(
            organizationId.Value,
            result.Items.Select(staff => staff.Email).ToArray(),
            cancellationToken);

        return Ok(new PagedResponse<object>(
            result.Items.Select(staff => (object)new
        {
            staff.Id,
            staff.FullName,
            staff.Email,
            PhoneNumber = organizationRole == OrganizationRole.OrganizationAdmin
                ? staff.PhoneNumber
                : null,
            OrganizationRole = rolesByEmail.GetValueOrDefault(staff.Email) ?? "Staff",
            staff.IsActive,
            staff.CreatedAt,
            staff.UpdatedAt
        }).ToList(),
            result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }

    private async Task<IReadOnlyDictionary<string, string>> GetOrganizationRolesAsync(
        Guid organizationId,
        string[] emails,
        CancellationToken cancellationToken)
    {
        if (emails.Length == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var normalizedEmails = emails
            .Select(email => email.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var roles = await (
            from user in dbContext.ApplicationUsers.AsNoTracking()
            join membership in dbContext.OrganizationMemberships.AsNoTracking()
                on user.Id equals membership.UserId
            where membership.OrganizationId == organizationId &&
                  user.NormalizedEmail != null &&
                  normalizedEmails.Contains(user.NormalizedEmail)
            select new
            {
                Email = user.Email!,
                Role = membership.Role.ToString()
            })
            .ToListAsync(cancellationToken);

        return roles.ToDictionary(
            item => item.Email,
            item => item.Role,
            StringComparer.OrdinalIgnoreCase);
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