using PublicServiceCaseManagement.Api.Domain.Cases;

namespace PublicServiceCaseManagement.Api.Features.Cases;

public interface ICaseService
{
    Task<Case> CreateAsync(
        Guid organizationId,
        string title,
        string description,
        CancellationToken cancellationToken,
        Guid? submittedByUserId = null);

    Task<Case?> GetByIdAsync(
        Guid organizationId,
        Guid id,
        CancellationToken cancellationToken,
        Guid? assignedToStaffMemberId = null);

    Task<Case?> UpdateDetailsAsync(
        Guid organizationId,
        Guid caseId,
        string title,
        string description,
        CancellationToken cancellationToken,
        Guid? authorizedStaffMemberId = null);

    Task<IReadOnlyList<string>?> DeleteAsync(
        Guid organizationId,
        Guid caseId,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<Case> Items, int TotalCount)> GetPageAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken cancellationToken,
        Guid? assignedToStaffMemberId = null,
        CaseStatus? status = null,
        CasePriority? priority = null,
        string? search = null);

    Task<Case?> ChangeStatusAsync(
        Guid caseId,
        Guid organizationId,
        CaseStatus newStatus,
        Guid? changedByUserId = null,
        Guid? authorizedStaffMemberId = null,
        CancellationToken cancellationToken = default,
        string? explanation = null,
        bool explanationVisibleToCitizen = false);

    Task<CaseUpdate?> AddUpdateAsync(
        Guid caseId,
        Guid organizationId,
        CaseUpdateKind kind,
        string message,
        Guid authorUserId,
        Guid? authorizedStaffMemberId = null,
        CancellationToken cancellationToken = default);

    Task<CaseUpdate?> AddCitizenResponseAsync(
        Guid caseId,
        Guid citizenUserId,
        string message,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CaseUpdate>> GetUpdatesAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CaseStatusHistory>> GetStatusHistoryAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken);
    Task<Case?> AssignAsync(
        Guid caseId,
        Guid organizationId,
        Guid staffMemberId,
        CancellationToken cancellationToken,
        Guid? assignedByUserId = null,
        Domain.Cases.CaseAssignmentRole role =
            Domain.Cases.CaseAssignmentRole.CaseWorker);
    Task<Case?> RouteToOrganizationAdministratorAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<CaseAssignmentHistory>> GetAssignmentHistoryAsync(
        Guid caseId,
        Guid organizationId,
        CancellationToken cancellationToken);
}
