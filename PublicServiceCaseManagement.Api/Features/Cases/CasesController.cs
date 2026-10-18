
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Cases;

[ApiController]
[Route("api/cases")]
public sealed class CasesController(
    ICaseService caseService,
    IHostEnvironment environment,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateCaseResponse>> Create(
        CreateCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var newCase = await caseService.CreateAsync(
            organizationId.Value,
            request.Title,
            request.Description,
            cancellationToken);

        var response = new CreateCaseResponse(
            newCase.Id,
            newCase.ReferenceNumber,
            newCase.Title,
            newCase.Description,
            newCase.Status,
            newCase.Priority,
            newCase.CreatedAt);

        return Created($"/api/cases/{newCase.Id}", response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreateCaseResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await caseService.GetByIdAsync(
            organizationId.Value,
            id,
            cancellationToken);

        if (existingCase is null)
            return NotFound();

        return Ok(new CreateCaseResponse(
            existingCase.Id,
            existingCase.ReferenceNumber,
            existingCase.Title,
            existingCase.Description,
            existingCase.Status,
            existingCase.Priority,
            existingCase.CreatedAt));
    }

    [HttpGet]
    public async Task<ActionResult<PagedCasesResponse>> GetAll(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var result = await caseService.GetPageAsync(
            organizationId.Value,
            page,
            pageSize,
            cancellationToken);

        var response = new PagedCasesResponse(
            result.Items.Select(c => new CreateCaseResponse(
                c.Id,
                c.ReferenceNumber,
                c.Title,
                c.Description,
                c.Status,
                c.Priority,
                c.CreatedAt)).ToList(),
            page,
            pageSize,
            result.TotalCount,
            (int)Math.Ceiling(result.TotalCount / (double)pageSize));

        return Ok(response);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeCaseStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        try
        {
            var caseEntity = await caseService.ChangeStatusAsync(
                id,
                organizationId.Value,
                request.NewStatus);

            if (caseEntity is null)
                return NotFound();

            return Ok(new ChangeCaseStatusResponse(
                caseEntity.Id,
                caseEntity.ReferenceNumber,
                caseEntity.Status,
                caseEntity.UpdatedAt));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new { error = "The requested status is invalid." });
        }
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetStatusHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await caseService.GetByIdAsync(
            organizationId.Value,
            id,
            cancellationToken);

        if (existingCase is null)
            return NotFound();

        var history = await caseService.GetStatusHistoryAsync(
            id,
            organizationId.Value,
            cancellationToken);

        return Ok(history.Select(item => new
        {
            item.Id,
            item.PreviousStatus,
            item.NewStatus,
            item.ChangedAt,
            item.ChangedByUserId
        }));
    }

    [HttpPatch("{id:guid}/assignment")]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        try
        {
            var caseEntity = await caseService.AssignAsync(
                id,
                organizationId.Value,
                request.StaffMemberId,
                cancellationToken);

            if (caseEntity is null)
                return NotFound();

            return Ok(new
            {
                caseEntity.Id,
                caseEntity.ReferenceNumber,
                caseEntity.AssignedToStaffMemberId,
                caseEntity.UpdatedAt
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{id:guid}/assignment-history")]
    public async Task<IActionResult> GetAssignmentHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await caseService.GetByIdAsync(
            organizationId.Value,
            id,
            cancellationToken);

        if (existingCase is null)
            return NotFound();

        var history = await caseService.GetAssignmentHistoryAsync(
            id,
            organizationId.Value,
            cancellationToken);

        return Ok(history.Select(item => new
        {
            item.Id,
            item.PreviousStaffMemberId,
            item.NewStaffMemberId,
            item.AssignedAt
        }));
    }
}
