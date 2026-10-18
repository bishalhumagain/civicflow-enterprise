namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class AuthenticatedCurrentOrganizationContext(
    IAuthenticatedOrganizationResolver resolver)
    : ICurrentOrganizationContext
{
    public Task<Guid?> GetOrganizationIdAsync(
        CancellationToken cancellationToken = default)
    {
        return resolver.ResolveAsync(cancellationToken);
    }
}