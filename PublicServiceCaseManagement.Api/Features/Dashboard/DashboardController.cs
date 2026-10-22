using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Dashboard;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Api.Features.Dashboard;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController(IDashboardService dashboardService) : ControllerBase
{
    [HttpGet("platform")]
    [Authorize(Policy = AppRoles.SuperAdminPolicy)]
    public async Task<ActionResult<CaseStatistics>> GetPlatformStatistics()
    {
        var statistics = await dashboardService.GetPlatformStatisticsAsync();
        return Ok(statistics);
    }

    [HttpGet("organization")]
    [Authorize(Policy = AppRoles.StaffPolicy)]
    public async Task<ActionResult<CaseStatistics>> GetOrganizationStatistics([FromQuery] Guid organizationId)
    {
        var statistics = await dashboardService.GetOrganizationStatisticsAsync(organizationId);
        return Ok(statistics);
    }
}
