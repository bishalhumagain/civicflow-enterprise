using Microsoft.AspNetCore.Hosting;

namespace PublicServiceCaseManagement.Api.Infrastructure.Organizations;

public sealed class DevelopmentCurrentOrganizationContext(
    IHostEnvironment environment) : ICurrentOrganizationContext
{
    private static readonly Guid DevelopmentOrganizationId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid OrganizationId
    {
        get
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "The development organization context cannot be used outside Development.");
            }

            return DevelopmentOrganizationId;
        }
    }
}