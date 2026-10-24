using PublicServiceCaseManagement.Api.Domain.Organizations;

namespace PublicServiceCaseManagement.Api.Features.Memberships;

public interface IOrganizationMembershipService
{
    Task<OrganizationMembership> CreateAsync(
        Guid userId,
        Guid organizationId,
        OrganizationRole role,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationMembership>> GetAllAsync(
        Guid organizationId,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<OrganizationMembership?> GetByIdAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken);

    Task<OrganizationMembership?> ChangeRoleAsync(
        Guid organizationId,
        Guid membershipId,
        OrganizationRole role,
        CancellationToken cancellationToken);

    Task<OrganizationMembership?> SetActiveStatusAsync(
        Guid organizationId,
        Guid membershipId,
        bool isActive,
        CancellationToken cancellationToken);

    Task<OrganizationMembership> TransferAsync(
        Guid sourceOrganizationId,
        Guid membershipId,
        Guid destinationOrganizationId,
        OrganizationRole role,
        CancellationToken cancellationToken);
}
