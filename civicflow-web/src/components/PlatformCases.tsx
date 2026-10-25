import { useEffect, useMemo, useState } from 'react'
import { apiRequest } from '../api'
import { requestJson } from '../api-client'
import {
    statusLabels,
    statusOptions,
    type CasePriority,
    type CaseStatistics,
    type CaseStatus,
    type CaseUpdateKind,
    type Organization,
    type PlatformCaseDetail,
    type PlatformCaseDocumentEntry,
    type PlatformCasePage,
    type PlatformCaseSummary,
} from '../domain'
import { formatDateTime, formatFileSize, formatNumber } from '../utils/format'
import { Icon } from './Icon'
import { StatCard } from './StatCard'
import { StatusPill } from './StatusPill'

const priorityOptions: CasePriority[] = ['Critical', 'High', 'Normal', 'Low']

const updateKindLabels: Record<CaseUpdateKind, string> = {
    PublicUpdate: 'Public update',
    InformationRequest: 'Information request',
    InternalNote: 'Internal note',
    CitizenResponse: 'Citizen response',
}

const sortOptions = [
    { value: 'created_desc', label: 'Newest first' },
    { value: 'created_asc', label: 'Oldest first' },
    { value: 'updated_desc', label: 'Recently updated' },
    { value: 'priority_desc', label: 'Highest priority' },
    { value: 'reference_asc', label: 'Reference number' },
]

