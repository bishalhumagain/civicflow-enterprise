namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public interface IAuthenticatedOrganizationResolver
{
    Task<Guid?> ResolveAsync(
        CancellationToken cancellationToken = default);
}