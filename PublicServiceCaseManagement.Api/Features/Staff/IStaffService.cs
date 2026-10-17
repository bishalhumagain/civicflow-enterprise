using PublicServiceCaseManagement.Api.Domain.Staff;

namespace PublicServiceCaseManagement.Api.Features.Staff;

public interface IStaffService
{
    Task<StaffMember> CreateAsync(
        Guid organizationId,
        string fullName,
        string email,
        CancellationToken cancellationToken);
}