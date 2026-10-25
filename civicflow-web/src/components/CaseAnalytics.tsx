import type {ReactNode} from 'react'
import { formatNumber } from '../utils/format'
import {statusLabels, statusOptions, type CaseStatistics, type CaseStatus} from '../domain'

const statusColors: Record<string, string> = {
    Submitted: 'var(--chart-slate)',
    UnderReview: 'var(--chart-amber)',
    Assigned: 'var(--chart-blue)',
    InProgress: 'var(--chart-violet)',
    AwaitingInformation: 'var(--chart-orange)',
    Resolved: 'var(--chart-green)',
    Closed: 'var(--chart-teal)',
    Rejected: 'var(--chart-red)',
}

const priorityColors = ['var(--chart-red)', 'var(--chart-blue)', 'var(--chart-green)']

export function CaseAnalytics({statistics}: { statistics: CaseStatistics }) {
    const statusCounts: Record<CaseStatus, number> = {
        Submitted: statistics.submittedCases,
        UnderReview: statistics.underReviewCases,
        Assigned: statistics.assignedCases,
        InProgress: statistics.inProgressCases,
        AwaitingInformation: statistics.awaitingInformationCases,
        Resolved: statistics.resolvedCases,
        Closed: statistics.closedCases,
        Rejected: statistics.rejectedCases,
    }
    const statuses = statusOptions.map((status) => ({
        label: statusLabels[status],
        value: statusCounts[status],
        color: statusColors[status],
    }))
    const priorities = [
        {label: 'High', value: statistics.highPriorityCases, color: priorityColors[0]},
        {label: 'Normal', value: statistics.normalPriorityCases, color: priorityColors[1]},
        {label: 'Low', value: statistics.lowPriorityCases, color: priorityColors[2]},
    ]
    const priorityTotal = priorities.reduce((total, item) => total + item.value, 0)
    const activeCases = statistics.totalCases - statistics.resolvedCases - statistics.closedCases - statistics.rejectedCases
    const activityMax = Math.max(
        statistics.casesCreatedThisWeek,
        statistics.casesResolvedThisWeek,
        statistics.casesCreatedThisMonth,
        statistics.casesResolvedThisMonth,
    )

    return (
        <>
            <div className="stat-grid dashboard-stat-grid">
                <SummaryCard label="Total cases" value={statistics.totalCases} detail="Across this reporting scope"/>
                <SummaryCard label="Active cases" value={Math.max(0, activeCases)}
                             detail="Not yet resolved, closed, or rejected"/>
                <SummaryCard label="Resolved this month" value={statistics.casesResolvedThisMonth}
                             detail="Reported by the case service"/>
                <SummaryCard label="Created this month" value={statistics.casesCreatedThisMonth}
                             detail="Reported by the case service"/>
            </div>

            <section className="analytics-grid" aria-label="Case analytics">
                <article className="case-card analytics-card">
                    <header className="analytics-heading">
                        <div>
                            <h2>Cases by status</h2>
                            <p className="muted">Current distribution
                                · {formatNumber(statistics.totalCases)} total</p>
                        </div>
                    </header>
                    {statistics.totalCases === 0 ? (
                        <p className="empty-state muted">Status distribution will appear when cases are available.</p>
                    ) : (
                        <div className="status-chart" role="list" aria-label="Cases by status">
                            {statuses.map((item) => {
                                const percentage = statistics.totalCases > 0
                                    ? Math.round((item.value / statistics.totalCases) * 100)
                                    : 0
                                return (
                                    <div className="status-chart-row" key={item.label} role="listitem">
                                        <span>{item.label}</span>
                                        <div
                                            className="status-chart-track"
                                            role="img"
                                            aria-label={`${item.label}: ${item.value} cases, ${percentage}%`}
                                        >
                                            <span style={{width: `${percentage}%`, background: item.color}}/>
                                        </div>
                                        <strong>{formatNumber(item.value)}</strong>
                                    </div>
                                )
                            })}
                        </div>
                    )}
                </article>

                <article className="case-card analytics-card">
                    <header className="analytics-heading">
                        <div>
                            <h2>Priority mix</h2>
                            <p className="muted">Cases grouped by reported priority</p>
                        </div>
                    </header>
                    {priorityTotal === 0 ? (
                        <p className="empty-state muted">Priority distribution will appear when cases are available.</p>
                    ) : (
                        <div className="priority-chart">
                            <svg
                                className="priority-donut"
                                viewBox="0 0 120 120"
                                role="img"
                                aria-label={`Priority mix: ${priorities.map(({
                                                                                 label,
                                                                                 value
                                                                             }) => `${label} ${value}`).join(', ')}`}
                            >
                                <circle className="donut-track" cx="60" cy="60" r="44"/>
                                {priorities.reduce<{ offset: number; circles: ReactNode[] }>((result, item) => {
                                    const length = (item.value / priorityTotal) * 276.46
                                    result.circles.push(
                                        <circle
                                            cx="60"
                                            cy="60"
                                            key={item.label}
                                            r="44"
                                            stroke={item.color}
                                            strokeDasharray={`${length} ${276.46 - length}`}
                                            strokeDashoffset={-result.offset}
                                        />,
                                    )
                                    result.offset += length
                                    return result
                                }, {offset: 0, circles: []}).circles}
                                <text className="donut-total" textAnchor="middle" x="60" y="57">
                                    {formatNumber(priorityTotal)}
                                </text>
                                <text className="donut-caption" textAnchor="middle" x="60" y="73">cases</text>
                            </svg>
                            <ul className="chart-legend">
                                {priorities.map((item) => (
                                    <li key={item.label}>
                                        <span><i style={{background: item.color}}/>{item.label}</span>
                                        <strong>{formatNumber(item.value)}</strong>
                                    </li>
                                ))}
                            </ul>
                        </div>
                    )}
                </article>

                <article className="case-card analytics-card activity-chart-card">
                    <header className="analytics-heading">
                        <div>
                            <h2>Recent case activity</h2>
                            <p className="muted">Counts reported for the current week and month</p>
                        </div>
                    </header>
                    <div className="activity-bars" role="list" aria-label="Case creation and resolution activity">
                        <ActivityBar label="Created · this week" value={statistics.casesCreatedThisWeek}
                                     max={activityMax} color="var(--chart-blue)"/>
                        <ActivityBar label="Resolved · this week" value={statistics.casesResolvedThisWeek}
                                     max={activityMax} color="var(--chart-green)"/>
                        <ActivityBar label="Created · this month" value={statistics.casesCreatedThisMonth}
                                     max={activityMax} color="var(--chart-blue)"/>
                        <ActivityBar label="Resolved · this month" value={statistics.casesResolvedThisMonth}
                                     max={activityMax} color="var(--chart-green)"/>
                    </div>
                </article>
            </section>
        </>
    )
}

function SummaryCard({label, value, detail}: { label: string; value: number; detail: string }) {
    return (
        <article className="stat-card">
            <span className="summary-indicator" aria-hidden="true"/>
            <div>
                <span>{label}</span>
                <strong>{formatNumber(value)}</strong>
                <small>{detail}</small>
            </div>
        </article>
    )
}

function ActivityBar({label, value, max, color}: { label: string; value: number; max: number; color: string }) {
    const percentage = max > 0 ? Math.min(100, Math.round((value / max) * 100)) : 0
    return (
        <div className="activity-bar-row" role="listitem">
            <span>{label}</span>
            <div
                className="status-chart-track"
                role="img"
                aria-label={`${label}: ${value} cases`}
            >
                <span style={{width: `${percentage}%`, background: color}}/>
            </div>
            <strong>{formatNumber(value)}</strong>
        </div>
    )
}
