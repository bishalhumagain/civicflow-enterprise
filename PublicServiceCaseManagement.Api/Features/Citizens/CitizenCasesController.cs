using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Citizens;

[ApiController]
[Authorize(Policy = AppRoles.CitizenPolicy)]
[Route("api/citizens/cases")]
public sealed class CitizenCasesController(
    ICaseService caseService,
    ICaseDocumentService caseDocumentService,
    IOrganizationService organizationService,
    IAuthenticatedOrganizationResolver organizationResolver,
    IOrganizationTransactionContext organizationTransactionContext,
    IBusinessEndpointAccess businessEndpointAccess,
    CivicFlowDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CitizenCaseSummary>>> GetMyCases(
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        await organizationTransactionContext.BeginCitizenAsync(
            userId.Value,
            cancellationToken);

        var cases = await dbContext.Cases
            .AsNoTracking()
            .Where(caseEntity => caseEntity.SubmittedByUserId == userId)
            .OrderByDescending(caseEntity => caseEntity.CreatedAt)
            .ThenBy(caseEntity => caseEntity.Id)
            .Select(caseEntity => new CitizenCaseSummary(
                caseEntity.Id,
                caseEntity.ReferenceNumber,
                caseEntity.Title,
                caseEntity.Status,
                caseEntity.CreatedAt,
                caseEntity.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(cases);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CitizenCaseDetails>> GetMyCase(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        await organizationTransactionContext.BeginCitizenAsync(
            userId.Value,
            cancellationToken);

        var caseDetails = await dbContext.Cases
            .AsNoTracking()
            .Where(caseEntity =>
                caseEntity.Id == id &&
                caseEntity.SubmittedByUserId == userId)
            .Select(caseEntity => new
            {
                caseEntity.Id,
                caseEntity.OrganizationId,
                caseEntity.ReferenceNumber,
                caseEntity.Title,
                caseEntity.Description,
                caseEntity.Status,
                caseEntity.CreatedAt,
                caseEntity.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (caseDetails is null)
            return NotFound();

        var statusHistory = await dbContext.CaseStatusHistories
            .AsNoTracking()
            .Where(history =>
                history.CaseId == caseDetails.Id &&
                history.OrganizationId == caseDetails.OrganizationId)
            .OrderBy(history => history.ChangedAt)
            .ThenBy(history => history.Id)
            .Select(history => new CitizenCaseStatusHistory(
                history.PreviousStatus,
                history.NewStatus,
                history.ChangedAt))
            .ToListAsync(cancellationToken);

        return Ok(new CitizenCaseDetails(
            caseDetails.Id,
            caseDetails.ReferenceNumber,
            caseDetails.Title,
            caseDetails.Description,
            caseDetails.Status,
            caseDetails.CreatedAt,
            caseDetails.UpdatedAt,
            statusHistory));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<CitizenCaseSubmissionResponse>> Create(
        [FromForm] CitizenCreateCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        if (request.AdditionalDocument is not null)
        {
            try
            {
                CaseDocumentService.ValidateUpload(request.AdditionalDocument);
            }
            catch (InvalidDataException exception)
            {
                return BadRequest(new { error = exception.Message });
            }
        }

        var organization = await organizationService.GetByIdAsync(
            request.OrganizationId,
            cancellationToken);

        if (organization is null || !organization.IsActive)
            return BadRequest(new { error = "Select an active organization." });

        await organizationTransactionContext.BeginAsync(
            organization.Id,
            cancellationToken);

        Domain.Cases.Case newCase;
        try
        {
            newCase = await caseService.CreateAsync(
                organization.Id,
                request.Title,
                request.Description,
                cancellationToken,
                userId.Value);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }

        CaseDocumentResponse? documentResponse = null;
        if (request.AdditionalDocument is not null)
        {
            var document = await caseDocumentService.UploadAsync(
                organization.Id,
                newCase.Id,
                request.AdditionalDocument,
                cancellationToken,
                userId.Value);

            documentResponse = new CaseDocumentResponse(
                document.Id,
                document.CaseId,
                document.OriginalFileName,
                document.ContentType,
                document.SizeBytes,
                document.Sha256Hash,
                document.UploadedAt);
        }

        var response = new CitizenCaseSubmissionResponse(
            newCase.Id,
            newCase.ReferenceNumber,
            newCase.Title,
            newCase.Description,
            newCase.Status,
            newCase.CreatedAt,
            documentResponse);

        return Created($"/api/citizens/cases/{newCase.Id}", response);
    }
}

public sealed class CitizenCreateCaseRequest
{
    [Required]
    public Guid OrganizationId { get; init; }

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; init; } = string.Empty;

    [Required]
    [StringLength(5000, MinimumLength = 10)]
    public string Description { get; init; } = string.Empty;

    public IFormFile? AdditionalDocument { get; init; }
}

public sealed record CitizenCaseSubmissionResponse(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    Domain.Cases.CaseStatus Status,
    DateTimeOffset CreatedAt,
    CaseDocumentResponse? AdditionalDocument);

public sealed record CitizenCaseSummary(
    Guid Id,
    string ReferenceNumber,
    string Title,
    CaseStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CitizenCaseDetails(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    CaseStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CitizenCaseStatusHistory> StatusHistory);

public sealed record CitizenCaseStatusHistory(
    CaseStatus PreviousStatus,
    CaseStatus NewStatus,
    DateTimeOffset ChangedAt);
