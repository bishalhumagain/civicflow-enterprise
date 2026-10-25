import type {CaseStatistics, PlatformOverview, PlatformOrganizationCaseBreakdown} from '../domain'
import { formatNumber } from '../utils/format'
import {CaseAnalytics} from './CaseAnalytics'
import {StatCard} from './StatCard'

export function PlatformDashboard({
                                      statistics,
                                      overview,
                                      organizationBreakdown,
                                      loading,
                                  }: {
    statistics: CaseStatistics | null
    overview: PlatformOverview | null
    organizationBreakdown: PlatformOrganizationCaseBreakdown[]
    loading: boolean
}) {
    if (loading) {
        return <p aria-live="polite" className="muted empty-state">Loading platform metrics…</p>
    }

    if (!statistics) {
        return (
            <section className="case-card dashboard-unavailable" role="status">
                <h1>Platform overview</h1>
                <p className="muted">Platform metrics are unavailable. Use the error message above to retry.</p>
            </section>
        )
    }

    const largestOrganization = Math.max(
        1,
        ...organizationBreakdown.map((organization) => organization.totalCases),
    )

    return (
        <>
            <div className="page-heading dashboard-heading">
                <div>
                    <p className="eyebrow">PLATFORM ADMINISTRATION</p>
                    <h1>Platform overview</h1>
                    <p className="muted">A live view of case activity and platform operations.</p>
                </div>
            </div>

            {overview && (
                <section aria-label="Platform accounts and organizations" className="stat-grid platform-overview-grid">
                    <StatCard label="Organizations" value={formatNumber(overview.organizationCount)}
                              detail={`${formatNumber(overview.activeOrganizationCount)} active`} tone="blue"/>
                    <StatCard label="User accounts" value={formatNumber(overview.userCount)}
                              detail={`${formatNumber(overview.activeUserCount)} active`} tone="green"/>
                    <StatCard label="Staff memberships" value={formatNumber(overview.staffCount)}
                              detail={`${formatNumber(overview.activeStaffCount)} active`} tone="purple"/>
                    <StatCard label="Pending invitations" value={formatNumber(overview.pendingInvitationCount)}
                              detail={`${formatNumber(overview.invitationCount)} total invitations`} tone="amber"/>
                </section>
            )}

            {organizationBreakdown.length > 0 && (
                <section className="case-card analytics-card platform-organization-chart"
                         aria-label="Cases by organization">
                    <header className="analytics-heading">
                        <div>
                            <h2>Cases by organization</h2>
                            <p className="muted">
                                Case volume per organization · {formatNumber(statistics.totalCases)} total cases
                            </p>
                        </div>
                    </header>
                    <div className="status-chart" role="list" aria-label="Cases by organization">
                        {[...organizationBreakdown]
                            .sort((first, second) =>
                                second.totalCases - first.totalCases ||
                                first.organizationName.localeCompare(second.organizationName))
                            .map((organization) => {
                                const percentage = Math.round(
                                    (organization.totalCases / largestOrganization) * 100,
                                )

                                return (
                                    <div className="status-chart-row" key={organization.organizationId}
                                         role="listitem">
                                        <span title={organization.organizationName}>
                                            {organization.organizationName}
                                        </span>
                                        <div
                                            className="status-chart-track"
                                            role="img"
                                            aria-label={
                                                `${organization.organizationName}: ` +
                                                `${organization.totalCases} cases, ` +
                                                `${organization.openCases} open, ` +
                                                `${organization.highPriorityCases} high priority`
                                            }
                                        >
                                            <span style={{
                                                width: `${percentage}%`,
                                                background: 'var(--chart-blue)',
                                            }}/>
                                        </div>
                                        <strong>{formatNumber(organization.totalCases)}</strong>
                                    </div>
                                )
                            })}
                    </div>
                </section>
            )}

            <CaseAnalytics scopeLabel="all organizations" statistics={statistics}/>
        </>
    )
}
