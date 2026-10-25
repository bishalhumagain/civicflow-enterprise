import { useState } from 'react'
import { type ApplicationError } from '../domain'
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
  onMarkAsHandled,
}: {
  errors: ApplicationError[]
  loading: boolean
  onMarkAsHandled: (errorId: string) => Promise<void>
}) {
  const [filter, setFilter] = useState<ErrorFilter>('all')

  const filteredErrors = errors.filter(error => {
    if (filter === 'unhandled') return !error.isHandled
    if (filter === 'handled') return error.isHandled
    return true
  })

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
              onChange={(event) => setFilter(event.target.value as ErrorFilter)}
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
              {filterLabel(filter)} · {filteredErrors.length} shown
            </p>
          </div>
        </div>

        {loading ? (
          <p className="muted empty-state">Loading application errors…</p>
        ) : filteredErrors.length === 0 ? (
          <p className="muted empty-state">No application errors match this filter.</p>
        ) : (
          <div aria-label="Application errors" className="table-wrap" role="region" tabIndex={0}>
            <table>
              <thead>
                <tr>
                  <th>ERROR</th>
                  <th>REQUEST</th>
                  <th>USER</th>
                  <th>OCCURRED</th>
                  <th>STATUS</th>
                  <th>ACTION</th>
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
                    <td>
                      {error.isHandled ? (
                        <span className="muted">—</span>
                      ) : (
                        <button
                          className="secondary-button"
                          disabled={loading}
                          onClick={() => void onMarkAsHandled(error.id)}
                          type="button"
                        >
                          Mark as handled
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
