import {useEffect, useState} from 'react'
import {
    getMyCases,
    getMyCase,
    type CitizenCaseSummary,
    type CitizenCaseDetails,
} from '../api/citizens'
import {statusLabels} from '../domain'
import {StatusPill} from './StatusPill'
import {StatCard} from './StatCard'

export type CitizenDashboardProps = {
    fullName: string
    email: string
    emailConfirmed: boolean
    onOpenIntake: () => void
    onOpenProfile: () => void
}

/**
 * Dashboard shown to accounts that hold the citizen role. It only exposes
 * citizen self-service; case management and organization administration stay
 * behind the organization portal.
 */
export function CitizenDashboard({
                                     fullName,
                                     email,
                                     emailConfirmed,
                                     onOpenIntake,
                                     onOpenProfile,
                                 }: CitizenDashboardProps) {
    const firstName = fullName.trim().split(/\s+/)[0] || fullName
    const [cases, setCases] = useState<CitizenCaseSummary[]>([])
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [selected, setSelected] = useState<CitizenCaseDetails | null>(null)
    const [detailLoading, setDetailLoading] = useState(false)
    const [detailError, setDetailError] = useState<string | null>(null)

    async function openCase(id: string) {
        setDetailLoading(true)
        setDetailError(null)

        try {
            const details = await getMyCase(id)
            setSelected(details)
        } catch (reason: unknown) {
            setDetailError(reason instanceof Error
                ? reason.message
                : 'Failed to load request details. Try again.')
        } finally {
            setDetailLoading(false)
        }
    }

    function closeCase() {
        setSelected(null)
        setDetailError(null)
    }

    useEffect(() => {
        let mounted = true

        getMyCases()
            .then((loaded) => {
                if (mounted) setCases(loaded)
            })
            .catch((reason: unknown) => {
                if (mounted) {
                    setError(reason instanceof Error
                        ? reason.message
                        : 'Failed to load your requests. Refresh the page and try again.')
                }
            })
            .finally(() => {
                if (mounted) setLoading(false)
            })

        return () => {
            mounted = false
        }
    }, [])

    return (
        <>
            <div className="page-heading">
                <div>
                    <p className="eyebrow">CITIZEN PORTAL</p>
                    <h1>Welcome back, {firstName}</h1>
                    <p className="muted">
                        Submit service requests and keep your profile current.
                    </p>
                </div>
            </div>

            <div className="stat-grid citizen-stat-grid">
                <StatCard
                    label="Account status"
                    value={emailConfirmed ? 'Verified' : 'Pending'}
                    detail={emailConfirmed ? 'Email address confirmed' : 'Confirm your email address'}
                    tone={emailConfirmed ? 'green' : 'amber'}
                />
                <StatCard
                    label="Signed in as"
                    value={email}
                    detail="Citizen account"
                    tone="blue"
                />
                <StatCard
                    label="Portal access"
                    value="Citizen"
                    detail="Public self-service only"
                    tone="purple"
                />
            </div>

            <div className="section-heading">
                <div>
                    <h2>My requests</h2>
                    <span className="muted">Track your submitted service requests</span>
                </div>
            </div>

            {selected ? (
                <>
                    {detailError ? (
                        <div className="error-banner" role="alert">{detailError}</div>
                    ) : null}

                    <article className="case-card citizen-case-detail">
                        <div className="case-card-header">
                            <h3>{selected.title}</h3>
                            <StatusPill status={selected.status} />
                        </div>
                        <p className="muted">Reference: {selected.referenceNumber}</p>
                        <p className="muted">
                            Last updated: {new Date(selected.updatedAt).toLocaleString()}
                        </p>
                        <p className="citizen-case-detail-description">{selected.description}</p>

                        <h4>Status history</h4>
                        {selected.statusHistory.length === 0 ? (
                            <p className="muted">No status changes have been recorded yet.</p>
                        ) : (
                            <ol className="citizen-status-timeline">
                                {selected.statusHistory.map((entry, index) => (
                                    <li key={`${entry.changedAt}-${index}`}>
                                        <span className="citizen-status-transition">
                                            {statusLabels[entry.previousStatus]} → {statusLabels[entry.newStatus]}
                                        </span>
                                        <span className="muted">
                                            {new Date(entry.changedAt).toLocaleString()}
                                        </span>
                                    </li>
                                ))}
                            </ol>
                        )}

                        <button className="primary-button" onClick={closeCase} type="button">
                            Back to my requests
                        </button>
                    </article>
                </>
            ) : loading ? (
                <div className="muted">Loading your requests...</div>
            ) : error ? (
                <div className="error-banner" role="alert">{error}</div>
            ) : cases.length === 0 ? (
                <div className="info-banner citizen-tracking-note" role="status">
                    You haven't submitted any service requests yet. Use public intake to submit your first request.
                </div>
            ) : (
                <>
                    {detailError ? (
                        <div className="error-banner" role="alert">{detailError}</div>
                    ) : null}

                    <div className="directory-grid">
                        {cases.map(c => (
                            <article key={c.id} className="case-card citizen-action-card">
                                <div className="case-card-header">
                                    <h3>{c.title}</h3>
                                    <StatusPill status={c.status} />
                                </div>
                                <p className="muted">
                                    Reference: {c.referenceNumber}
                                </p>
                                <p className="muted">
                                    Submitted: {new Date(c.createdAt).toLocaleDateString()}
                                </p>
                                <button
                                    className="primary-button"
                                    onClick={() => openCase(c.id)}
                                    type="button"
                                    disabled={detailLoading}
                                >
                                    {detailLoading ? "Loading..." : "View details"}
                                </button>
                            </article>
                        ))}
                    </div>
                </>
            )}

            <div className="section-heading">
                <div>
                    <h2>Quick actions</h2>
                    <span className="muted">Citizen self-service</span>
                </div>
            </div>

            <div className="directory-grid">
                <article className="case-card citizen-action-card">
                    <h3>Request public assistance</h3>
                    <p className="muted">
                        Describe a local service issue and send it to the responsible team.
                    </p>
                    <button className="primary-button" onClick={onOpenIntake} type="button">
                        Open public intake
                    </button>
                </article>

                <article className="case-card citizen-action-card">
                    <h3>My profile</h3>
                    <p className="muted">
                        Review the contact details and identifiers attached to your account.
                    </p>
                    <button className="primary-button" onClick={onOpenProfile} type="button">
                        Open my profile
                    </button>
                </article>
            </div>
        </>
    )
}
