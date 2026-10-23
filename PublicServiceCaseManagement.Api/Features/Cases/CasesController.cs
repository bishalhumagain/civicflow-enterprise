
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Files;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PublicServiceCaseManagement.Api.Features.Cases;

[ApiController]
[Authorize(Policy = "StaffClient")]
[Route("api/cases")]
public sealed class CasesController(
    ICaseService caseService,
    ICaseDocumentService caseDocumentService,
    IFileStorage fileStorage,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext,
    CivicFlowDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateCaseResponse>> Create(
        CreateCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await CanManageCasesAsync(cancellationToken))
            return Forbid();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var applicationUserId = await organizationContext
            .GetApplicationUserIdAsync(cancellationToken);

        if (applicationUserId is null)
            return Forbid();

        var organizationRole =
            await organizationContext.GetOrganizationRoleAsync(cancellationToken);
        var creatingStaffMemberId = organizationRole == OrganizationRole.CaseWorker
            ? await GetCurrentStaffMemberIdAsync(
                organizationId.Value,
                cancellationToken)
            : null;
        if (organizationRole == OrganizationRole.CaseWorker &&
            creatingStaffMemberId is null)
        {
            return Forbid();
        }

        var newCase = await caseService.CreateAsync(
            organizationId.Value,
            request.Title,
            request.Description,
            cancellationToken,
            applicationUserId.Value);

        if (creatingStaffMemberId is not null)
        {
            newCase = (await caseService.AssignAsync(
                newCase.Id,
                organizationId.Value,
                creatingStaffMemberId.Value,
                cancellationToken,
                applicationUserId.Value,
                CaseAssignmentRole.CaseWorker))!;
        }

        var response = new CreateCaseResponse(
            newCase.Id,
            newCase.ReferenceNumber,
            newCase.Title,
            newCase.Description,
            newCase.Status,
            newCase.Priority,
            newCase.CreatedAt,
            newCase.AssignedToStaffMemberId,
            newCase.AssignedRole);

        return Created($"/api/cases/{newCase.Id}", response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CreateCaseResponse>> Update(
        Guid id,
        [FromBody] UpdateCaseRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await CanManageCasesAsync(cancellationToken))
            return Forbid();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);
        if (organizationId is null)
            return Forbid();

        var existingCase = await GetAccessibleCaseAsync(
            id,
            organizationId.Value,
            cancellationToken);
        if (existingCase is null)
            return NotFound();
        if (!await CanEditCaseAsync(existingCase, cancellationToken))
            return Forbid();

        var staffMemberId =
            await IsOrganizationAdminAsync(cancellationToken)
                ? null
                : await GetCurrentStaffMemberIdAsync(
                    organizationId.Value,
                    cancellationToken);
        var updated = await caseService.UpdateDetailsAsync(
            organizationId.Value,
            id,
            request.Title,
            request.Description,
            cancellationToken,
            staffMemberId);
        if (updated is null)
            return NotFound();

        return Ok(new CreateCaseResponse(
            updated.Id,
            updated.ReferenceNumber,
            updated.Title,
            updated.Description,
            updated.Status,
            updated.Priority,
            updated.CreatedAt,
            updated.AssignedToStaffMemberId,
            updated.AssignedRole));
    }

    /// <summary>
    /// Permanently deletes a case after an explicit confirmation from an
    /// organization administrator.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromBody] ConfirmCaseDeletionRequest request,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsOrganizationAdminAsync(cancellationToken))
            return Forbid();

        if (!request.Confirmed)
            return BadRequest(new
            {
                error = "Organization administrator confirmation is required."
            });

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);
        if (organizationId is null)
            return Forbid();

        var storageKeys = await caseService.DeleteAsync(
            organizationId.Value,
            id,
            cancellationToken);
        if (storageKeys is null)
            return NotFound();

        // Metadata is removed first so a failed blob cleanup cannot leave
        // downloadable documents attached to a deleted case.
        foreach (var storageKey in storageKeys)
        {
            await fileStorage.DeleteAsync(storageKey, cancellationToken);
        }

        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CreateCaseResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var role = await organizationContext.GetOrganizationRoleAsync(
            cancellationToken);
        var staffMemberId = role == OrganizationRole.OrganizationAdmin
            ? null
            : await GetCurrentStaffMemberIdAsync(
                organizationId.Value,
                cancellationToken);
        if (role != OrganizationRole.OrganizationAdmin && staffMemberId is null)
            return NotFound();

        var existingCase = await caseService.GetByIdAsync(
            organizationId.Value,
            id,
            cancellationToken,
            staffMemberId);

        if (existingCase is null)
            return NotFound();

        return Ok(new CreateCaseResponse(
            existingCase.Id,
            existingCase.ReferenceNumber,
            existingCase.Title,
            existingCase.Description,
            existingCase.Status,
            existingCase.Priority,
            existingCase.CreatedAt,
            existingCase.AssignedToStaffMemberId,
            existingCase.AssignedRole));
    }

    [HttpGet]
    public async Task<ActionResult<PagedCasesResponse>> GetAll(
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var role = await organizationContext.GetOrganizationRoleAsync(
            cancellationToken);
        var staffMemberId = role == OrganizationRole.OrganizationAdmin
            ? null
            : await GetCurrentStaffMemberIdAsync(
                organizationId.Value,
                cancellationToken);
        if (role != OrganizationRole.OrganizationAdmin && staffMemberId is null)
        {
            return Ok(new PagedCasesResponse(
                Array.Empty<CreateCaseResponse>(),
                page,
                pageSize,
                0,
                0));
        }

        var result = await caseService.GetPageAsync(
            organizationId.Value,
            page,
            pageSize,
            cancellationToken,
            staffMemberId);

        var response = new PagedCasesResponse(
            result.Items.Select(c => new CreateCaseResponse(
                c.Id,
                c.ReferenceNumber,
                c.Title,
                c.Description,
                c.Status,
                c.Priority,
                c.CreatedAt,
                c.AssignedToStaffMemberId,
                c.AssignedRole)).ToList(),
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
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await CanManageCasesAsync(cancellationToken))
            return Forbid();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await GetAccessibleCaseAsync(
            id,
            organizationId.Value,
            cancellationToken);
        if (existingCase is null)
            return NotFound();
        if (!await CanEditCaseAsync(existingCase, cancellationToken))
            return Forbid();

        var applicationUserId = await organizationContext
            .GetApplicationUserIdAsync(cancellationToken);

        if (applicationUserId is null)
            return Forbid();

        var authorizedStaffMemberId =
            await IsOrganizationAdminAsync(cancellationToken)
                ? null
                : await GetCurrentStaffMemberIdAsync(
                    organizationId.Value,
                    cancellationToken);

        try
        {
            var caseEntity = await caseService.ChangeStatusAsync(
                id,
                organizationId.Value,
                request.NewStatus,
                applicationUserId.Value,
                authorizedStaffMemberId);

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
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await GetAccessibleCaseAsync(
            id,
            organizationId.Value,
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
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsOrganizationAdminAsync(cancellationToken))
            return Forbid();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var applicationUserId = await organizationContext
            .GetApplicationUserIdAsync(cancellationToken);

        if (applicationUserId is null)
            return Forbid();

        try
        {
            var caseEntity = await caseService.AssignAsync(
                id,
                organizationId.Value,
                request.StaffMemberId,
                cancellationToken,
                applicationUserId.Value,
                request.Role);

            if (caseEntity is null)
                return NotFound();

            return Ok(new
            {
                caseEntity.Id,
                caseEntity.ReferenceNumber,
                caseEntity.AssignedToStaffMemberId,
                caseEntity.AssignedRole,
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
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await GetAccessibleCaseAsync(
            id,
            organizationId.Value,
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
            item.PreviousRole,
            item.NewRole,
            item.AssignedAt,
            item.AssignedByUserId
        }));
    }

    [HttpPost("{id:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<CaseDocumentResponse>> UploadDocument(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await GetAccessibleCaseAsync(
            id,
            organizationId.Value,
            cancellationToken);
        if (existingCase is null)
            return NotFound();
        if (!await CanEditCaseAsync(existingCase, cancellationToken))
            return Forbid();

        var applicationUserId = await organizationContext
            .GetApplicationUserIdAsync(cancellationToken);

        if (applicationUserId is null)
            return Forbid();

        var authorizedStaffMemberId =
            await IsOrganizationAdminAsync(cancellationToken)
                ? null
                : await GetCurrentStaffMemberIdAsync(
                    organizationId.Value,
                    cancellationToken);

        try
        {
            var document = await caseDocumentService.UploadAsync(
                organizationId.Value,
                id,
                file,
                cancellationToken,
                applicationUserId.Value,
                authorizedStaffMemberId);

            return CreatedAtAction(
                nameof(GetDocument),
                new
                {
                    id,
                    documentId = document.Id
                },
                ToDocumentResponse(document));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (InvalidDataException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{id:guid}/documents")]
    public async Task<IActionResult> GetDocuments(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await GetAccessibleCaseAsync(
            id,
            organizationId.Value,
            cancellationToken);
        if (existingCase is null)
            return NotFound();

        try
        {
            var documents = await caseDocumentService.GetAllAsync(
                organizationId.Value,
                id,
                cancellationToken);

            return Ok(documents.Select(ToDocumentResponse).ToList());
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }

    [HttpGet("{id:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> GetDocument(
        Guid id,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        var organizationId = await organizationContext.GetOrganizationIdAsync(
            cancellationToken);

        if (organizationId is null)
            return Forbid();

        var existingCase = await GetAccessibleCaseAsync(
            id,
            organizationId.Value,
            cancellationToken);
        if (existingCase is null)
            return NotFound();

        var document = await caseDocumentService.GetByIdAsync(
            organizationId.Value,
            id,
            documentId,
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

    private static CaseDocumentResponse ToDocumentResponse(
        Domain.Cases.CaseDocument document) =>
        new(
            document.Id,
            document.CaseId,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Sha256Hash,
            document.UploadedAt);

    private async Task<bool> CanManageCasesAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetOrganizationRoleAsync(
            cancellationToken);

        return role is OrganizationRole.CaseWorker
            or OrganizationRole.OrganizationAdmin;
    }

    private async Task<bool> IsOrganizationAdminAsync(
        CancellationToken cancellationToken) =>
        await organizationContext.GetOrganizationRoleAsync(cancellationToken)
            == OrganizationRole.OrganizationAdmin;

    private async Task<Guid?> GetCurrentStaffMemberIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var userId = await organizationContext.GetApplicationUserIdAsync(
            cancellationToken);
        if (userId is null)
            return null;

        var email = await dbContext.ApplicationUsers
            .AsNoTracking()
            .Where(user => user.Id == userId.Value && user.IsActive)
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (email is null)
            return null;

        return await dbContext.StaffMembers
            .AsNoTracking()
            .Where(staff => staff.OrganizationId == organizationId &&
                            staff.IsActive &&
                            staff.Email.ToUpper() == email.ToUpper())
            .Select(staff => (Guid?)staff.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Domain.Cases.Case?> GetAccessibleCaseAsync(
        Guid id,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetOrganizationRoleAsync(
            cancellationToken);
        if (role == OrganizationRole.OrganizationAdmin)
        {
            return await caseService.GetByIdAsync(
                organizationId,
                id,
                cancellationToken);
        }

        var staffMemberId = await GetCurrentStaffMemberIdAsync(
            organizationId,
            cancellationToken);
        return staffMemberId is null
            ? null
            : await caseService.GetByIdAsync(
                organizationId,
                id,
                cancellationToken,
                staffMemberId);
    }

    private async Task<bool> CanEditCaseAsync(
        Domain.Cases.Case caseEntity,
        CancellationToken cancellationToken)
    {
        if (await IsOrganizationAdminAsync(cancellationToken))
            return true;

        var staffMemberId = await GetCurrentStaffMemberIdAsync(
            caseEntity.OrganizationId,
            cancellationToken);
        return await organizationContext.GetOrganizationRoleAsync(
                   cancellationToken) == OrganizationRole.CaseWorker &&
               staffMemberId == caseEntity.AssignedToStaffMemberId &&
               caseEntity.AssignedRole == CaseAssignmentRole.CaseWorker;
    }
}
