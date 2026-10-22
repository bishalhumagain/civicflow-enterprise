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

        var statistics = await dashboardService.GetPlatformStatisticsAsync();
        return Ok(statistics);
    }

    [HttpGet("organization")]
    public async Task<ActionResult<CaseStatistics>> GetOrganizationStatistics(
        [FromQuery] Guid organizationId)
    {
        var statistics = await dashboardService.GetOrganizationStatisticsAsync(organizationId);
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
