using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public interface ICaseDocumentService
{
    Task<CaseDocument> UploadAsync(
        Guid organizationId,
        Guid caseId,
        IFormFile file,
        CancellationToken cancellationToken,
        Guid? uploadedByUserId = null,
        Guid? authorizedStaffMemberId = null,
        bool isVisibleToCitizen = false);

    Task<IReadOnlyList<CaseDocument>> GetVisibleForCitizenAsync(
        Guid caseId,
        Guid citizenUserId,
        CancellationToken cancellationToken);

    Task<CaseDocument?> GetVisibleByIdForCitizenAsync(
        Guid caseId,
        Guid documentId,
        Guid citizenUserId,
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
