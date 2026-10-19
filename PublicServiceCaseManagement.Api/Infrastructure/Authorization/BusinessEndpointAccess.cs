namespace PublicServiceCaseManagement.Api.Infrastructure.Authorization;

public sealed class BusinessEndpointAccess(
    IHostEnvironment environment,
    IConfiguration configuration) : IBusinessEndpointAccess
{
    public bool IsEnabled =>
        environment.IsDevelopment() ||
        configuration.GetValue<bool>("BusinessEndpoints:Enabled");
}
