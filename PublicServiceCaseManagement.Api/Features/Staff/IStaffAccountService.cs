namespace PublicServiceCaseManagement.Api.Features.Staff;

public interface IStaffAccountService
{
    Task<ProvisionStaffAccountResponse> ProvisionAsync(
        Guid organizationId,
        ProvisionStaffAccountRequest request,
        CancellationToken cancellationToken = default);
}
