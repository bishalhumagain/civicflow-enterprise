namespace PublicServiceCaseManagement.Api.Infrastructure.Authorization;

public interface IBusinessEndpointAccess
{
    bool IsEnabled { get; }
}
