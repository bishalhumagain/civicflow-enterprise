using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Features.Cases;
using PublicServiceCaseManagement.Api.Features.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Files;
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
    IFileStorage fileStorage,
    CivicFlowDbContext dbContext) : ControllerBase
{
    private static readonly string[] SortValues =
    [
        "updated_desc",
        "updated_asc",
        "created_desc",
        "created_asc"
    ];

    /// <summary>
    /// Lists only the authenticated citizen's own cases. The identity is
    /// always taken from the server principal; callers cannot select another
    /// citizen by passing an id.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<CitizenCasePage>> GetMyCases(
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery, StringLength(200)] string? search = null,
        [FromQuery] CaseStatus? status = null,
        [FromQuery] string? sort = "updated_desc",
        CancellationToken cancellationToken = default)
    {
        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        var normalizedSort = string.IsNullOrWhiteSpace(sort)
            ? "updated_desc"
            : sort.Trim().ToLowerInvariant();

        if (!SortValues.Contains(normalizedSort, StringComparer.Ordinal))
        {
            return BadRequest(new
            {
                error =
                    "The sort value is invalid. Use updated_desc, updated_asc, " +
                    "created_desc, or created_asc."
            });
        }

        await organizationTransactionContext.BeginCitizenAsync(
            userId.Value,
            cancellationToken);

        var query = dbContext.Cases
            .AsNoTracking()
            .Where(caseEntity => caseEntity.SubmittedByUserId == userId);

        if (status.HasValue)
        {
            query = query.Where(caseEntity => caseEntity.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();

            query = query.Where(caseEntity =>
                caseEntity.ReferenceNumber.ToLower().Contains(term) ||
                caseEntity.Title.ToLower().Contains(term));
        }

        query = normalizedSort switch
        {
            "created_desc" => query
                .OrderByDescending(caseEntity => caseEntity.CreatedAt)
                .ThenBy(caseEntity => caseEntity.Id),
            "created_asc" => query
                .OrderBy(caseEntity => caseEntity.CreatedAt)
                .ThenBy(caseEntity => caseEntity.Id),
            "updated_asc" => query
                .OrderBy(caseEntity => caseEntity.UpdatedAt)
                .ThenBy(caseEntity => caseEntity.Id),
            _ => query
                .OrderByDescending(caseEntity => caseEntity.UpdatedAt)
                .ThenBy(caseEntity => caseEntity.Id)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var pageItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var organizationNames = await GetOrganizationNamesAsync(
            pageItems.Select(item => item.OrganizationId),
            cancellationToken);

        var items = pageItems
            .Select(caseEntity => new CitizenCaseSummary(
                caseEntity.Id,
                caseEntity.ReferenceNumber,
                caseEntity.Title,
                caseEntity.Status,
                caseEntity.OrganizationId,
                organizationNames.GetValueOrDefault(
                    caseEntity.OrganizationId,
                    "Service organization"),
                caseEntity.CreatedAt,
                caseEntity.UpdatedAt))
            .ToList();

        return Ok(new CitizenCasePage(
            items,
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CitizenCaseDetails>> GetMyCase(
        Guid id,
        CancellationToken cancellationToken)
    {
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

        var organizationName = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.Id == caseDetails.OrganizationId)
            .Select(organization => organization.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "Service organization";

        // The status transition itself is always part of the citizen timeline.
        // The explanation is only projected when staff marked it citizen-visible.
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
                history.ChangedAt,
                history.IsVisibleToCitizen ? history.Explanation : null))
            .ToListAsync(cancellationToken);

        // Internal notes are filtered in the database query and never reach
        // the citizen response, including their author identity.
        var updates = await dbContext.CaseUpdates
            .AsNoTracking()
            .Where(update =>
                update.CaseId == caseDetails.Id &&
                update.OrganizationId == caseDetails.OrganizationId &&
                update.IsVisibleToCitizen)
            .OrderBy(update => update.CreatedAt)
            .ThenBy(update => update.Id)
            .Select(update => new CitizenCaseUpdate(
                update.Id,
                update.Kind,
                update.Message,
                update.CreatedAt))
            .ToListAsync(cancellationToken);

        var documents = await dbContext.CaseDocuments
            .AsNoTracking()
            .Where(document =>
                document.CaseId == caseDetails.Id &&
                document.OrganizationId == caseDetails.OrganizationId &&
                document.IsVisibleToCitizen)
            .OrderBy(document => document.UploadedAt)
            .ThenBy(document => document.Id)
            .Select(document => new CitizenCaseDocument(
                document.Id,
                document.OriginalFileName,
                document.ContentType,
                document.SizeBytes,
                document.Sha256Hash,
                document.UploadedAt))
            .ToListAsync(cancellationToken);

        return Ok(new CitizenCaseDetails(
            caseDetails.Id,
            caseDetails.ReferenceNumber,
            caseDetails.Title,
            caseDetails.Description,
            caseDetails.Status,
            caseDetails.OrganizationId,
            organizationName,
            caseDetails.CreatedAt,
            caseDetails.UpdatedAt,
            caseDetails.Status is not (CaseStatus.Closed or CaseStatus.Rejected),
            caseDetails.Status == CaseStatus.AwaitingInformation,
            statusHistory,
            updates,
            documents));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<CitizenCaseSubmissionResponse>> Create(
        [FromForm] CitizenCreateCaseRequest request,
        CancellationToken cancellationToken)
    {
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
                userId.Value,
                isVisibleToCitizen: true);

            documentResponse = ToDocumentResponse(document);
        }

        // A citizen submission is triaged by an organization administrator: the
        // case becomes owned by an admin (or, if no admin staff row exists yet,
        // every active admin is notified) so someone always acts on it.
        await caseService.RouteToOrganizationAdministratorAsync(
            newCase.Id,
            organization.Id,
            cancellationToken);

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

    /// <summary>
    /// Lets the owner respond to an information request. A response to a case
    /// that is awaiting information moves it back into review and records a
    /// citizen-visible timeline entry.
    /// </summary>
    [HttpPost("{id:guid}/responses")]
    public async Task<ActionResult<CitizenCaseUpdate>> Respond(
        Guid id,
        [FromBody] CitizenCaseResponseRequest request,
        CancellationToken cancellationToken)
    {
        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        await organizationTransactionContext.BeginCitizenAsync(
            userId.Value,
            cancellationToken);

        try
        {
            var update = await caseService.AddCitizenResponseAsync(
                id,
                userId.Value,
                request.Message,
                cancellationToken);

            if (update is null)
                return NotFound();

            return CreatedAtAction(
                nameof(GetMyCase),
                new { id },
                new CitizenCaseUpdate(
                    update.Id,
                    update.Kind,
                    update.Message,
                    update.CreatedAt));
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

    [HttpGet("{id:guid}/documents")]
    public async Task<ActionResult<IReadOnlyList<CitizenCaseDocument>>>
        GetDocuments(
            Guid id,
            CancellationToken cancellationToken)
    {
        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        await organizationTransactionContext.BeginCitizenAsync(
            userId.Value,
            cancellationToken);

        try
        {
            var documents = await caseDocumentService.GetVisibleForCitizenAsync(
                id,
                userId.Value,
                cancellationToken);

            return Ok(documents
                .Select(document => new CitizenCaseDocument(
                    document.Id,
                    document.OriginalFileName,
                    document.ContentType,
                    document.SizeBytes,
                    document.Sha256Hash,
                    document.UploadedAt))
                .ToList());
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> GetDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var userId = await organizationResolver.ResolveApplicationUserIdAsync(
            cancellationToken);

        if (userId is null)
            return Unauthorized();

        await organizationTransactionContext.BeginCitizenAsync(
            userId.Value,
            cancellationToken);

        var document = await caseDocumentService.GetVisibleByIdForCitizenAsync(
            id,
            documentId,
            userId.Value,
            cancellationToken);

        if (document is null)
            return NotFound();

        try
        {
            var stream = await fileStorage.OpenReadAsync(
                document.StorageKey,
                cancellationToken);

            return File(
                stream,
                document.ContentType,
                document.OriginalFileName,
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetOrganizationNamesAsync(
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        var ids = organizationIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => ids.Contains(organization.Id))
            .Select(organization => new
            {
                organization.Id,
                organization.Name
            })
            .ToDictionaryAsync(
                organization => organization.Id,
                organization => organization.Name,
                cancellationToken);
    }

    private static CaseDocumentResponse ToDocumentResponse(
        CaseDocument document) =>
        new(
            document.Id,
            document.CaseId,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Sha256Hash,
            document.UploadedAt,
            document.IsVisibleToCitizen);
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

public sealed class CitizenCaseResponseRequest
{
    [Required]
    [StringLength(CaseUpdate.MaximumMessageLength, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;
}

public sealed record CitizenCaseSubmissionResponse(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    Domain.Cases.CaseStatus Status,
    DateTimeOffset CreatedAt,
    CaseDocumentResponse? AdditionalDocument);

public sealed record CitizenCasePage(
    IReadOnlyList<CitizenCaseSummary> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record CitizenCaseSummary(
    Guid Id,
    string ReferenceNumber,
    string Title,
    CaseStatus Status,
    Guid OrganizationId,
    string OrganizationName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CitizenCaseDetails(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    CaseStatus Status,
    Guid OrganizationId,
    string OrganizationName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanRespond,
    bool AwaitingInformation,
    IReadOnlyList<CitizenCaseStatusHistory> StatusHistory,
    IReadOnlyList<CitizenCaseUpdate> Updates,
    IReadOnlyList<CitizenCaseDocument> Documents);

public sealed record CitizenCaseStatusHistory(
    CaseStatus PreviousStatus,
    CaseStatus NewStatus,
    DateTimeOffset ChangedAt,
    string? Explanation);

public sealed record CitizenCaseUpdate(
    Guid Id,
    CaseUpdateKind Kind,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record CitizenCaseDocument(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    DateTimeOffset UploadedAt);
