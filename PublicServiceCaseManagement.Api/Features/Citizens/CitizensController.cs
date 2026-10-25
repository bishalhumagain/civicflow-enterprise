using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Features.Citizens.Services;

namespace PublicServiceCaseManagement.Api.Features.Citizens;

[ApiController]
[Authorize(Policy = "CitizenClient")]
[Route("api/citizens")]
public sealed class CitizensController(
    ICitizenProfileService citizenProfileService,
    IAuthenticatedOrganizationResolver organizationResolver) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<CitizenProfileResponse>> GetMe(
        CancellationToken cancellationToken)
    {
        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        var profile = await citizenProfileService.GetByUserIdAsync(
            userId.Value,
            cancellationToken);

        if (profile is null)
            return NotFound();

        return Ok(profile);
    }

    [HttpPost("me")]
    public async Task<ActionResult<CitizenProfileResponse>> CreateOrUpdateMe(
        CreateCitizenProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        var existing = await citizenProfileService.GetByUserIdAsync(
            userId.Value,
            cancellationToken);

        if (existing is not null)
        {
            return await ApplyUpdateAsync(
                userId.Value,
                new UpdateCitizenProfileRequest(
                    request.PhoneNumber,
                    request.NIDNumber,
                    request.CitizenshipNumber,
                    request.Age,
                    request.Gender,
                    request.PermanentAddress,
                    request.TemporaryAddress),
                cancellationToken);
        }

        try
        {
            var created = await citizenProfileService.CreateAsync(
                userId.Value,
                request,
                cancellationToken);

            return CreatedAtAction(nameof(GetMe), created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("me")]
    public async Task<ActionResult<CitizenProfileResponse>> UpdateMe(
        UpdateCitizenProfileRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        return await ApplyUpdateAsync(userId.Value, request, cancellationToken);
    }

    private async Task<ActionResult<CitizenProfileResponse>> ApplyUpdateAsync(
        Guid userId,
        UpdateCitizenProfileRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await citizenProfileService.UpdateAsync(
                userId,
                request,
                cancellationToken);

            return updated is null ? NotFound() : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}
