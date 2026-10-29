import {useEffect, useMemo, useState, type FormEvent} from 'react'
import {
    downloadMyCaseDocument,
    getMyCases,
    getMyCase,
    respondToMyCase,
    type CitizenCaseDetails,
    type CitizenCaseDocument,
    type CitizenCasePage,
    type CitizenCaseSort,
    type CitizenCaseUpdate,
} from '../api/citizens'
import {
    statusLabels,
    statusOptions,
    type CaseStatus,
} from '../domain'
import {formatDate, formatDateTime, formatFileSize, formatNumber} from '../utils/format'
import {MAX_CASE_UPDATE_LENGTH, normalizeMultiline, validateCaseUpdate} from '../utils/validation'
import {StatusPill} from './StatusPill'
import {StatCard} from './StatCard'

export type CitizenDashboardProps = {
    mode: 'dashboard' | 'cases'
    fullName: string
    email: string
    emailConfirmed: boolean
    onOpenIntake: () => void
    onOpenProfile: () => void
    onOpenCases: (caseId?: string) => void
    initialCaseId?: string | null
    onInitialCaseOpened?: () => void
}

const updateKindLabels: Record<CitizenCaseUpdate['kind'], string> = {
    PublicUpdate: 'Update from the case team',
    InformationRequest: 'Information requested',
    CitizenResponse: 'Your response',
}

type StatusFilter = 'All' | CaseStatus

/**
 * Citizen self-service workspace. Every query is scoped by the API to the
 * signed-in citizen; the UI never accepts a citizen identifier from input.
 */
