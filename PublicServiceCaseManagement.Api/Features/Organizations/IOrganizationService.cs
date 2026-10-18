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
}