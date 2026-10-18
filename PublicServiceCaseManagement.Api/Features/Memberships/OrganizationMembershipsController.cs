
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Memberships;

[ApiController]
[Route("api/organizations/{organizationId:guid}/memberships")]
public sealed class OrganizationMembershipsController(
    IOrganizationMembershipService membershipService,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrganizationMembershipResponse>> Create(
        Guid organizationId,
        [FromBody] CreateOrganizationMembershipRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = "The organization role is invalid." });

        try
        {
            var membership = await membershipService.CreateAsync(
                request.UserId,
                organizationId,
                role,
                cancellationToken);

            var response = ToResponse(membership);

            return CreatedAtAction(
                nameof(GetById),
                new { organizationId, membershipId = membership.Id },
                response);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrganizationMembershipResponse>>> GetAll(
        Guid organizationId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var memberships = await membershipService.GetAllAsync(
            organizationId,
            isActive,
            cancellationToken);

        return Ok(memberships.Select(ToResponse).ToList());
    }

    [HttpGet("{membershipId:guid}")]
    public async Task<ActionResult<OrganizationMembershipResponse>> GetById(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var membership = await membershipService.GetByIdAsync(
            organizationId,
            membershipId,
            cancellationToken);

        if (membership is null)
            return NotFound();

        return Ok(ToResponse(membership));
    }

    [HttpPut("{membershipId:guid}/role")]
    public async Task<ActionResult<OrganizationMembershipResponse>> ChangeRole(
        Guid organizationId,
        Guid membershipId,
        [FromBody] ChangeOrganizationMembershipRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        if (!TryParseRole(request.Role, out var role))
            return BadRequest(new { error = "The organization role is invalid." });

        try
        {
            var membership = await membershipService.ChangeRoleAsync(
                organizationId,
                membershipId,
                role,
                cancellationToken);

            if (membership is null)
                return NotFound();

            return Ok(ToResponse(membership));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPatch("{membershipId:guid}/deactivate")]
    public Task<ActionResult<OrganizationMembershipResponse>> Deactivate(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        SetActiveStatus(
            organizationId,
            membershipId,
            false,
            cancellationToken);

    [HttpPatch("{membershipId:guid}/reactivate")]
    public Task<ActionResult<OrganizationMembershipResponse>> Reactivate(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        SetActiveStatus(
            organizationId,
            membershipId,
            true,
            cancellationToken);

    private async Task<ActionResult<OrganizationMembershipResponse>> SetActiveStatus(
        Guid organizationId,
        Guid membershipId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var membership = await membershipService.SetActiveStatusAsync(
            organizationId,
            membershipId,
            isActive,
            cancellationToken);

        if (membership is null)
            return NotFound();

        return Ok(ToResponse(membership));
    }

    private static bool TryParseRole(
        string? value,
        out OrganizationRole role)
    {
        role = default;

        return !string.IsNullOrWhiteSpace(value) &&
               Enum.TryParse(value, true, out role) &&
               Enum.IsDefined(role);
    }

    private static OrganizationMembershipResponse ToResponse(
        Domain.Organizations.OrganizationMembership membership) =>
        new(
            membership.Id,
            membership.UserId,
            membership.OrganizationId,
            membership.Role.ToString(),
            membership.IsActive,
            membership.CreatedAt,
            membership.UpdatedAt);
}
