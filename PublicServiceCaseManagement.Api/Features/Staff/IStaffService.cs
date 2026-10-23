using PublicServiceCaseManagement.Api.Domain.Staff;

namespace PublicServiceCaseManagement.Api.Features.Staff;

public interface IStaffService
{
    Task<StaffMember> CreateAsync(
        Guid organizationId,
        string fullName,
        string email,
        string? phoneNumber,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMember>> GetAllAsync(
        Guid organizationId,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffMember>> GetAllPlatformStaffAsync(
        Guid? organizationId,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<StaffMember?> SetActiveStatusAsync(
        Guid organizationId,
        Guid staffMemberId,
        bool isActive,
        CancellationToken cancellationToken);

    Task<StaffMember?> UpdatePhoneNumberAsync(
        Guid organizationId,
        Guid staffMemberId,
        string? phoneNumber,
        CancellationToken cancellationToken);
}