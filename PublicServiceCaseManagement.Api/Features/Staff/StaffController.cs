using Microsoft.AspNetCore.Mvc;

namespace PublicServiceCaseManagement.Api.Features.Staff;

[ApiController]
[Route("api/staff")]
public sealed class StaffController(
    IStaffService staffService,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateStaffMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var organizationId = Guid.Parse(
            "11111111-1111-1111-1111-111111111111");

        try
        {
            var staffMember = await staffService.CreateAsync(
                organizationId,
                request.FullName,
                request.Email,
                cancellationToken);

            return Created(
                $"/api/staff/{staffMember.Id}",
                new
                {
                    staffMember.Id,
                    staffMember.FullName,
                    staffMember.Email,
                    staffMember.IsActive,
                    staffMember.CreatedAt
                });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }
}