export function CitizenDashboard({
                                     mode,
                                     fullName,
                                     email,
                                     emailConfirmed,
                                     onOpenIntake,
                                     onOpenProfile,
                                     onOpenCases,
                                     initialCaseId,
                                     onInitialCaseOpened,
                                 }: CitizenDashboardProps) {
    const firstName = fullName.trim().split(/\s+/)[0] || fullName
    const [pageData, setPageData] = useState<CitizenCasePage | null>(null)
    const [loading, setLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)
    const [reloadVersion, setReloadVersion] = useState(0)

    const [search, setSearch] = useState('')
    const [debouncedSearch, setDebouncedSearch] = useState('')
    const [statusFilter, setStatusFilter] = useState<StatusFilter>('All')
    const [sort, setSort] = useState<CitizenCaseSort>('updated_desc')
    const [page, setPage] = useState(1)

    const [selected, setSelected] = useState<CitizenCaseDetails | null>(null)
    const [detailLoading, setDetailLoading] = useState(false)
    const [detailError, setDetailError] = useState<string | null>(null)
    const [detailNotice, setDetailNotice] = useState<string | null>(null)

    const [responseText, setResponseText] = useState('')
    const [responding, setResponding] = useState(false)
    const [responseError, setResponseError] = useState<string | null>(null)
    const [downloadingId, setDownloadingId] = useState<string | null>(null)

    const filtersActive = search.trim().length > 0 || statusFilter !== 'All'

    useEffect(() => {
        if (search === debouncedSearch) return

        const timer = window.setTimeout(() => {
            setDebouncedSearch(search)
            setPage(1)
            setLoading(true)
            setError(null)
        }, 300)
        return () => window.clearTimeout(timer)
    }, [search, debouncedSearch])

    useEffect(() => {
        let mounted = true

        getMyCases({
            page,
            pageSize: 10,
            search: debouncedSearch,
            status: statusFilter === 'All' ? undefined : statusFilter,
            sort,
        })
            .then((result) => {
                if (!mounted) return
                setPageData(result)
                const lastPage = Math.max(1, result.totalPages)
                if (page > lastPage) {
                    setLoading(true)
                    setPage(lastPage)
                }
            })
            .catch((reason: unknown) => {
                if (!mounted) return
                setError(reason instanceof Error
                    ? reason.message
                    : 'Failed to load your cases. Refresh the page and try again.')
            })
            .finally(() => {
                if (mounted) setLoading(false)
            })

        return () => {
            mounted = false
        }
    }, [page, debouncedSearch, statusFilter, sort, reloadVersion])

    async function openCase(id: string) {
        setDetailLoading(true)
        setDetailError(null)
        setDetailNotice(null)
        setResponseError(null)
        setResponseText('')

        try {
            const details = await getMyCase(id)
            setSelected(details)
        } catch (reason: unknown) {
            setDetailError(reason instanceof Error
                ? reason.message
                : 'Failed to load case details. Try again.')
        } finally {
            setDetailLoading(false)
        }
    }

    useEffect(() => {
        if (!initialCaseId) return

        const timer = window.setTimeout(() => {
            void openCase(initialCaseId)
            onInitialCaseOpened?.()
        }, 0)

        return () => window.clearTimeout(timer)
        // The callback only clears the one-shot deep link in the parent.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [initialCaseId])

    function closeCase() {
        setSelected(null)
        setDetailError(null)
        setDetailNotice(null)
        setResponseError(null)
        setResponseText('')
    }

    function applyFilters(resetSearch: boolean) {
        if (resetSearch) setSearch('')
        setStatusFilter('All')
        setSort('updated_desc')
        setPage(1)
        setLoading(true)
        setError(null)
    }

    function changePage(nextPage: number) {
        setPage(nextPage)
        setLoading(true)
        setError(null)
    }

    async function submitResponse(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        if (!selected) return

        const message = normalizeMultiline(responseText)
        const validationError = validateCaseUpdate(message)

        if (validationError !== null) {
            setResponseError(validationError)
            return
        }

        setResponding(true)
        setResponseError(null)
        setDetailNotice(null)

        try {
            await respondToMyCase(selected.id, message)
            const refreshed = await getMyCase(selected.id)
            setSelected(refreshed)
            setResponseText('')
            setDetailNotice('Your response was sent to the case team.')
            setReloadVersion((version) => version + 1)
        } catch (reason: unknown) {
            setResponseError(reason instanceof Error
                ? reason.message
                : 'Your response could not be sent. Try again.')
        } finally {
            setResponding(false)
        }
    }

    async function downloadDocument(document: CitizenCaseDocument) {
        if (!selected) return

        setDownloadingId(document.id)
        setDetailError(null)

        try {
            const blob = await downloadMyCaseDocument(selected.id, document.id)
            const url = URL.createObjectURL(blob)
            const anchor = window.document.createElement('a')

            anchor.href = url
            anchor.download = document.originalFileName
            anchor.click()

            URL.revokeObjectURL(url)
        } catch (reason: unknown) {
            setDetailError(reason instanceof Error
                ? reason.message
                : 'The document could not be downloaded.')
        } finally {
            setDownloadingId(null)
        }
    }

    const totalCount = pageData?.totalCount ?? 0
    const currentPage = pageData?.page ?? page
    const totalPages = Math.max(1, pageData?.totalPages ?? 1)
    const visibleCount = pageData?.items.length ?? 0

    const statsDetail = useMemo(() => {
        if (loading) return 'Loading your cases'
        if (filtersActive) return 'Matching the current filters'
        return 'All cases on your account'
    }, [filtersActive, loading])

    return (
        <>
            {mode === 'dashboard' ? (
                <>
                    <div className="page-heading">
                        <div>
                            <p className="eyebrow">CITIZEN PORTAL</p>
                            <h1>Welcome back, {firstName}</h1>
                            <p className="muted">
                                Submit service requests and follow their progress.
                            </p>
                        </div>
                    </div>

                    <div className="stat-grid citizen-stat-grid">
                        <StatCard
                            icon={emailConfirmed ? 'check' : 'alert'}
                            label="Account status"
                            value={emailConfirmed ? 'Verified' : 'Pending'}
                            detail={emailConfirmed ? 'Email address confirmed' : 'Confirm your email address'}
                            tone={emailConfirmed ? 'green' : 'amber'}
                        />
                        <StatCard
                            icon="profile"
                            label="Signed in as"
                            value={email}
                            detail="Citizen account"
                            tone="blue"
                        />
                        <StatCard
                            icon="cases"
                            label="My cases"
                            value={loading ? '…' : formatNumber(totalCount)}
                            detail={statsDetail}
                            tone="purple"
                        />
                    </div>
                </>
            ) : (
                <div className="page-heading">
                    <div>
                        <p className="eyebrow">CITIZEN PORTAL</p>
                        <h1>My cases</h1>
                        <p className="muted">
                            Track your submitted cases and respond to updates from the case team.
                        </p>
                    </div>
                    <button className="primary-button" onClick={onOpenIntake} type="button">
                        Submit new case
                    </button>
                </div>
            )}

            {selected && mode === 'cases' ? (
                <>
                    <div className="section-heading">
                        <div>
                            <h2>Case details</h2>
                            <span className="muted">
                                {selected.referenceNumber} · {selected.organizationName}
                            </span>
                        </div>
                        <button className="secondary-button" onClick={closeCase} type="button">
                            Back to my cases
                        </button>
                    </div>

                    {detailError ? (
                        <div className="error-banner" role="alert">
                            {detailError}
                            <button
                                className="secondary-button"
                                onClick={() => void openCase(selected.id)}
                                type="button"
                            >
                                Retry
                            </button>
                        </div>
                    ) : null}

                    {detailNotice ? (
                        <div className="success-message" role="status">{detailNotice}</div>
                    ) : null}

                    <article className="case-card citizen-case-detail" aria-busy={detailLoading}>
                        <div className="case-card-header">
                            <div>
                                <h3>{selected.title}</h3>
                                <p className="muted">
                                    Reference {selected.referenceNumber}
                                </p>
                            </div>
                            <StatusPill status={selected.status}/>
                        </div>

                        <dl className="citizen-case-facts">
                            <div>
                                <dt>Responsible department</dt>
                                <dd>{selected.organizationName}</dd>
                            </div>
                            <div>
                                <dt>Submitted</dt>
                                <dd>{formatDateTime(selected.createdAt)}</dd>
                            </div>
                            <div>
                                <dt>Last updated</dt>
                                <dd>{formatDateTime(selected.updatedAt)}</dd>
                            </div>
                        </dl>

                        <p className="citizen-case-detail-description">{selected.description}</p>

                        {selected.awaitingInformation ? (
                            <div className="info-banner citizen-tracking-note" role="status">
                                The case team needs more information from you. Review the request below
                                and add your response.
                            </div>
                        ) : null}

                        <section className="citizen-detail-section" aria-labelledby="citizen-timeline-title">
                            <h4 id="citizen-timeline-title">Status timeline</h4>
                            <ol className="citizen-status-timeline">
                                <li>
                                    <span className="citizen-timeline-dot" aria-hidden="true"/>
                                    <div>
                                        <strong>Case submitted</strong>
                                        <time dateTime={selected.createdAt}>
                                            {formatDateTime(selected.createdAt)}
                                        </time>
                                    </div>
                                </li>
                                {selected.statusHistory.map((entry, index) => (
                                    <li key={`${entry.changedAt}-${entry.newStatus}-${index}`}>
                                        <span className="citizen-timeline-dot" aria-hidden="true"/>
                                        <div>
                                            <strong>
                                                {statusLabels[entry.previousStatus]} → {statusLabels[entry.newStatus]}
                                            </strong>
                                            {entry.explanation ? (
                                                <p>{entry.explanation}</p>
                                            ) : null}
                                            <time dateTime={entry.changedAt}>
                                                {formatDateTime(entry.changedAt)}
                                            </time>
                                        </div>
                                    </li>
                                ))}
                            </ol>
                        </section>

                        <section className="citizen-detail-section" aria-labelledby="citizen-updates-title">
                            <h4 id="citizen-updates-title">Updates and information requests</h4>
                            {selected.updates.length === 0 ? (
                                <p className="muted">
                                    No updates have been posted yet.
                                </p>
                            ) : (
                                <ul className="citizen-update-list">
                                    {selected.updates.map((update) => (
                                        <li
                                            className={`citizen-update citizen-update-${update.kind.toLowerCase()}`}
                                            key={update.id}
                                        >
                                            <strong>{updateKindLabels[update.kind]}</strong>
                                            <p>{update.message}</p>
                                            <time dateTime={update.createdAt}>
                                                {formatDateTime(update.createdAt)}
                                            </time>
                                        </li>
                                    ))}
                                </ul>
                            )}
                        </section>

                        <section className="citizen-detail-section" aria-labelledby="citizen-documents-title">
                            <h4 id="citizen-documents-title">Documents</h4>
                            {selected.documents.length === 0 ? (
                                <p className="muted">No documents have been shared with you.</p>
                            ) : (
                                <ul className="citizen-document-list">
                                    {selected.documents.map((document) => (
                                        <li key={document.id}>
                                            <div>
                                                <strong>{document.originalFileName}</strong>
                                                <span className="muted">
                                                    {formatFileSize(document.sizeBytes)} ·
                                                    {' '}{formatDate(document.uploadedAt)}
                                                </span>
                                            </div>
                                            <button
                                                className="secondary-button"
                                                disabled={downloadingId === document.id}
                                                onClick={() => void downloadDocument(document)}
                                                type="button"
                                            >
                                                {downloadingId === document.id ? 'Downloading…' : 'Download'}
                                            </button>
                                        </li>
                                    ))}
                                </ul>
                            )}
                        </section>

                        {selected.canRespond ? (
                            <section className="citizen-detail-section" aria-labelledby="citizen-response-title">
                                <h4 id="citizen-response-title">Add information</h4>
                                <form className="citizen-response-form" onSubmit={submitResponse}>
                                    <label htmlFor="citizen-response-message">
                                        Your message
                                    </label>
                                    <textarea
                                        aria-describedby={
                                            responseError !== null
                                                ? "citizen-response-error citizen-response-hint"
                                                : "citizen-response-hint"
                                        }
                                        aria-invalid={responseError !== null}
                                        id="citizen-response-message"
                                        maxLength={MAX_CASE_UPDATE_LENGTH}
                                        minLength={1}
                                        onChange={(event) => {
                                            setResponseText(event.target.value)
                                            setResponseError(null)
                                        }}
                                        placeholder="Provide the requested details or add context for the case team."
                                        required
                                        rows={4}
                                        value={responseText}
                                    />
                                    {responseError ? (
                                        <div className="error-banner" id="citizen-response-error" role="alert">
                                            {responseError}
                                        </div>
                                    ) : null}
                                    <small className="muted" id="citizen-response-hint">
                                        {responseText.length}/{MAX_CASE_UPDATE_LENGTH} characters
                                    </small>
                                    <button
                                        className="primary-button"
                                        disabled={responding || validateCaseUpdate(responseText) !== null}
                                        type="submit"
                                    >
                                        {responding ? 'Sending…' : 'Send response'}
                                    </button>
                                </form>
                            </section>
                        ) : (
                            <p className="muted">
                                This case is closed, so new responses are no longer accepted.
                            </p>
                        )}

                        <button className="primary-button" onClick={closeCase} type="button">
                            Back to my cases
                        </button>
                    </article>
                </>
            ) : (
                <>
                    <div className="section-heading">
                        <div>
                            <h2>{mode === 'cases' ? 'Submitted cases' : 'Recent cases'}</h2>
                            <span className="muted">
                                {loading
                                    ? 'Loading your tracked cases'
                                    : mode === 'cases'
                                        ? `Showing ${visibleCount} of ${totalCount}`
                                        : `${formatNumber(totalCount)} submitted`}
                            </span>
                        </div>
                        {mode === 'dashboard' && totalCount > 0 ? (
                            <button
                                className="secondary-button"
                                onClick={() => onOpenCases()}
                                type="button"
                            >
                                View all my cases
                            </button>
                        ) : null}
                    </div>

                    <section className="case-card citizen-request-list">
                        {mode === 'cases' ? (
                            <div className="case-toolbar">
                                <div className="search">
                                    <label className="sr-only" htmlFor="citizen-case-search">
                                        Search my cases
                                    </label>
                                    <input
                                        id="citizen-case-search"
                                        maxLength={200}
                                        placeholder="Search by title or reference…"
                                        value={search}
                                        onChange={(event) => setSearch(event.target.value)}
                                    />
                                </div>

                                <label className="citizen-filter">
                                    <span className="sr-only">Filter by status</span>
                                    <select
                                        aria-label="Filter my cases by status"
                                        value={statusFilter}
                                        onChange={(event) => {
                                            setStatusFilter(event.target.value as StatusFilter)
                                            setPage(1)
                                            setLoading(true)
                                            setError(null)
                                        }}
                                    >
                                        <option value="All">All statuses</option>
                                        {statusOptions.map((status) => (
                                            <option key={status} value={status}>
                                                {statusLabels[status]}
                                            </option>
                                        ))}
                                    </select>
                                </label>

                                <label className="citizen-filter">
                                    <span className="sr-only">Sort cases</span>
                                    <select
                                        aria-label="Sort my cases"
                                        value={sort}
                                        onChange={(event) => {
                                            setSort(event.target.value as CitizenCaseSort)
                                            setPage(1)
                                            setLoading(true)
                                            setError(null)
                                        }}
                                    >
                                        <option value="updated_desc">Recently updated</option>
                                        <option value="updated_asc">Least recently updated</option>
                                        <option value="created_desc">Newest first</option>
                                        <option value="created_asc">Oldest first</option>
                                    </select>
                                </label>
                            </div>
                        ) : null}

                        {loading ? (
                            <p className="muted empty-state" role="status">
                                Loading your cases…
                            </p>
                        ) : error ? (
                            <div className="error-banner" role="alert">
                                {error}
                                <button
                                    className="secondary-button"
                                    onClick={() => {
                                        setLoading(true)
                                        setError(null)
                                        setReloadVersion((version) => version + 1)
                                    }}
                                    type="button"
                                >
                                    Retry
                                </button>
                            </div>
                        ) : visibleCount === 0 ? (
                            <div className="empty-state citizen-empty-state">
                                <p className="muted">
                                    {filtersActive
                                        ? 'No cases match the current search and filters.'
                                        : "You haven't submitted any cases yet."}
                                </p>
                                {filtersActive ? (
                                    <button
                                        className="secondary-button"
                                        onClick={() => applyFilters(true)}
                                        type="button"
                                    >
                                        Clear filters
                                    </button>
                                ) : (
                                    <button
                                        className="primary-button"
                                        onClick={onOpenIntake}
                                        type="button"
                                    >
                                        Submit your first case
                                    </button>
                                )}
                            </div>
                        ) : (
                            <div className="directory-grid citizen-request-grid">
                                {pageData?.items
                                    .slice(0, mode === 'cases' ? undefined : 3)
                                    .map((item) => (
                                        <article className="case-card citizen-action-card" key={item.id}>
                                            <div className="case-card-header">
                                                <h3>{item.title}</h3>
                                                <StatusPill status={item.status}/>
                                            </div>
                                            <p className="muted">Reference: {item.referenceNumber}</p>
                                            <p className="muted">Department: {item.organizationName}</p>
                                            <p className="muted">
                                                Submitted: {formatDate(item.createdAt)}
                                            </p>
                                            <p className="muted">
                                                Updated: {formatDateTime(item.updatedAt)}
                                            </p>
                                            <button
                                                className="primary-button"
                                                onClick={() => {
                                                    if (mode === 'cases') {
                                                        void openCase(item.id)
                                                    } else {
                                                        onOpenCases(item.id)
                                                    }
                                                }}
                                                type="button"
                                                disabled={mode === 'cases' && detailLoading}
                                            >
                                                {mode === 'cases'
                                                    ? (detailLoading ? 'Loading…' : 'View details')
                                                    : 'Open case'}
                                            </button>
                                        </article>
                                    ))}
                            </div>
                        )}

                        {mode === 'cases' ? (
                            <div className="pagination">
                                <span>Page {currentPage} of {totalPages}</span>
                                <span>
                                    <button
                                        disabled={currentPage <= 1 || loading}
                                        onClick={() => changePage(Math.max(1, currentPage - 1))}
                                        type="button"
                                    >
                                        Previous
                                    </button>
                                    <button
                                        disabled={currentPage >= totalPages || loading}
                                        onClick={() => changePage(Math.min(totalPages, currentPage + 1))}
                                        type="button"
                                    >
                                        Next
                                    </button>
                                </span>
                            </div>
                        ) : null}
                    </section>

                    {detailError ? (
                        <div className="error-banner" role="alert">{detailError}</div>
                    ) : null}
                </>
            )}

            {mode === 'dashboard' ? (
                <>
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
            ) : null}
        </>
    )
}
