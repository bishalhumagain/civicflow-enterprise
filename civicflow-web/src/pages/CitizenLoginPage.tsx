import { LoginForm, type LoginCredentials } from '../components/LoginForm'

export type CitizenLoginPageProps = {
  onSubmit: (credentials: LoginCredentials) => void
  onBack: () => void
  onRegister: () => void
  onForgotPassword: () => void
  loading?: boolean
  error?: string | null
}

export function CitizenLoginPage({
  onSubmit,
  onBack,
  onRegister,
  onForgotPassword,
  loading = false,
  error = null,
}: CitizenLoginPageProps) {
  return (
    <div className="landing-page">
      <header className="landing-header">
        <div className="landing-brand">
          <span className="landing-brand-mark">C</span>
          <span className="landing-brand-name">CivicFlow</span>
        </div>
      </header>
      <main className="landing-main">
        <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
            <h2 style={{ margin: 0 }}>Citizen Sign In</h2>
            <button className="secondary-button" type="button" onClick={onBack}>
              ← Back
            </button>
          </div>
          <p className="muted" style={{ marginTop: 0 }}>
            Sign in to submit service requests, track progress, and manage your
            citizen profile.
          </p>
          <LoginForm onSubmit={onSubmit} loading={loading} error={error} />
          <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 12, flexWrap: 'wrap' }}>
            <button className="link-button" type="button" onClick={onRegister}>
              Create an account
            </button>
            <button className="link-button" type="button" onClick={onForgotPassword}>
              Forgot your password?
            </button>
          </div>
        </section>
      </main>
      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
      </footer>
    </div>
  )
}

export default CitizenLoginPage
