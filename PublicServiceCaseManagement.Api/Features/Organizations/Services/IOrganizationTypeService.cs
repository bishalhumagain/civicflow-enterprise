using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Features.Organizations.Contracts;

namespace PublicServiceCaseManagement.Api.Features.Organizations.Services;

public interface IOrganizationTypeService
{
    Task<OrganizationType> CreateAsync(
        string name,
        string code,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationTypeResponse>> GetAllAsync(
        CancellationToken cancellationToken);

    Task<OrganizationType?> GetByIdAsync(
        Guid organizationTypeId,
        CancellationToken cancellationToken);

    Task<OrganizationTypeResponse?> GetResponseByIdAsync(
        Guid organizationTypeId,
        CancellationToken cancellationToken);

    Task<OrganizationType?> UpdateAsync(
        Guid organizationTypeId,
        string name,
        string code,
        CancellationToken cancellationToken);

    Task<OrganizationType?> SetActiveStatusAsync(
        Guid organizationTypeId,
        bool isActive,
        CancellationToken cancellationToken);

    /// <summary>
    /// Permanently removes an unused organization type. Throws
    /// <see cref="InvalidOperationException"/> when at least one organization
    /// still references it.
    /// </summary>
    Task<bool> DeleteAsync(
        Guid organizationTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the display names for a batch of organization type identifiers.
    /// Missing identifiers are simply absent from the result so callers can
    /// degrade gracefully.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesByIdsAsync(
        IEnumerable<Guid> organizationTypeIds,
        CancellationToken cancellationToken);
}
