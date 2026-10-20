import { useState, type ReactNode } from 'react'
import { LoginForm } from '../components/LoginForm'

export type LandingIntent = 'citizen' | 'officer'

type AuthMode = 'none' | 'signin'

export type LandingPageProps = {
  onSelectIntent?: (intent: LandingIntent) => void
  onLoginSubmit?: (credentials: { username: string; password: string }) => void
  loginLoading?: boolean
  loginError?: string | null
}

function Card({
  icon,
  title,
  description,
  actionLabel,
  onClick,
}: {
  icon: ReactNode
  title: string
  description: string
  actionLabel: string
  onClick: () => void
}) {
  return (
    <article className="landing-card">
      <div className="landing-brand" style={{ fontSize: 28 }}>
        {icon}
      </div>
      <h2>{title}</h2>
      <p>{description}</p>
      <button className="landing-button primary" type="button" onClick={onClick}>
        {actionLabel}
      </button>
    </article>
  )
}

export function LandingPage({
  onSelectIntent,
  onLoginSubmit,
  loginLoading = false,
  loginError = null,
}: LandingPageProps) {
  const [authMode, setAuthMode] = useState<AuthMode>('none')

  const handleCitizenClick = () => {
    onSelectIntent?.('citizen')
    // Stay on landing; public intake entry remains accessible after auth
  }

  const handleOfficerClick = () => {
    setAuthMode('signin')
  }

  const handleBack = () => {
    setAuthMode('none')
  }

  return (
    <div className="landing-page">
      <header className="landing-header">
        <div className="landing-brand">
          <span className="landing-brand-mark">C</span>
          <span className="landing-brand-name">CivicFlow</span>
        </div>
      </header>
      <main className="landing-main">
        {authMode === 'none' ? (
          <>
            <section className="landing-hero">
              <p className="landing-eyebrow">PUBLIC SERVICE CASE MANAGEMENT</p>
              <h1>Manage public requests with clarity and accountability</h1>
              <p className="landing-subtitle">
                CivicFlow helps government teams track, assign, and resolve cases while providing citizens a simple way to
                submit service requests. All access is validated server-side for security and compliance.
              </p>
            </section>
            <section className="landing-actions">
              <Card
                icon={<span>👤</span>}
                title="Citizen Portal"
                description="Submit service requests and track their progress."
                actionLabel="Continue as Citizen"
                onClick={handleCitizenClick}
              />
              <Card
                icon={<span>🏛️</span>}
                title="Organization Portal"
                description="Staff and Organization Administrators access the workspace."
                actionLabel="Sign in"
                onClick={handleOfficerClick}
              />
              <Card
                icon={<span>⚙️</span>}
                title="Platform Administration"
                description="Super Admin access for platform-wide administration."
                actionLabel="Sign in"
                onClick={handleOfficerClick}
              />
            </section>
            <section className="landing-note">
              <p>
                Your choice here sets the journey only. Authentication and role-based permissions are enforced by the
                CivicFlow API and remain authoritative.
              </p>
            </section>
          </>
        ) : (
          <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
              <h2 style={{ margin: 0 }}>Sign In</h2>
              <button className="secondary-button" type="button" onClick={handleBack}>
                ← Back
              </button>
            </div>
            <LoginForm onSubmit={() => onLoginSubmit?.({ username: '', password: '' })} loading={loginLoading} error={loginError} />
          </section>
        )}
      </main>
      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
      </footer>
    </div>
  )
}

export default LandingPage
