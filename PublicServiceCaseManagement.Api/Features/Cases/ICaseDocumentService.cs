using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public interface ICaseDocumentService
{
    Task<CaseDocument> UploadAsync(
        Guid organizationId,
        Guid caseId,
        IFormFile file,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CaseDocument>> GetAllAsync(
        Guid organizationId,
        Guid caseId,
        CancellationToken cancellationToken);

    Task<CaseDocument?> GetByIdAsync(
        Guid organizationId,
        Guid caseId,
        Guid documentId,
        CancellationToken cancellationToken);
}
