using PublicServiceCaseManagement.Api.Domain.Dashboard;

namespace PublicServiceCaseManagement.Api.Features.Dashboard;

public interface IDashboardService
{
    Task<CaseStatistics> GetPlatformStatisticsAsync();
    Task<CaseStatistics> GetOrganizationStatisticsAsync(Guid organizationId);
}
