using PublicServiceCaseManagement.Api.Features.Platform.Contracts;
using PublicServiceCaseManagement.Api.Infrastructure.Pagination;

namespace PublicServiceCaseManagement.Api.Features.Platform.Services;

/// <summary>
/// Read access to the platform-wide account and staff directory.
/// </summary>
public interface IPlatformDirectoryService
{
    Task<PagedResponse<PlatformUserSummary>> GetUsersAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        bool? isStaff = null,
        bool? isCitizen = null,
        string? sort = null,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<PlatformStaffSummary>> GetStaffAsync(
        int page = 1,
        int pageSize = 25,
        Guid? organizationId = null,
        bool? isActive = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<PlatformOverviewResponse> GetOverviewAsync(
        CancellationToken cancellationToken = default);

    Task<PagedResponse<PlatformCitizenSummary>> GetCitizensAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? sort = null,
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
