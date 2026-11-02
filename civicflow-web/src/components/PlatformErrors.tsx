import { type ApplicationError } from '../domain'
import { Icon } from './Icon'
import { formatDateTime } from '../utils/format'

type ErrorFilter = 'all' | 'unhandled' | 'handled'

function filterLabel(filter: ErrorFilter): string {
  if (filter === 'unhandled') return 'Unhandled'
  if (filter === 'handled') return 'Handled'
  return 'All errors'
}

export function PlatformErrors({
  errors,
  loading,
  page,
  pageCount,
  totalCount,
  filter,
  onPageChange,
  onFilterChange,
  onMarkAsHandled,
}: {
  errors: ApplicationError[]
  loading: boolean
  page: number
  pageCount: number
  totalCount: number
  filter: ErrorFilter
  onPageChange: (page: number) => void
  onFilterChange: (filter: ErrorFilter) => void
  onMarkAsHandled: (errorId: string) => Promise<void>
}) {
  const filteredErrors = errors
  const rangeStart = totalCount === 0 ? 0 : (page - 1) * 25 + 1
  const rangeEnd = Math.min(page * 25, totalCount)

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PLATFORM MONITORING</p>
          <h1>Application errors</h1>
          <p className="muted">Review failures across the platform and mark resolved items as handled.</p>
        </div>
        <div className="page-actions">
          <label className="page-filter">
            Status
            <select
              aria-label="Filter application errors"
              value={filter}
              onChange={(event) => onFilterChange(event.target.value as ErrorFilter)}
            >
              <option value="all">All errors</option>
              <option value="unhandled">Unhandled</option>
              <option value="handled">Handled</option>
            </select>
          </label>
        </div>
      </div>

      <section aria-busy={loading} className="case-card">
        <div className="section-heading list-toolbar">
          <div>
            <h2>Error log</h2>
            <p className="muted">
              {filterLabel(filter)} · {rangeStart}–{rangeEnd} of {totalCount}
            </p>
          </div>
        </div>

        {loading ? (
          <p className="muted empty-state">Loading application errors…</p>
        ) : filteredErrors.length === 0 ? (
          <p className="muted empty-state">No application errors match this filter.</p>
        ) : (
          <div aria-label="Application errors" className="table-wrap" role="region" tabIndex={0}>
            <table className="data-grid-table">
              <thead>
                <tr>
                  <th scope="col">ERROR</th>
                  <th scope="col">REQUEST</th>
                  <th scope="col">USER</th>
                  <th scope="col">OCCURRED</th>
                  <th scope="col">STATUS</th>
                  <th className="data-grid-actions-column" scope="col">ACTION</th>
                </tr>
              </thead>
              <tbody>
                {filteredErrors.map((error) => (
                  <tr key={error.id}>
                    <td className="error-cell">
                      <strong>{error.message}</strong>
                      {error.source && <span>Source: {error.source}</span>}
                    </td>
                    <td>
                      {error.requestPath ? (
                        <>
                          <strong>{error.requestMethod ?? 'REQUEST'}</strong>
                          <span>{error.requestPath}</span>
                        </>
                      ) : (
                        <span className="muted">—</span>
                      )}
                    </td>
                    <td>{error.userEmail ?? <span className="muted">Anonymous</span>}</td>
                    <td>{formatDateTime(error.occurredAt)}</td>
                    <td>
                      <span className={`status-pill ${error.isHandled ? 'resolved' : 'rejected'}`}>
                        <i />
                        {error.isHandled ? 'Handled' : 'Unhandled'}
                      </span>
                    </td>
                    <td className="data-grid-actions-column">
                      {error.isHandled ? (
                        <span className="muted">—</span>
                      ) : (
                        <button
                          className="secondary-button"
                          disabled={loading}
                          onClick={() => void onMarkAsHandled(error.id)}
                          type="button"
                        >
                          <Icon className="button-icon" name="check" />
                          <span>Mark as handled</span>
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <div className="pagination">
          <span>{rangeStart}–{rangeEnd} of {totalCount} errors</span>
          <span>Page {page} of {pageCount}</span>
          <span>
            <button disabled={page <= 1 || loading} onClick={() => onPageChange(page - 1)} type="button">Previous</button>
            <button disabled={page >= pageCount || loading} onClick={() => onPageChange(page + 1)} type="button">Next</button>
          </span>
        </div>
      </section>
    </>
  )
}
