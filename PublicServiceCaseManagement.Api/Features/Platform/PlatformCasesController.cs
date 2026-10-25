using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Users;
using PublicServiceCaseManagement.Api.Infrastructure.Authorization;
using PublicServiceCaseManagement.Api.Infrastructure.Files;
using PublicServiceCaseManagement.Api.Infrastructure.Identity;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Platform;

/// <summary>
/// Cross-organization case inspection for platform super administrators.
/// These endpoints are read-only: status, assignment, and communication
/// changes remain scoped to the owning organization.
/// </summary>
[ApiController]
[Authorize(Policy = AppRoles.StaffPolicy)]
public sealed class PlatformCasesController(
    IPlatformCaseService platformCaseService,
    IFileStorage fileStorage,
    IBusinessEndpointAccess businessEndpointAccess,
    ICurrentOrganizationContext organizationContext) : ControllerBase
{
    [HttpGet("api/platform/cases")]
    public async Task<ActionResult<PlatformCasePageResponse>> GetCases(
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        [FromQuery] Guid? organizationId = null,
        [FromQuery] CaseStatus? status = null,
        [FromQuery] CasePriority? priority = null,
        [FromQuery, StringLength(200)] string? search = null,
        [FromQuery, StringLength(20)] string? sort = null,
        CancellationToken cancellationToken = default)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        try
        {
            var result = await platformCaseService.GetPageAsync(
                page,
                pageSize,
                organizationId,
                status,
                priority,
                search,
                sort,
                cancellationToken);

            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("api/platform/cases/{caseId:guid}")]
    public async Task<ActionResult<PlatformCaseDetailResponse>> GetCase(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var detail = await platformCaseService.GetDetailAsync(
            caseId,
            cancellationToken);

        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("api/platform/cases/{caseId:guid}/documents/{documentId:guid}")]
    public async Task<IActionResult> GetDocument(
        Guid caseId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (!businessEndpointAccess.IsEnabled)
            return NotFound();

        if (!await IsPlatformSuperAdminAsync(cancellationToken))
            return Forbid();

        var document = await platformCaseService.GetDocumentAsync(
            caseId,
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

    private async Task<bool> IsPlatformSuperAdminAsync(
        CancellationToken cancellationToken)
    {
        var role = await organizationContext.GetPlatformRoleAsync(
            cancellationToken);

        return role == PlatformRole.SuperAdmin;
    }
}
