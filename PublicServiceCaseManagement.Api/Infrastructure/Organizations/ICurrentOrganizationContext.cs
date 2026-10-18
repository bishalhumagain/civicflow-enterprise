using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public interface ICurrentOrganizationContext
{
    Task<Guid?> GetOrganizationIdAsync(
        CancellationToken cancellationToken = default);

    Task<OrganizationRole?> GetOrganizationRoleAsync(
        CancellationToken cancellationToken = default);

    Task<PlatformRole?> GetPlatformRoleAsync(
        CancellationToken cancellationToken = default);
}