export function PlatformCases() {
    const [organizations, setOrganizations] = useState<Organization[]>([])
    const [statistics, setStatistics] = useState<CaseStatistics | null>(null)
    const [cases, setCases] = useState<PlatformCaseSummary[]>([])
    const [page, setPage] = useState(1)
    const [totalPages, setTotalPages] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [selectedCaseId, setSelectedCaseId] = useState<string | null>(null)
    const [detail, setDetail] = useState<PlatformCaseDetail | null>(null)
    const [searchInput, setSearchInput] = useState('')
    const [search, setSearch] = useState('')
    const [organizationId, setOrganizationId] = useState('All')
    const [status, setStatus] = useState<'All' | CaseStatus>('All')
    const [priority, setPriority] = useState<'All' | CasePriority>('All')
    const [sort, setSort] = useState('created_desc')
    const [loading, setLoading] = useState(false)
    const [detailLoading, setDetailLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [detailError, setDetailError] = useState<string | null>(null)
    const [refreshVersion, setRefreshVersion] = useState(0)

    useEffect(() => {
        const timer = window.setTimeout(() => {
            setSearch(searchInput.trim())
            setPage(1)
        }, 300)

        return () => window.clearTimeout(timer)
    }, [searchInput])

    useEffect(() => {
        let mounted = true

        void requestJson<Organization[]>('/api/organizations')
            .then((loaded) => {
                if (mounted) setOrganizations(loaded)
            })
            .catch(() => {
                // The organization filter degrades to "All organizations";
                // the case request below surfaces any real authorization error.
            })

        return () => {
            mounted = false
        }
    }, [refreshVersion])

    useEffect(() => {
        let mounted = true

        void requestJson<CaseStatistics>('/api/dashboard/platform')
            .then((loaded) => {
                if (mounted) setStatistics(loaded)
            })
            .catch(() => {
                // Summary cards are supplementary. The case table still
                // reports its own load failures.
            })

        return () => {
            mounted = false
        }
    }, [refreshVersion])

    useEffect(() => {
        let mounted = true
        const controller = new AbortController()

        async function loadCases() {
            setLoading(true)
            setError(null)

            const query = new URLSearchParams({
                page: String(page),
                pageSize: '20',
                sort,
            })

            if (organizationId !== 'All') query.set('organizationId', organizationId)
            if (status !== 'All') query.set('status', status)
            if (priority !== 'All') query.set('priority', priority)
            if (search) query.set('search', search)

            try {
                const result = await requestJson<PlatformCasePage>(
                    `/api/platform/cases?${query.toString()}`,
                    { signal: controller.signal },
                )

                if (!mounted) return

                setCases(result.items)
                setTotalPages(Math.max(1, result.totalPages))
                setTotalCount(result.totalCount)
                setSelectedCaseId((current) => (
                    current && result.items.some((item) => item.id === current)
                        ? current
                        : (result.items[0]?.id ?? null)
                ))
            } catch (reason) {
                if (!mounted || controller.signal.aborted) return

                setError(reason instanceof Error
                    ? reason.message
                    : 'The platform case directory could not be loaded.')
            } finally {
                if (mounted) setLoading(false)
            }
        }

        void loadCases()

        return () => {
            mounted = false
            controller.abort()
        }
    }, [
        page,
        organizationId,
        status,
        priority,
        search,
        sort,
        refreshVersion,
    ])

    useEffect(() => {
        if (!selectedCaseId) return

        let mounted = true
        const controller = new AbortController()

        async function loadDetail() {
            setDetailError(null)
            setDetailLoading(true)

            try {
                const loaded = await requestJson<PlatformCaseDetail>(
                    `/api/platform/cases/${selectedCaseId}`,
                    { signal: controller.signal },
                )

                if (mounted) setDetail(loaded)
            } catch (reason) {
                if (!mounted || controller.signal.aborted) return

                setDetailError(reason instanceof Error
                    ? reason.message
                    : 'The case details could not be loaded.')
            } finally {
                if (mounted) setDetailLoading(false)
            }
        }

        void loadDetail()

        return () => {
            mounted = false
            controller.abort()
        }
    }, [selectedCaseId, refreshVersion])

    const activeDetail = detail && detail.case.id === selectedCaseId ? detail : null

    const selectedCase = useMemo(
        () => cases.find((item) => item.id === selectedCaseId) ?? null,
        [cases, selectedCaseId],
    )

    const openCases = statistics
        ? Math.max(
            0,
            statistics.totalCases -
            statistics.resolvedCases -
            statistics.closedCases -
            statistics.rejectedCases,
        )
        : 0

    function resetFilters() {
        setSearchInput('')
        setSearch('')
        setOrganizationId('All')
        setStatus('All')
        setPriority('All')
        setSort('created_desc')
        setPage(1)
    }

    async function downloadDocument(document: PlatformCaseDocumentEntry) {
        if (!selectedCaseId) return

        setError(null)

        try {
            const response = await apiRequest(
                `/api/platform/cases/${selectedCaseId}/documents/${document.id}`,
                '',
            )

            if (response.status === 401) {
                throw new Error('Your session is missing or has expired. Sign in again.')
            }

            if (response.status === 403) {
                throw new Error('You are not authorized to download this document.')
            }

            if (!response.ok) {
                throw new Error('The document could not be downloaded.')
            }

            const url = URL.createObjectURL(await response.blob())
            const anchor = window.document.createElement('a')

            anchor.href = url
            anchor.download = document.originalFileName
            anchor.click()

            URL.revokeObjectURL(url)
        } catch (reason) {
            setError(reason instanceof Error
                ? reason.message
                : 'The document could not be downloaded.')
        }
    }

    return (
        <>
            <div className="page-heading">
                <div>
                    <p className="eyebrow">PLATFORM CASE DIRECTORY</p>
                    <h1>All cases</h1>
                    <p className="muted">
                        Every case across every organization, with filters for
                        organization, status, priority, and free-text search.
                    </p>
                </div>
                <button
                    className="secondary-button"
                    disabled={loading}
                    onClick={() => setRefreshVersion((version) => version + 1)}
                    type="button"
                >
                    <Icon className="button-icon" name="refresh" />
                    <span>Refresh</span>
                </button>
            </div>

            {error && (
                <div className="auth-error" role="alert">
                    {error}
                    <button
                        className="secondary-button"
                        onClick={() => setRefreshVersion((version) => version + 1)}
                        type="button"
                    >
                        Retry
                    </button>
                </div>
            )}

            <section aria-label="Platform case summary" className="stat-grid">
                <StatCard
                    label="Total cases"
                    value={formatNumber(statistics?.totalCases ?? 0)}
                    detail="All organizations"
                    tone="blue"
                />
                <StatCard
                    label="Open cases"
                    value={formatNumber(openCases)}
                    detail="Not resolved, closed, or rejected"
                    tone="amber"
                />
                <StatCard
                    label="Resolved"
                    value={formatNumber(
                        (statistics?.resolvedCases ?? 0) +
                        (statistics?.closedCases ?? 0),
                    )}
                    detail="Resolved or closed"
                    tone="green"
                />
                <StatCard
                    label="Critical priority"
                    value={formatNumber(statistics?.criticalPriorityCases ?? 0)}
                    detail="Requires immediate attention"
                    tone="purple"
                />
            </section>

            <div className="case-layout platform-case-layout">
                <section className="case-card">
                    <div className="case-toolbar platform-case-toolbar">
                        <div className="search">
                            <Icon className="search-icon" name="search" />
                            <input
                                aria-label="Search all platform cases"
                                maxLength={200}
                                placeholder="Search reference, title, or description…"
                                value={searchInput}
                                onChange={(event) => setSearchInput(event.target.value)}
                            />
                        </div>

                        <label className="platform-filter" htmlFor="platform-case-organization">
                            Organization
                        </label>
                        <select
                            id="platform-case-organization"
                            aria-label="Organization"
                            value={organizationId}
                                onChange={(event) => {
                                    setOrganizationId(event.target.value)
                                    setPage(1)
                                }}
                            >
                                <option value="All">All organizations</option>
                                {organizations.map((organization) => (
                                    <option key={organization.id} value={organization.id}>
                                        {organization.name}
                                    </option>
                                ))}
                            </select>

                        <label className="platform-filter" htmlFor="platform-case-status">
                            Status
                        </label>
                        <select
                            id="platform-case-status"
                            aria-label="Status"
                            value={status}
                                onChange={(event) => {
                                    setStatus(event.target.value as 'All' | CaseStatus)
                                    setPage(1)
                                }}
                            >
                                <option value="All">All statuses</option>
                                {statusOptions.map((option) => (
                                    <option key={option} value={option}>
                                        {statusLabels[option]}
                                    </option>
                                ))}
                            </select>

                        <label className="platform-filter" htmlFor="platform-case-priority">
                            Priority
                        </label>
                        <select
                            id="platform-case-priority"
                            aria-label="Priority"
                            value={priority}
                                onChange={(event) => {
                                    setPriority(event.target.value as 'All' | CasePriority)
                                    setPage(1)
                                }}
                            >
                                <option value="All">All priorities</option>
                                {priorityOptions.map((option) => (
                                    <option key={option} value={option}>{option}</option>
                                ))}
                            </select>

                        <label className="platform-filter" htmlFor="platform-case-sort">
                            Sort
                        </label>
                        <select
                            id="platform-case-sort"
                            aria-label="Sort"
                            value={sort}
                                onChange={(event) => {
                                    setSort(event.target.value)
                                    setPage(1)
                                }}
                            >
                                {sortOptions.map((option) => (
                                    <option key={option.value} value={option.value}>
                                        {option.label}
                                    </option>
                                ))}
                            </select>

                        <button
                            className="secondary-button"
                            onClick={resetFilters}
                            type="button"
                        >
                            Reset
                        </button>
                    </div>

                    <div className="section-heading platform-case-count">
                        <div>
                            <h2>Cases</h2>
                            <span className="muted">
                                Showing {formatNumber(cases.length)} of{' '}
                                {formatNumber(totalCount)} matching cases
                            </span>
                        </div>
                    </div>

                    {loading ? (
                        <p aria-live="polite" className="muted empty-state">
                            Loading platform cases…
                        </p>
                    ) : cases.length === 0 ? (
                        <p className="muted empty-state">
                            No cases match the current filters.
                        </p>
                    ) : (
                        <div
                            aria-label="Platform cases list"
                            className="table-wrap"
                            role="region"
                            tabIndex={0}
                        >
                            <table>
                                <thead>
                                    <tr>
                                        <th>CASE</th>
                                        <th>ORGANIZATION</th>
                                        <th>STATUS</th>
                                        <th>PRIORITY</th>
                                        <th>ASSIGNEE</th>
                                        <th>UPDATED</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {cases.map((item) => (
                                        <tr
                                            className={selectedCaseId === item.id ? 'selected-row' : ''}
                                            key={item.id}
                                        >
                                            <td>
                                                <button
                                                    aria-pressed={selectedCaseId === item.id}
                                                    className="case-row-button"
                                                    onClick={() => setSelectedCaseId(item.id)}
                                                    type="button"
                                                >
                                                    <strong>{item.referenceNumber}</strong>
                                                    <span>{item.title}</span>
                                                </button>
                                            </td>
                                            <td>
                                                <div className="platform-case-organization">
                                                    <strong>{item.organizationName}</strong>
                                                    <span>{item.organizationCode}</span>
                                                </div>
                                            </td>
                                            <td><StatusPill status={item.status}/></td>
                                            <td>
                                                <span className={`priority ${item.priority.toLowerCase()}`}>
                                                    <i/>{item.priority}
                                                </span>
                                            </td>
                                            <td>{item.assignedToName ?? 'Unassigned'}</td>
                                            <td>{formatDateTime(item.updatedAt)}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    <div className="pagination">
                        <span>Page {page} of {totalPages}</span>
                        <span>
                            <button
                                disabled={page <= 1 || loading}
                                onClick={() => setPage((current) => Math.max(1, current - 1))}
                                type="button"
                            >
                                Previous
                            </button>
                            <button
                                disabled={page >= totalPages || loading}
                                onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
                                type="button"
                            >
                                Next
                            </button>
                        </span>
                    </div>
                </section>

                <aside className="detail-card platform-case-detail">
                    <div className="detail-header">
                        <span>PLATFORM CASE DETAILS</span>
                    </div>

                    {detailError && (
                        <p className="field-error" role="alert">{detailError}</p>
                    )}

                    {selectedCaseId && detailLoading ? (
                        <p aria-live="polite" className="muted empty-state">
                            Loading case details…
                        </p>
                    ) : !selectedCase ? (
                        <p className="muted empty-state">
                            Select a case to view its details.
                        </p>
                    ) : (
                        <>
                            <div className="detail-title">
                                <span className="case-reference">
                                    {selectedCase.referenceNumber}
                                </span>
                                <StatusPill status={selectedCase.status}/>
                                <h2 className="case-details-title">
                                    {selectedCase.title}
                                </h2>
                                <p className="case-description">
                                    {selectedCase.description}
                                </p>
                            </div>

                            <div className="detail-section">
                                <h3>Overview</h3>
                                <dl>
                                    <div>
                                        <dt>Organization</dt>
                                        <dd>
                                            {selectedCase.organizationName}{' '}
                                            <small className="muted">
                                                ({selectedCase.organizationCode})
                                            </small>
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>Priority</dt>
                                        <dd>{selectedCase.priority}</dd>
                                    </div>
                                    <div>
                                        <dt>Submitted by</dt>
                                        <dd>
                                            {activeDetail?.submittedBy
                                                ? `${activeDetail.submittedBy.fullName} · ${activeDetail.submittedBy.email}`
                                                : (selectedCase.submittedByName ?? 'Not recorded')}
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>Assigned to</dt>
                                        <dd>
                                            {activeDetail?.assignedTo
                                                ? `${activeDetail.assignedTo.fullName} · ${activeDetail.assignedTo.email}`
                                                : (selectedCase.assignedToName ?? 'Unassigned')}
                                        </dd>
                                    </div>
                                    <div>
                                        <dt>Created</dt>
                                        <dd>{formatDateTime(selectedCase.createdAt)}</dd>
                                    </div>
                                    <div>
                                        <dt>Updated</dt>
                                        <dd>{formatDateTime(selectedCase.updatedAt)}</dd>
                                    </div>
                                </dl>
                            </div>

                            <div className="detail-section">
                                <h3>Documents</h3>
                                {!activeDetail || activeDetail.documents.length === 0 ? (
                                    <p className="muted">No documents attached.</p>
                                ) : (
                                    <ul>
                                        {activeDetail.documents.map((document) => (
                                            <li key={document.id}>
                                                <button
                                                    className="document-link"
                                                    onClick={() => void downloadDocument(document)}
                                                    type="button"
                                                >
                                                    {document.originalFileName}
                                                </button>{' '}
                                                <small>{formatFileSize(document.sizeBytes)}</small>{' '}
                                                <span
                                                    className={
                                                        document.isVisibleToCitizen
                                                            ? 'visibility-badge visible'
                                                            : 'visibility-badge internal'
                                                    }
                                                >
                                                    {document.isVisibleToCitizen
                                                        ? 'Shared with citizen'
                                                        : 'Internal'}
                                                </span>
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </div>

                            <div className="detail-section">
                                <h3>Updates</h3>
                                {!activeDetail || activeDetail.updates.length === 0 ? (
                                    <p className="muted">No updates recorded yet.</p>
                                ) : (
                                    <ul className="case-update-list">
                                        {activeDetail.updates.map((update) => (
                                            <li
                                                className={
                                                    update.isVisibleToCitizen
                                                        ? 'case-update visible'
                                                        : 'case-update internal'
                                                }
                                                key={update.id}
                                            >
                                                <div className="case-update-header">
                                                    <strong>{updateKindLabels[update.kind]}</strong>
                                                    <span
                                                        className={
                                                            update.isVisibleToCitizen
                                                                ? 'visibility-badge visible'
                                                                : 'visibility-badge internal'
                                                        }
                                                    >
                                                        {update.isVisibleToCitizen
                                                            ? 'Visible to citizen'
                                                            : 'Staff only'}
                                                    </span>
                                                </div>
                                                <p>{update.message}</p>
                                                <small>
                                                    {update.authorEmail ?? 'System'}
                                                    {' · '}
                                                    {formatDateTime(update.createdAt)}
                                                </small>
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </div>

                            <div className="detail-section timeline">
                                <h3>Status history</h3>
                                {!activeDetail || activeDetail.statusHistory.length === 0 ? (
                                    <p className="muted">No recorded status changes yet.</p>
                                ) : (
                                    activeDetail.statusHistory.map((item) => (
                                        <p key={item.id}>
                                            <span className="timeline-dot"/>
                                            <span>
                                                <strong>
                                                    {statusLabels[item.previousStatus]}
                                                    {' → '}
                                                    {statusLabels[item.newStatus]}
                                                </strong>
                                                <small>
                                                    {item.changedByEmail ?? 'System'}
                                                    {' · '}
                                                    {formatDateTime(item.changedAt)}
                                                </small>
                                                {item.explanation && (
                                                    <span className="timeline-explanation">
                                                        {item.explanation}
                                                    </span>
                                                )}
                                            </span>
                                        </p>
                                    ))
                                )}
                            </div>

                            <div className="detail-section timeline">
                                <h3>Assignment history</h3>
                                {!activeDetail || activeDetail.assignmentHistory.length === 0 ? (
                                    <p className="muted">No recorded assignments yet.</p>
                                ) : (
                                    activeDetail.assignmentHistory.map((item) => (
                                        <p key={item.id}>
                                            <span className="timeline-dot"/>
                                            <span>
                                                <strong>
                                                    {item.previousStaffMemberId
                                                        ? 'Reassigned case'
                                                        : 'Case assigned'}
                                                    {' · '}
                                                    {item.newStaffMemberName ?? 'Unknown staff member'}
                                                    {' ('}{item.newRole}{')'}
                                                </strong>
                                                <small>
                                                    {formatDateTime(item.assignedAt)}
                                                </small>
                                            </span>
                                        </p>
                                    ))
                                )}
                            </div>
                        </>
                    )}
                </aside>
            </div>
        </>
    )
}
