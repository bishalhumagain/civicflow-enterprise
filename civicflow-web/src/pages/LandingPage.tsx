export type LandingIntent = 'citizen' | 'officer'

export function LandingPage({ onSelectIntent }: { onSelectIntent: (intent: LandingIntent) => void }) {
  return (
    <div className="landing-page">
      <header className="landing-header">
        <div className="landing-brand">
          <span className="landing-brand-mark">C</span>
          <span className="landing-brand-name">CivicFlow Enterprise</span>
        </div>
      </header>

      <main className="landing-main">
        <section className="landing-hero">
          <p className="landing-eyebrow">PUBLIC SERVICE PLATFORM</p>
          <h1>CivicFlow helps citizens and government teams resolve public service requests</h1>
          <p className="landing-subtitle">
            CivicFlow helps citizens submit and track public service requests, and helps authorized government staff
            review, assign, and resolve cases securely and transparently.
          </p>
        </section>

        <section className="landing-actions" aria-label="Entry options">
          <article className="landing-card">
            <h2>Login as Citizen</h2>
            <p>Submit requests, track status, and stay informed about your public service cases.</p>
            <button
              className="landing-button primary"
              type="button"
              onClick={() => onSelectIntent('citizen')}
              aria-label="Continue as citizen to sign in"
            >
              Continue as Citizen
            </button>
          </article>

          <article className="landing-card">
            <h2>Login as Government Officer</h2>
            <p>Access the case management workspace to review, assign, and resolve service requests.</p>
            <button
              className="landing-button"
              type="button"
              onClick={() => onSelectIntent('officer')}
              aria-label="Continue as government officer to sign in"
            >
              Continue as Government Officer
            </button>
          </article>
        </section>

        <section className="landing-note">
          <p>
            Selection indicates your intended journey only. Authentication and authorization are handled by Keycloak.
            Roles and permissions are validated on the server.
          </p>
        </section>
      </main>

      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure public service case management</p>
      </footer>
    </div>
  )
}
