import { useCallback, useEffect, useState } from 'react'
import { apiRequest } from '../api'
import { Icon } from './Icon'
import { formatDateTime } from '../utils/format'

type JobSummary = {
  id: string
  type: string
  status: string
  correlationId: string
  entityType: string | null
  entityId: string | null
  createdAt: string
  lastAttemptAt: string | null
  completedAt: string | null
  attemptCount: number
  maxAttempts: number
  errorCode: string | null
  errorDetails: string | null
}

type JobPage = { items: JobSummary[]; page: number; pageSize: number; totalCount: number }

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await apiRequest(path, '', init)
  if (!response.ok) throw new Error(`Request failed (${response.status}).`)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export function BackgroundJobsPage() {
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [result, setResult] = useState<JobPage | null>(null)
  const [loading, setLoading] = useState(true)
  const [changingJobId, setChangingJobId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const query = new URLSearchParams({ page: String(page), pageSize: '25' })
      if (status) query.set('status', status)
      setResult(await request<JobPage>(`/api/platform/jobs?${query}`))
    } catch {
      setError('Job information could not be loaded. Check your access and retry.')
    } finally {
      setLoading(false)
    }
  }, [page, status])

  useEffect(() => {
    const timer = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const changeJob = async (job: JobSummary, action: 'cancel' | 'retry') => {
    const actionLabel = action === 'retry' ? 'retry' : 'cancel'
    if (!window.confirm(`Are you sure you want to ${actionLabel} job ${job.id}?`)) return

    setError(null)
    setChangingJobId(job.id)
    try {
      await request(`/api/platform/jobs/${job.id}/${action}`, { method: 'POST' })
      await load()
    } catch {
      setError(`The job could not be ${action === 'retry' ? 'retried' : 'cancelled'}.`)
    } finally {
      setChangingJobId(null)
    }
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PLATFORM OPERATIONS</p>
          <h1>Background jobs</h1>
          <p className="muted">Delivery status and sanitized execution failures.</p>
        </div>
        <div className="page-actions">
          <label className="page-filter">
            Status
            <select value={status} onChange={(event) => { setPage(1); setStatus(event.target.value) }}>
              <option value="">All statuses</option>
              {['Queued', 'Running', 'Succeeded', 'RetryScheduled', 'Failed', 'Cancelled'].map((value) => (
                <option key={value} value={value}>{value}</option>
              ))}
            </select>
          </label>
          <button className="secondary-button" disabled={loading} onClick={() => void load()} type="button">
            <Icon className="button-icon" name="refresh" />
            <span>Refresh</span>
          </button>
        </div>
      </div>

      <section aria-busy={loading} className="case-card jobs-table-card">
        <div className="section-heading list-toolbar">
          <div>
            <h2>Job queue</h2>
            <p className="muted">{result?.totalCount ?? 0} total jobs · page {page}</p>
          </div>
        </div>

        {error && (
          <div className="auth-error" role="alert">
            {error}
            <button className="document-link" onClick={() => void load()} type="button">Retry</button>
          </div>
        )}

        {loading ? (
          <p className="muted empty-state">Loading jobs…</p>
        ) : !result?.items.length ? (
          <p className="muted empty-state">No jobs found.</p>
        ) : (
          <div aria-label="Background jobs" className="table-wrap jobs-table" role="region" tabIndex={0}>
            <table className="data-grid-table">
              <thead>
                <tr>
                  <th scope="col">TYPE / ENTITY</th>
                  <th scope="col">STATUS</th>
                  <th scope="col">ATTEMPTS</th>
                  <th scope="col">CREATED / UPDATED</th>
                  <th scope="col">DETAILS</th>
                  <th className="data-grid-actions-column" scope="col">ACTIONS</th>
                </tr>
              </thead>
              <tbody>
                {result.items.map((job) => (
                  <tr key={job.id}>
                    <td><strong>{job.type}</strong><span>{job.entityType ?? '—'} {job.entityId ?? ''}</span></td>
                    <td>
                      <span
                        aria-label={`Job status: ${job.status === 'Running' ? 'Processing' : job.status === 'RetryScheduled' ? 'Retrying' : job.status}`}
                        className={`job-status job-status-${job.status.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`}
                      >
                        {job.status === 'Running' ? 'Processing' : job.status === 'RetryScheduled' ? 'Retrying' : job.status}
                      </span>
                    </td>
                    <td>{job.attemptCount} / {job.maxAttempts}</td>
                    <td><span>{formatDateTime(job.createdAt)}</span><small>{job.lastAttemptAt ? formatDateTime(job.lastAttemptAt) : 'Not attempted'}</small></td>
                    <td>{job.errorCode && <><strong>{job.errorCode}</strong><span>{job.errorDetails}</span></>}</td>
                    <td className="data-grid-actions-column">
                      {job.status === 'Failed' && (
                        <button
                          className="secondary-button"
                          disabled={changingJobId !== null}
                          onClick={() => void changeJob(job, 'retry')}
                          type="button"
                        >
                          <Icon className="button-icon" name="refresh" />
                          <span>{changingJobId === job.id ? 'Retrying…' : 'Retry'}</span>
                        </button>
                      )}
                      {['Queued', 'RetryScheduled'].includes(job.status) && (
                        <button
                          className="secondary-button"
                          disabled={changingJobId !== null}
                          onClick={() => void changeJob(job, 'cancel')}
                          type="button"
                        >
                          <Icon className="button-icon" name="x" />
                          <span>{changingJobId === job.id ? 'Cancelling…' : 'Cancel'}</span>
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
          <span>Page {page} · {result?.totalCount ?? 0} total</span>
          <span>
            <button disabled={page <= 1 || loading} onClick={() => setPage((value) => value - 1)} type="button">Previous</button>
            <button disabled={!result || page * result.pageSize >= result.totalCount || loading} onClick={() => setPage((value) => value + 1)} type="button">Next</button>
          </span>
        </div>
      </section>
    </>
  )
}
