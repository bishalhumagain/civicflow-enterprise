import type { ReactNode } from 'react'

export type LandingPageProps = {
  /** Citizen sign-in (existing account). */
  onCitizenSignIn?: () => void
  /** Citizen registration (new account). */
  onCitizenRegister?: () => void
  /** Navigate to the separate staff/organization portal URL. */
  onOrganizationSignIn?: () => void
  loading?: boolean
  /** Optional operator-facing notice shown when sign-in is temporarily unavailable. */
  configurationWarning?: string | null
}

function Card({
  icon,
  title,
  description,
  actions,
}: {
  icon: ReactNode
  title: string
  description: string
  actions: ReactNode
}) {
  return (
    <article className="landing-card">
      <div className="landing-card-icon" aria-hidden="true">
        {icon}
      </div>
      <h2>{title}</h2>
      <p>{description}</p>
      <div className="landing-card-actions">{actions}</div>
    </article>
  )
}

export function LandingPage({
  onCitizenSignIn,
  onCitizenRegister,
  onOrganizationSignIn,
  loading = false,
  configurationWarning = null,
}: LandingPageProps) {
  return (
    <div className="landing-page">
      <header className="landing-header">
        <div className="landing-brand">
          <span className="landing-brand-mark">C</span>
          <span className="landing-brand-name">CivicFlow</span>
        </div>
      </header>
      <main className="landing-main">
        {configurationWarning && (
          <div className="auth-error" role="alert" style={{ marginBottom: 20 }}>
            {configurationWarning}
          </div>
        )}
        <section className="landing-hero">
          <p className="landing-eyebrow">PUBLIC SERVICE CASE MANAGEMENT</p>
          <h1>Manage public requests with clarity and accountability</h1>
          <p className="landing-subtitle">
            CivicFlow helps government teams track, assign, and resolve cases while providing citizens a simple way to
            submit service requests. All access is validated server-side for security and compliance.
          </p>
        </section>
        <section className="landing-actions landing-actions-compact">
          <Card
            icon={<span>C</span>}
            title="Citizen Portal"
            description="Submit service requests and manage your citizen profile. Case tracking is not yet available."
            actions={
              <>
                <button
                  className="landing-button primary"
                  type="button"
                  onClick={onCitizenSignIn}
                  disabled={loading}
                >
                  {loading ? 'Please wait…' : 'Sign in'}
                </button>
                <button
                  className="landing-button"
                  type="button"
                  onClick={onCitizenRegister}
                  disabled={loading}
                >
                  Register
                </button>
              </>
            }
          />
          <Card
            icon={<span>O</span>}
            title="Organization Portal"
            description="Staff and Organization Administrators access the organization workspace."
            actions={
              <button
                className="landing-button primary"
                type="button"
                onClick={onOrganizationSignIn}
                disabled={loading}
              >
                Sign in
              </button>
            }
          />
        </section>
        <section className="landing-note">
          <p>
            Platform administration is available at a separate, dedicated sign-in URL. Authentication and role-based
            permissions are enforced by the CivicFlow API and remain authoritative.
          </p>
        </section>
      </main>
      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
      </footer>
    </div>
  )
}

export default LandingPage
