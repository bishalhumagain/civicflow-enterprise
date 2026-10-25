import { useState, type FormEvent } from 'react'
import type { AuditEvent } from '../domain'
import { auditCategories } from '../domain'
import { emptyAuditFilters, type AuditFilters } from '../utils/audit'
import { Icon } from './Icon'
import { formatDateTime } from '../utils/format'

/**
 * Read-only view of the audit trail. The component never writes anything, it
 * only renders the events the API returned and reports filter changes to the
 * caller, which owns the request.
 */
export function AuditLog({
  title,
  description,
  loading,
  events,
  filters,
  totalCount,
  totalPages,
  page,
  onApplyFilters,
  onPageChange,
  emptyMessage = 'No audit events match the current filters.',
}: {
  title: string
  description: string
  loading: boolean
  events: AuditEvent[]
  filters: AuditFilters
  totalCount: number
  totalPages: number
  page: number
  onApplyFilters: (filters: AuditFilters) => void
  onPageChange: (page: number) => void
  emptyMessage?: string
}) {
  const [category, setCategory] = useState(filters.category)
  const [action, setAction] = useState(filters.action)
  const [search, setSearch] = useState(filters.search)
  const [from, setFrom] = useState(filters.from)
  const [to, setTo] = useState(filters.to)

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    onApplyFilters({
      category,
      action: action.trim(),
      search: search.trim(),
      from,
      to,
    })
  }

  function handleReset() {
    setCategory('')
    setAction('')
    setSearch('')
    setFrom('')
    setTo('')
    onApplyFilters(emptyAuditFilters)
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">AUDIT TRAIL</p>
          <h1>{title}</h1>
          <p className="muted">{description}</p>
        </div>
      </div>

      <section className="case-card admin-panel">
        <h2>Filters</h2>

        <form className="data-grid-toolbar data-grid-toolbar--inset" onSubmit={handleSubmit}>
          <label className="data-grid-filter">
            <span>Category</span>
            <select
              aria-label="Filter by category"
              value={category}
              onChange={(event) => setCategory(event.target.value)}
            >
              <option value="">All categories</option>
              {auditCategories.map((option) => (
                <option key={option} value={option}>{option}</option>
              ))}
            </select>
          </label>

          <label className="data-grid-filter">
            <span>Action</span>
            <input
              aria-label="Filter by action"
              placeholder="e.g. case.created"
              value={action}
              onChange={(event) => setAction(event.target.value)}
            />
          </label>

          <label className="data-grid-filter">
            <span>Search</span>
            <input
              aria-label="Search the audit trail"
              placeholder="Actor, entity, or details"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>

          <label className="data-grid-filter">
            <span>From</span>
            <input
              aria-label="From date"
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
            />
          </label>

          <label className="data-grid-filter">
            <span>To</span>
            <input
              aria-label="To date"
              type="date"
              value={to}
              onChange={(event) => setTo(event.target.value)}
            />
          </label>

          <div className="data-grid-toolbar-actions">
            <button className="primary-button" disabled={loading} type="submit">
              <Icon className="button-icon" name="filter" />
              <span>{loading ? 'Loading…' : 'Apply filters'}</span>
            </button>
            <button
              className="secondary-button"
              disabled={loading}
              onClick={handleReset}
              type="button"
            >
              <Icon className="button-icon" name="refresh" />
              <span>Reset</span>
            </button>
          </div>
        </form>
      </section>

      <section className="case-card">
        <div className="section-heading list-toolbar">
          <div>
            <h2>Events</h2>
            <span className="muted">
              {totalCount} event{totalCount === 1 ? '' : 's'} recorded
            </span>
          </div>
        </div>

        {loading ? (
          <p className="muted empty-state">Loading audit events…</p>
        ) : events.length === 0 ? (
          <p className="muted empty-state">{emptyMessage}</p>
        ) : (
          <div aria-label="Audit events" className="table-wrap" role="region" tabIndex={0}>
            <table className="data-grid-table">
              <thead>
                <tr>
                  <th scope="col">OCCURRED</th>
                  <th scope="col">CATEGORY</th>
                  <th scope="col">ACTION</th>
                  <th scope="col">ACTOR</th>
                  <th scope="col">ORGANIZATION</th>
                  <th scope="col">DETAILS</th>
                </tr>
              </thead>
              <tbody>
                {events.map((event) => (
                  <tr key={event.id}>
                    <td>{formatDateTime(event.occurredAt)}</td>
                    <td><strong>{event.category}</strong></td>
                    <td><strong>{event.action}</strong></td>
                    <td>
                      <strong>{event.actorEmail || 'System'}</strong>
                      {event.entityType && (
                        <span>{event.entityType}</span>
                      )}
                    </td>
                    <td>{event.organizationName ?? '—'}</td>
                    <td>{event.details ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="pagination">
          <span>Page {page} of {Math.max(1, totalPages)}</span>
          <span>
            <button
              disabled={page <= 1 || loading}
              onClick={() => onPageChange(page - 1)}
              type="button"
            >
              Previous
            </button>
            <button
              disabled={page >= totalPages || loading}
              onClick={() => onPageChange(page + 1)}
              type="button"
            >
              Next
            </button>
          </span>
        </div>
      </section>
    </>
  )
}
