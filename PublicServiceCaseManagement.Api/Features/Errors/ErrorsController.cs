using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Errors;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Errors;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;

namespace PublicServiceCaseManagement.Api.Features.Errors;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AppRoles.SuperAdminPolicy)]
public sealed class ErrorsController(IErrorLogger errorLogger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApplicationError>>> GetErrors(
        [FromQuery] bool? isHandled = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var errors = await errorLogger.GetErrorsAsync(isHandled, page, pageSize);
        return Ok(errors);
    }

    [HttpGet("{errorId}")]
    public async Task<ActionResult<ApplicationError>> GetError(Guid errorId)
    {
        var error = await errorLogger.GetErrorAsync(errorId);
        if (error == null)
        {
            return NotFound();
        }
        return Ok(error);
    }

    [HttpPatch("{errorId}/mark-handled")]
    public async Task<IActionResult> MarkAsHandled(Guid errorId)
    {
        await errorLogger.MarkAsHandledAsync(errorId);
        return NoContent();
    }
}
