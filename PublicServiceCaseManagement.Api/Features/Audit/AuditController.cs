using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Audit;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Audit;

/// <summary>
/// Read access to the audit trail. Platform administrators see the whole
/// platform; organization administrators only see their own organization.
/// </summary>
[ApiController]
[Authorize(Policy = AppRoles.StaffPolicy)]
public sealed class AuditController(
    IAuditQueryService auditQueryService,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpGet("api/platform/audit")]
    public async Task<ActionResult<AuditEventPageResponse>> GetPlatformAudit(
        [FromQuery] AuditQuery query,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var page = await auditQueryService.GetPlatformEventsAsync(
            query,
            cancellationToken);

        return Ok(page);
    }

    [HttpGet("api/platform/audit/categories")]
    public ActionResult<IReadOnlyList<string>> GetPlatformAuditCategories()
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        return Ok(Enum.GetNames<AuditCategory>());
    }

    [HttpGet("api/organizations/{organizationId:guid}/audit")]
    public async Task<ActionResult<AuditEventPageResponse>> GetOrganizationAudit(
        Guid organizationId,
        [FromQuery] AuditQuery query,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await CanAdministerOrganizationAsync(organizationId, cancellationToken))
            return Forbid();

        var page = await auditQueryService.GetOrganizationEventsAsync(
            organizationId,
            query,
            cancellationToken);

        return Ok(page);
    }

    private async Task<bool> IsPlatformSuperAdminAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        return role == PlatformRole.SuperAdmin;
    }

    private async Task<bool> CanAdministerOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var platformRole = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        if (platformRole == PlatformRole.SuperAdmin)
            return true;

        var currentOrganizationId =
            await organizationContext.GetOrganizationIdAsync(cancellationToken);

        if (currentOrganizationId != organizationId)
            return false;

        var organizationRole =
            await organizationContext.GetOrganizationRoleAsync(cancellationToken);

        return organizationRole == OrganizationRole.OrganizationAdmin;
    }
}
