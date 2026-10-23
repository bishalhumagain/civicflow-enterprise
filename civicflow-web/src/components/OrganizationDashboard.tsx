import type {CaseStatistics} from '../domain'
import {CaseAnalytics} from './CaseAnalytics'

export function OrganizationDashboard({
                                          statistics,
                                          loading,
                                          organizationName,
                                      }: {
    statistics: CaseStatistics | null
    loading: boolean
    organizationName: string
}) {
    if (loading) {
        return <p aria-live="polite" className="muted empty-state">Loading organization metrics…</p>
    }

    if (!statistics) {
        return (
            <section className="case-card dashboard-unavailable" role="status">
                <h1>Organization overview</h1>
                <p className="muted">Organization metrics are unavailable. Use the error message above to retry.</p>
            </section>
        )
    }

    return (
        <>
            <div className="page-heading dashboard-heading">
                <div>
                    <p className="eyebrow">ORGANIZATION WORKSPACE</p>
                    <h1>{organizationName} overview</h1>
                    <p className="muted">Live case statistics for the active organization.</p>
                </div>
            </div>
            <p className="info-banner dashboard-scope-note">
                Metrics are scoped to this organization. Review the Cases workspace for case assignments; individual
                staff workload is not available in these statistics.
            </p>
            <CaseAnalytics statistics={statistics}/>
        </>
    )
}
