using PublicServiceCaseManagement.Api.Domain.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Organizations;

public interface IOrganizationService
{
    Task<Organization> CreateAsync(
        string name,
        string code,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Organization>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationWorkspaceOption>> GetMembershipsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Organization?> GetByIdAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<Organization?> UpdateAsync(
        Guid organizationId,
        string name,
        string code,
        CancellationToken cancellationToken);

    Task<Organization?> SetActiveStatusAsync(
        Guid organizationId,
        bool isActive,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an organization that no longer holds operational data. The
    /// access records that belong to it (memberships, staff records, and
    /// invitations) are removed with it.
    /// </summary>
    /// <returns>
    /// False when the organization does not exist. Throws
    /// <see cref="InvalidOperationException"/> when the organization still
    /// holds cases and therefore has to be deactivated instead.
    /// </returns>
    Task<bool> DeleteAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
}