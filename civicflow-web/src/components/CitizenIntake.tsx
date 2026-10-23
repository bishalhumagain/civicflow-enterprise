import { useEffect, useState, type FormEvent } from 'react'
import { requestJson } from '../api-client'

type CitizenOrganization = { id: string; name: string; code: string }
type CitizenCaseSubmission = { referenceNumber: string; additionalDocument: { fileName: string } | null }

export function CitizenIntake() {
  const [organizations, setOrganizations] = useState<CitizenOrganization[]>([])
  const [organizationId, setOrganizationId] = useState('')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [document, setDocument] = useState<File | null>(null)
  const [loadingOrganizations, setLoadingOrganizations] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [retryVersion, setRetryVersion] = useState(0)

  useEffect(() => {
    let mounted = true

    void requestJson<CitizenOrganization[]>(
      '/api/citizens/organizations',
      undefined,
      '',
    )
      .then((loaded) => {
        if (!mounted) return
        setOrganizations(loaded)
        setOrganizationId((current) =>
          current && loaded.some((item) => item.id === current)
            ? current
            : loaded[0]?.id ?? '')
        setError(null)
      })
      .catch((reason: unknown) => {
        if (mounted) {
          setError(reason instanceof Error
            ? reason.message
            : 'Organizations could not be loaded.')
        }
      })
      .finally(() => {
        if (mounted) setLoadingOrganizations(false)
      })

    return () => {
      mounted = false
    }
  }, [retryVersion])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    if (!organizationId) {
      setError('Select an organization before submitting.')
      return
    }

    const payload = new FormData()
    payload.set('organizationId', organizationId)
    payload.set('title', title.trim())
    payload.set('description', description.trim())
    if (document) payload.set('additionalDocument', document)

    setSubmitting(true)
    setError(null)
    setSuccess(null)

    try {
      const result = await requestJson<CitizenCaseSubmission>(
        '/api/citizens/cases',
        { method: 'POST', body: payload },
        '',
      )
      setSuccess(
        `Request ${result.referenceNumber} was submitted successfully` +
        (result.additionalDocument
          ? ` with ${result.additionalDocument.fileName}.`
          : '.'),
      )
      setTitle('')
      setDescription('')
      setDocument(null)
      form.reset()
    } catch (reason) {
      setError(reason instanceof Error
        ? reason.message
        : 'Your request could not be submitted. Please retry.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PUBLIC SERVICE PORTAL</p>
          <h1>Request public assistance</h1>
          <p className="muted">Select the responsible organization, describe the issue, and attach any supporting file.</p>
        </div>
      </div>

      <section className="case-card citizen-card">
        <form onSubmit={(event) => void submit(event)}>
          <label>
            Organization
            <select
              required
              disabled={loadingOrganizations || organizations.length === 0}
              value={organizationId}
              onChange={(event) => setOrganizationId(event.target.value)}
            >
              <option value="">Select an organization</option>
              {organizations.map((organization) => (
                <option key={organization.id} value={organization.id}>
                  {organization.name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Issue title
            <input
              required
              minLength={3}
              maxLength={200}
              placeholder="What do you need help with?"
              value={title}
              onChange={(event) => setTitle(event.target.value)}
            />
          </label>
          <label>
            Details
            <textarea
              required
              minLength={10}
              maxLength={5000}
              placeholder="Include location, dates, and relevant context."
              value={description}
              onChange={(event) => setDescription(event.target.value)}
            />
          </label>
          <label>
            Additional document (optional, any file type; maximum 10 MB)
            <input
              type="file"
              onChange={(event) => setDocument(event.target.files?.[0] ?? null)}
            />
          </label>
          {error && (
            <div className="auth-error" role="alert">
              {error}
              <button
                className="secondary-button"
                disabled={loadingOrganizations || submitting}
                onClick={() => {
                  setLoadingOrganizations(true)
                  setRetryVersion((version) => version + 1)
                }}
                type="button"
              >
                Retry
              </button>
            </div>
          )}
          {success && <p className="success-message" role="status">{success}</p>}
          {organizations.length === 0 && !loadingOrganizations && !error && (
            <p className="muted">No active organizations are accepting requests.</p>
          )}
          <button
            className="primary-button"
            disabled={loadingOrganizations || submitting || organizations.length === 0}
            type="submit"
          >
            {submitting ? 'Submitting…' : 'Submit request'}
          </button>
        </form>
      </section>
    </>
  )
}
