using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public interface IAuthenticatedOrganizationResolver
{
    Task<Guid?> ResolveAsync(
        CancellationToken cancellationToken = default);

    Task<OrganizationRole?> ResolveRoleAsync(
        CancellationToken cancellationToken = default);

    Task<PlatformRole?> ResolvePlatformRoleAsync(
        CancellationToken cancellationToken = default);
}