using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Dashboard;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Dashboard;

public sealed class DashboardService(CivicFlowDbContext dbContext) : IDashboardService
{
    public async Task<CaseStatistics> GetPlatformStatisticsAsync()
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddMonths(-1);

        var cases = await dbContext.Cases.ToListAsync();

        return new CaseStatistics
        {
            TotalCases = cases.Count,
            SubmittedCases = cases.Count(c => c.Status == CaseStatus.Submitted),
            UnderReviewCases = cases.Count(c => c.Status == CaseStatus.UnderReview),
            AssignedCases = cases.Count(c => c.Status == CaseStatus.Assigned),
            InProgressCases = cases.Count(c => c.Status == CaseStatus.InProgress),
            AwaitingInformationCases = cases.Count(c => c.Status == CaseStatus.AwaitingInformation),
            ResolvedCases = cases.Count(c => c.Status == CaseStatus.Resolved),
            ClosedCases = cases.Count(c => c.Status == CaseStatus.Closed),
            RejectedCases = cases.Count(c => c.Status == CaseStatus.Rejected),
            HighPriorityCases = cases.Count(c => c.Priority == CasePriority.High),
            NormalPriorityCases = cases.Count(c => c.Priority == CasePriority.Normal),
            LowPriorityCases = cases.Count(c => c.Priority == CasePriority.Low),
            CasesCreatedThisWeek = cases.Count(c => c.CreatedAt >= weekAgo),
            CasesCreatedThisMonth = cases.Count(c => c.CreatedAt >= monthAgo),
            CasesResolvedThisWeek = cases.Count(c => c.Status == CaseStatus.Resolved && c.UpdatedAt >= weekAgo),
            CasesResolvedThisMonth = cases.Count(c => c.Status == CaseStatus.Resolved && c.UpdatedAt >= monthAgo)
        };
    }

    public async Task<CaseStatistics> GetOrganizationStatisticsAsync(Guid organizationId)
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddMonths(-1);

        var cases = await dbContext.Cases
            .Where(c => c.OrganizationId == organizationId)
            .ToListAsync();

        return new CaseStatistics
        {
            TotalCases = cases.Count,
            SubmittedCases = cases.Count(c => c.Status == CaseStatus.Submitted),
            UnderReviewCases = cases.Count(c => c.Status == CaseStatus.UnderReview),
            AssignedCases = cases.Count(c => c.Status == CaseStatus.Assigned),
            InProgressCases = cases.Count(c => c.Status == CaseStatus.InProgress),
            AwaitingInformationCases = cases.Count(c => c.Status == CaseStatus.AwaitingInformation),
            ResolvedCases = cases.Count(c => c.Status == CaseStatus.Resolved),
            ClosedCases = cases.Count(c => c.Status == CaseStatus.Closed),
            RejectedCases = cases.Count(c => c.Status == CaseStatus.Rejected),
            HighPriorityCases = cases.Count(c => c.Priority == CasePriority.High),
            NormalPriorityCases = cases.Count(c => c.Priority == CasePriority.Normal),
            LowPriorityCases = cases.Count(c => c.Priority == CasePriority.Low),
            CasesCreatedThisWeek = cases.Count(c => c.CreatedAt >= weekAgo),
            CasesCreatedThisMonth = cases.Count(c => c.CreatedAt >= monthAgo),
            CasesResolvedThisWeek = cases.Count(c => c.Status == CaseStatus.Resolved && c.UpdatedAt >= weekAgo),
            CasesResolvedThisMonth = cases.Count(c => c.Status == CaseStatus.Resolved && c.UpdatedAt >= monthAgo)
        };
    }
}
