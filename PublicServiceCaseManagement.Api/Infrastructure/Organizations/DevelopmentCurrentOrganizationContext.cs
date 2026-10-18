using PublicServiceCaseManagement.Api.Domain.Organizations;
using PublicServiceCaseManagement.Api.Domain.Users;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class DevelopmentCurrentOrganizationContext(
    IHostEnvironment environment) : ICurrentOrganizationContext
{
    private static readonly Guid DevelopmentOrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Task<Guid?> GetOrganizationIdAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();

        return Task.FromResult<Guid?>(DevelopmentOrganizationId);
    }

    public Task<OrganizationRole?> GetOrganizationRoleAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();

        return Task.FromResult<OrganizationRole?>(
            OrganizationRole.OrganizationAdmin);
    }

    public Task<PlatformRole?> GetPlatformRoleAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureDevelopment();

        return Task.FromResult<PlatformRole?>(
            PlatformRole.SuperAdmin);
    }

    private void EnsureDevelopment()
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "The development organization context cannot be used outside Development.");
        }
    }
}