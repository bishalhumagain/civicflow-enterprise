using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class AuthenticatedCurrentOrganizationContext(
    IAuthenticatedOrganizationResolver resolver,
    IOrganizationTransactionContext transactionContext)
    : ICurrentOrganizationContext
{
    public async Task<Guid?> GetOrganizationIdAsync(
        CancellationToken cancellationToken = default)
    {
        var organizationId = await resolver.ResolveAsync(cancellationToken);

        if (organizationId.HasValue)
        {
            await transactionContext.BeginAsync(
                organizationId.Value,
                cancellationToken);
        }

        return organizationId;
    }

    public async Task<OrganizationRole?> GetOrganizationRoleAsync(
        CancellationToken cancellationToken = default)
    {
        var role = await resolver.ResolveRoleAsync(cancellationToken);
        var organizationId = await resolver.ResolveAsync(cancellationToken);

        if (organizationId.HasValue)
        {
            await transactionContext.BeginAsync(
                organizationId.Value,
                cancellationToken);
        }

        return role;
    }

    public Task<PlatformRole?> GetPlatformRoleAsync(
        CancellationToken cancellationToken = default) =>
        resolver.ResolvePlatformRoleAsync(cancellationToken);

    public Task<Guid?> GetApplicationUserIdAsync(
        CancellationToken cancellationToken = default) =>
        resolver.ResolveApplicationUserIdAsync(cancellationToken);
}
