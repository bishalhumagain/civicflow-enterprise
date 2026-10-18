namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public interface ICurrentOrganizationContext
{
    Task<Guid?> GetOrganizationIdAsync(
        CancellationToken cancellationToken = default);
}