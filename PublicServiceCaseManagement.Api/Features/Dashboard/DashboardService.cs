using Microsoft.EntityFrameworkCore;
using PublicServiceCaseManagement.Api.Domain.Cases;
using PublicServiceCaseManagement.Api.Domain.Dashboard;
using PublicServiceCaseManagement.Api.Infrastructure.Organizations;
using PublicServiceCaseManagement.Api.Infrastructure.Persistence;

namespace PublicServiceCaseManagement.Api.Features.Dashboard;

public sealed class DashboardService(
    CivicFlowDbContext dbContext,
    IOrganizationTransactionContext transactionContext) : IDashboardService
{
    public async Task<CaseStatistics> GetPlatformStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        await transactionContext.BeginPlatformReadAsync(cancellationToken);

        var cases = await dbContext.Cases
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return BuildStatistics(cases);
    }

    public async Task<IReadOnlyList<PlatformOrganizationCaseBreakdown>>
        GetPlatformOrganizationBreakdownAsync(
            CancellationToken cancellationToken = default)
    {
        await transactionContext.BeginPlatformReadAsync(cancellationToken);

        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .OrderBy(organization => organization.Name)
            .ThenBy(organization => organization.Id)
            .Select(organization => new
            {
                organization.Id,
                organization.Name,
                organization.Code,
                organization.IsActive
            })
            .ToListAsync(cancellationToken);

        var cases = await dbContext.Cases
            .AsNoTracking()
            .Select(c => new { c.OrganizationId, c.Status, c.Priority })
            .ToListAsync(cancellationToken);

        return organizations
            .Select(organization =>
            {
                var organizationCases = cases
                    .Where(c => c.OrganizationId == organization.Id)
                    .ToList();

                return new PlatformOrganizationCaseBreakdown(
                    organization.Id,
                    organization.Name,
                    organization.Code,
                    organization.IsActive,
                    organizationCases.Count,
                    organizationCases.Count(c => IsOpen(c.Status)),
                    organizationCases.Count(c =>
                        c.Status is CaseStatus.Resolved or CaseStatus.Closed),
                    organizationCases.Count(c => c.Priority == CasePriority.High));
            })
            .ToList();
    }

    public async Task<CaseStatistics> GetOrganizationStatisticsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var cases = await dbContext.Cases
            .AsNoTracking()
            .Where(c => c.OrganizationId == organizationId)
            .ToListAsync(cancellationToken);

        return BuildStatistics(cases);
    }

    private static CaseStatistics BuildStatistics(IReadOnlyList<Case> cases)
    {
        var now = DateTime.UtcNow;
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddMonths(-1);

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

    private static bool IsOpen(CaseStatus status) =>
        status is not (CaseStatus.Resolved
            or CaseStatus.Closed
            or CaseStatus.Rejected);
}
