using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

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
    IBusinessEndpointAccess businessEndpointAccess) : ControllerBase
{
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
