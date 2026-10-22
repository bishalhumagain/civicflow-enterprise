import { type CaseStatistics } from '../domain'
import { StatCard } from './StatCard'

export function OrganizationDashboard({
  statistics,
  loading,
}: {
  statistics: CaseStatistics | null
  loading: boolean
}) {
  if (loading) {
    return <div className="muted">Loading dashboard...</div>
  }

  if (!statistics) {
    return <div className="muted">No statistics available.</div>
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ORGANIZATION DASHBOARD</p>
          <h1>Organization Overview</h1>
          <p className="muted">Your organization's case statistics and metrics</p>
        </div>
      </div>

      <section className="case-card">
        <h2>Case Statistics</h2>

        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <StatCard label="Total Cases" value={statistics.totalCases.toString()} detail="All cases" tone="neutral" />
          <StatCard label="Submitted" value={statistics.submittedCases.toString()} detail="New cases" tone="info" />
          <StatCard label="Under Review" value={statistics.underReviewCases.toString()} detail="In review" tone="info" />
          <StatCard label="Assigned" value={statistics.assignedCases.toString()} detail="Assigned" tone="info" />
          <StatCard label="In Progress" value={statistics.inProgressCases.toString()} detail="Active" tone="success" />
          <StatCard label="Awaiting Info" value={statistics.awaitingInformationCases.toString()} detail="Pending" tone="warning" />
          <StatCard label="Resolved" value={statistics.resolvedCases.toString()} detail="Completed" tone="success" />
          <StatCard label="Closed" value={statistics.closedCases.toString()} detail="Closed" tone="neutral" />
          <StatCard label="Rejected" value={statistics.rejectedCases.toString()} detail="Rejected" tone="danger" />
        </div>

        <h3>Priority Distribution</h3>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <StatCard label="High Priority" value={statistics.highPriorityCases.toString()} detail="Urgent" tone="danger" />
          <StatCard label="Normal Priority" value={statistics.normalPriorityCases.toString()} detail="Standard" tone="info" />
          <StatCard label="Low Priority" value={statistics.lowPriorityCases.toString()} detail="Non-urgent" tone="success" />
        </div>

        <h3>Recent Activity</h3>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
          <StatCard label="Created This Week" value={statistics.casesCreatedThisWeek.toString()} detail="Last 7 days" tone="info" />
          <StatCard label="Created This Month" value={statistics.casesCreatedThisMonth.toString()} detail="Last 30 days" tone="info" />
          <StatCard label="Resolved This Week" value={statistics.casesResolvedThisWeek.toString()} detail="Last 7 days" tone="success" />
          <StatCard label="Resolved This Month" value={statistics.casesResolvedThisMonth.toString()} detail="Last 30 days" tone="success" />
        </div>
      </section>
    </>
  )
}
