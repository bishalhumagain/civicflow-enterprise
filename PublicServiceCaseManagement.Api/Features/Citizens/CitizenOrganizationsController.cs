using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Api.Features.Citizens;

[ApiController]
[Authorize(Policy = AppRoles.CitizenPolicy)]
[Route("api/citizens/organizations")]
public sealed class CitizenOrganizationsController(
    IOrganizationService organizationService,
    IBusinessEndpointAccess businessEndpointAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CitizenOrganizationOption>>> Get(
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizations =
            await organizationService.GetActiveOrganizationsForCitizensAsync(
                cancellationToken);

        return Ok(organizations);
    }
}
