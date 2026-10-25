import type {CaseStatistics, PlatformOverview} from '../domain'
import { formatNumber } from '../utils/format'
import {CaseAnalytics} from './CaseAnalytics'
import {StatCard} from './StatCard'

export function PlatformDashboard({
                                      statistics,
                                      overview,
                                      loading,
                                  }: {
    statistics: CaseStatistics | null
    overview: PlatformOverview | null
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

            <CaseAnalytics statistics={statistics}/>
        </>
    )
}
