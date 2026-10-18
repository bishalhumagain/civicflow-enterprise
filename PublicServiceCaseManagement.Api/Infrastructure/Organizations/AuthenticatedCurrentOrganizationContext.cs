using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class AuthenticatedCurrentOrganizationContext(
    IAuthenticatedOrganizationResolver resolver)
    : ICurrentOrganizationContext
{
    public Task<Guid?> GetOrganizationIdAsync(
        CancellationToken cancellationToken = default) =>
        resolver.ResolveAsync(cancellationToken);

    public Task<OrganizationRole?> GetOrganizationRoleAsync(
        CancellationToken cancellationToken = default) =>
        resolver.ResolveRoleAsync(cancellationToken);

    public Task<PlatformRole?> GetPlatformRoleAsync(
        CancellationToken cancellationToken = default) =>
        resolver.ResolvePlatformRoleAsync(cancellationToken);
}