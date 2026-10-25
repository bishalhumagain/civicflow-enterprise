using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Features.Organizations.Contracts;
using PublicServiceCaseManagement.Api.Features.Organizations.Services;

namespace PublicServiceCaseManagement.Api.Features.Citizens;

[ApiController]
[Authorize(Policy = AppRoles.CitizenPolicy)]
[Route("api/citizens/organizations")]
public sealed class CitizenOrganizationsController(
    IOrganizationService organizationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CitizenOrganizationOption>>> Get(
        CancellationToken cancellationToken)
    {
        var organizations =
            await organizationService.GetActiveOrganizationsForCitizensAsync(
                cancellationToken);

        return Ok(organizations);
    }
}
