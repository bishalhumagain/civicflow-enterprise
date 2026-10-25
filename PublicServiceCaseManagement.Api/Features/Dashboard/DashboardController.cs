using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Dashboard;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Dashboard;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppRoles.StaffPolicy)]
public sealed class DashboardController(
    IDashboardService dashboardService,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpGet("platform")]
    public async Task<ActionResult<CaseStatistics>> GetPlatformStatistics(
        CancellationToken cancellationToken)
    {
        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var statistics = await dashboardService.GetPlatformStatisticsAsync(
            cancellationToken);
        return Ok(statistics);
    }

    [HttpGet("platform/organizations")]
    public async Task<ActionResult<
        IReadOnlyList<PlatformOrganizationCaseBreakdown>>>
        GetPlatformOrganizationBreakdown(
            CancellationToken cancellationToken)
    {
        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var breakdown =
            await dashboardService.GetPlatformOrganizationBreakdownAsync(
                cancellationToken);

        return Ok(breakdown);
    }

    [HttpGet("organization")]
    public async Task<ActionResult<CaseStatistics>> GetOrganizationStatistics(
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken)
    {
        var authorizedOrganizationId =
            await organizationContext.GetOrganizationIdAsync(cancellationToken);

        if (authorizedOrganizationId is null ||
            (organizationId.HasValue &&
             organizationId.Value != authorizedOrganizationId.Value))
        {
            return Forbid();
        }

        var statistics = await dashboardService.GetOrganizationStatisticsAsync(
            authorizedOrganizationId.Value,
            cancellationToken);

        return Ok(statistics);
    }

    private async Task<bool> IsPlatformSuperAdminAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        return role == PlatformRole.SuperAdmin;
    }
}
