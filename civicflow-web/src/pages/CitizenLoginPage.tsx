import { LoginForm, type LoginCredentials } from '../components/LoginForm'
import { PublicFooter, PublicHeader } from '../components/PublicLayout'

export type CitizenLoginPageProps = {
  onSubmit: (credentials: LoginCredentials) => void
  onRegister: () => void
  onForgotPassword: () => void
  loading?: boolean
  error?: string | null
  errorCode?: string | null
}

export function CitizenLoginPage({
  onSubmit,
  onRegister,
  onForgotPassword,
  loading = false,
  error = null,
  errorCode = null,
}: CitizenLoginPageProps) {
  return (
    <div className="landing-page">
      <PublicHeader />
      <main className="landing-main">
        <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
          <h2 style={{ marginTop: 0 }}>Citizen Sign In</h2>
          <p className="muted" style={{ marginTop: 0 }}>
            Sign in to submit service requests, track progress, and manage your
            citizen profile.
          </p>
          <LoginForm
            onSubmit={onSubmit}
            loading={loading}
            error={error}
            errorCode={errorCode}
          />
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
      <PublicFooter />
    </div>
  )
}

export default CitizenLoginPage
