using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Features.Organizations.Contracts;
using PublicServiceCaseManagement.Api.Features.Organizations.Services;
using PublicServiceCaseManagement.Api.Infrastructure.Pagination;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

/// <summary>Organization directory and authenticated staff membership endpoints.</summary>
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
        CancellationToken cancellationToken = default)
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
    public async Task<ActionResult<PagedResponse<OrganizationResponse>>> GetAll(
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 25,
        [FromQuery, StringLength(200)] string? search = null,
        [FromQuery] Guid? organizationTypeId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var isPlatformAdmin = await IsPlatformSuperAdminAsync(cancellationToken);
        Guid? managedByUserId = null;
        if (!isPlatformAdmin)
        {
            managedByUserId = await organizationContext.GetApplicationUserIdAsync(
                cancellationToken);
            if (managedByUserId is null)
                return Forbid();
        }

        var result = await organizationService.GetPageAsync(
            managedByUserId,
            page,
            pageSize,
            search,
            organizationTypeId,
            isActive,
            cancellationToken);
        var items = await ToResponsesAsync(result.Items, cancellationToken);
        return Ok(new PagedResponse<OrganizationResponse>(
            items, result.Page, result.PageSize, result.TotalCount,
            result.TotalPages));
    }

    /// <summary>Returns active organization memberships belonging to the authenticated staff account.</summary>
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

        // Staff rows are tenant isolated, so deletion must run in the organization transaction.
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