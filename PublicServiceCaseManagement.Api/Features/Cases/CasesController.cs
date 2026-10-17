
using PublicServiceCaseManagement.Api.Domain.Cases;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PublicServiceCaseManagement.Api.Features.Cases;

[ApiController]
[Route("api/cases")]
public sealed class CasesController(
    ICaseService caseService,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateCaseResponse>> Create(
        CreateCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var organizationId = Guid.Parse(
            "11111111-1111-1111-1111-111111111111");

        var newCase = await caseService.CreateAsync(
            organizationId,
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
        {
            return NotFound();
        }

        var organizationId = Guid.Parse(
            "11111111-1111-1111-1111-111111111111");

        var existingCase = await caseService.GetByIdAsync(
            organizationId,
            id,
            cancellationToken);

        if (existingCase is null)
        {
            return NotFound();
        }

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
    {
        return NotFound();
    }

    var organizationId = Guid.Parse(
        "11111111-1111-1111-1111-111111111111");

    var result = await caseService.GetPageAsync(
        organizationId,
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
}

