using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Platform;

/// <summary>
/// Read-only, cross-organization case access for platform super
/// administrators. The implementation runs inside the platform read context,
/// and the database policies grant SELECT only.
/// </summary>
public interface IPlatformCaseService
{
    Task<PlatformCasePageResponse> GetPageAsync(
        int page,
        int pageSize,
        Guid? organizationId,
        CaseStatus? status,
        CasePriority? priority,
        string? search,
        string? sort,
        CancellationToken cancellationToken = default);

    Task<PlatformCaseDetailResponse?> GetDetailAsync(
        Guid caseId,
        CancellationToken cancellationToken = default);

    Task<CaseDocument?> GetDocumentAsync(
        Guid caseId,
        Guid documentId,
        CancellationToken cancellationToken = default);
}
