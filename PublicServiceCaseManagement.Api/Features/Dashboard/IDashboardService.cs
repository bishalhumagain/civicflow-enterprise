using PublicServiceCaseManagement.Api.Domain.Dashboard;

namespace PublicServiceCaseManagement.Api.Features.Dashboard;

public interface IDashboardService
{
    Task<CaseStatistics> GetPlatformStatisticsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformOrganizationCaseBreakdown>>
        GetPlatformOrganizationBreakdownAsync(
            CancellationToken cancellationToken = default);

    Task<CaseStatistics> GetOrganizationStatisticsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default);
}
