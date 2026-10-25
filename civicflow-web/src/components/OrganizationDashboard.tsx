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
    const heading = (
        <div className="page-heading dashboard-heading">
            <div>
                <p className="eyebrow">ORGANIZATION WORKSPACE</p>
                <h1>{organizationName} overview</h1>
                <p className="muted">Live case statistics for the active organization.</p>
            </div>
        </div>
    )

    if (loading) {
        return (
            <>
                {heading}
                <p aria-live="polite" className="muted empty-state">Loading organization metrics…</p>
            </>
        )
    }

    if (!statistics) {
        return (
            <>
                {heading}
                <section className="case-card dashboard-unavailable" role="status">
                    <h2>Metrics unavailable</h2>
                    <p className="muted">Organization metrics are unavailable. Use the error message above to retry.</p>
                </section>
            </>
        )
    }

    return (
        <>
            {heading}
            <CaseAnalytics statistics={statistics}/>
        </>
    )
}
