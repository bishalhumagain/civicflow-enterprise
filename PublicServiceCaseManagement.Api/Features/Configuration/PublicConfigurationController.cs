using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PublicServiceCaseManagement.Api.Configuration;

namespace PublicServiceCaseManagement.Api.Features.Configuration;

[ApiController]
[AllowAnonymous]
[Route("api/config")]
public sealed class PublicConfigurationController(
    IOptions<ApplicationConfiguration> applicationConfiguration) : ControllerBase
{
    [HttpGet("public")]
    public IActionResult GetPublicConfiguration()
    {
        var applicationName = applicationConfiguration.Value.Name;

        return Ok(new
        {
            applicationName = string.IsNullOrWhiteSpace(applicationName)
                ? "CivicFlow Enterprise"
                : applicationName
        });
    }
}
