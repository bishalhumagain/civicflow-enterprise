import { useState } from 'react'

export function CitizenIntake() {
  const [submitted, setSubmitted] = useState(false)

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PUBLIC SERVICE PORTAL</p>
          <h1>Request public assistance</h1>
          <p className="muted">Describe a local service issue and provide enough detail for the responsible team.</p>
        </div>
      </div>

      <section className="case-card citizen-card">
        <div>
          <h2>Citizen intake foundation</h2>
          <p>This public-facing form is prepared for the future citizen submission contract. It does not submit to the staff API and cannot expose internal case data.</p>
        </div>

        <form onSubmit={(event) => {
          event.preventDefault()
          setSubmitted(true)
        }}>
          <label>
            Issue title
            <input required minLength={3} maxLength={200} placeholder="What do you need help with?" />
          </label>
          <label>
            Details
            <textarea required minLength={10} maxLength={5000} placeholder="Include location, dates, and relevant context." />
          </label>
          <button className="primary-button" type="submit">
            {submitted ? 'Submission unavailable' : 'Preview request'}
          </button>
          {submitted && (
            <p className="auth-error" role="status">
              Citizen intake is not enabled yet. A dedicated citizen API, organization-resolution policy, and document authorization must be implemented before requests can be submitted.
            </p>
          )}
        </form>
      </section>
    </>
  )
}
