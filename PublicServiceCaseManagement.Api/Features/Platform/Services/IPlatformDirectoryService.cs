using PublicServiceCaseManagement.Api.Features.Platform.Contracts;

namespace PublicServiceCaseManagement.Api.Features.Platform.Services;

/// <summary>
/// Read access to the platform-wide account and staff directory.
/// </summary>
public interface IPlatformDirectoryService
{
    Task<IReadOnlyList<PlatformUserSummary>> GetUsersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformStaffSummary>> GetStaffAsync(
        CancellationToken cancellationToken = default);

    Task<PlatformOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformCitizenSummary>> GetCitizensAsync(
        CancellationToken cancellationToken = default);

    Task<PlatformUserSummary> UpdateStaffAccountAsync(
        Guid userId,
        UpdatePlatformStaffRequest request,
        CancellationToken cancellationToken = default);

    Task<PlatformCitizenSummary> UpdateCitizenAsync(
        Guid userId,
        UpdatePlatformCitizenRequest request,
        CancellationToken cancellationToken = default);

    Task SetUserActiveStatusAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task ChangePlatformRoleAsync(
        Guid userId,
        string platformRole,
        CancellationToken cancellationToken = default);
}
