using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public interface ICaseService
{
    Task<Case> CreateAsync(
        Guid organizationId,
        string title,
        string description,
        CancellationToken cancellationToken);

    Task<Case?> GetByIdAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<Case> Items, int TotalCount)> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Case?> ChangeStatusAsync(
        Guid caseId,
        Guid organizationId,
        CaseStatus newStatus);

    Task<IReadOnlyList<CaseStatusHistory>> GetStatusHistoryAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken);
    Task<Case?> AssignAsync(
        Guid caseId,
        Guid organizationId,
        Guid staffMemberId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CaseAssignmentHistory>> GetAssignmentHistoryAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken);
}