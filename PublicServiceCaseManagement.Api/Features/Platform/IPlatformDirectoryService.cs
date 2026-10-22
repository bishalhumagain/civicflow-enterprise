namespace PublicServiceCaseManagement.Api.Features.Platform;

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
}
