using PublicServiceCaseManagement.Api.Domain.Dashboard;
using PublicServiceCaseManagement.Api.Features.Dashboard.Contracts;

namespace PublicServiceCaseManagement.Api.Features.Dashboard.Services;

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
