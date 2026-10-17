using Microsoft.AspNetCore.Mvc;

namespace PublicServiceCaseManagement.Api.Features.Health;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "CivicFlow Enterprise API"
        });
    }
}