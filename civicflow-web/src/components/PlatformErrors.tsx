import { useState } from 'react'
import { type ApplicationError } from '../domain'

export function PlatformErrors({
  errors,
  loading,
  onMarkAsHandled,
}: {
  errors: ApplicationError[]
  loading: boolean
  onMarkAsHandled: (errorId: string) => Promise<void>
}) {
  const [filter, setFilter] = useState<'all' | 'unhandled' | 'handled'>('all')

  const filteredErrors = errors.filter(error => {
    if (filter === 'all') return true
    if (filter === 'unhandled') return !error.isHandled
    if (filter === 'handled') return error.isHandled
    return true
  })

  if (loading) {
    return <div className="muted">Loading errors...</div>
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PLATFORM ERRORS</p>
          <h1>Application Errors</h1>
          <p className="muted">View and manage application errors across the platform</p>
        </div>
      </div>

      <section className="case-card">
        <div style={{ marginBottom: '16px', display: 'flex', gap: '8px', alignItems: 'center' }}>
          <label>Filter:</label>
          <select
            value={filter}
            onChange={(e) => setFilter(e.target.value as 'all' | 'unhandled' | 'handled')}
          >
            <option value="all">All Errors</option>
            <option value="unhandled">Unhandled</option>
            <option value="handled">Handled</option>
          </select>
        </div>

        {filteredErrors.length === 0 ? (
          <p className="muted empty-state">No errors found.</p>
        ) : (
          <div className="membership-list">
            {filteredErrors.map((error) => (
              <div className="membership-row" key={error.id}>
                <div style={{ minWidth: 300, flex: 1 }}>
                  <strong>{error.message}</strong>
                  <small className="muted" style={{ display: 'block' }}>
                    {new Date(error.occurredAt).toLocaleString()}
                  </small>
                  {error.requestPath && (
                    <small className="muted" style={{ display: 'block' }}>
                      {error.requestMethod} {error.requestPath}
                    </small>
                  )}
                  {error.userEmail && (
                    <small className="muted" style={{ display: 'block' }}>
                      User: {error.userEmail}
                    </small>
                  )}
                  {error.source && (
                    <small className="muted" style={{ display: 'block' }}>
                      Source: {error.source}
                    </small>
                  )}
                </div>
                <span>{error.isHandled ? 'Handled' : 'Unhandled'}</span>
                {!error.isHandled && (
                  <button
                    className="secondary-button"
                    disabled={loading}
                    onClick={() => void onMarkAsHandled(error.id)}
                    type="button"
                  >
                    Mark as Handled
                  </button>
                )}
              </div>
            ))}
          </div>
        )}
      </section>
    </>
  )
}
