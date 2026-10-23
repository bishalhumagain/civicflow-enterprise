using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
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
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 200)] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var errors = await errorLogger.GetErrorsAsync(
            isHandled,
            page,
            pageSize,
            cancellationToken);
        return Ok(errors);
    }

    [HttpGet("{errorId:guid}")]
    public async Task<ActionResult<ApplicationError>> GetError(
        Guid errorId,
        CancellationToken cancellationToken)
    {
        var error = await errorLogger.GetErrorAsync(
            errorId,
            cancellationToken);
        if (error == null)
        {
            return NotFound();
        }

        return Ok(error);
    }

    [HttpPatch("{errorId:guid}/mark-handled")]
    public async Task<IActionResult> MarkAsHandled(
        Guid errorId,
        CancellationToken cancellationToken)
    {
        var updated = await errorLogger.MarkAsHandledAsync(
            errorId,
            cancellationToken);
        return updated ? NoContent() : NotFound();
    }
}