import { LoginForm, type LoginCredentials } from '../components/LoginForm'
import { PublicFooter, PublicHeader } from '../components/PublicLayout'

export type StaffLoginPageProps = {
  onSubmit: (credentials: LoginCredentials) => void
  onForgotPassword?: () => void
  loading?: boolean
  error?: string | null
  errorCode?: string | null
}

export function StaffLoginPage({
  onSubmit,
  onForgotPassword,
  loading = false,
  error = null,
  errorCode = null,
}: StaffLoginPageProps) {
  return (
    <div className="landing-page">
      <PublicHeader />
      <main className="landing-main">
        <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
          <h2 style={{ marginTop: 0 }}>Organization Sign In</h2>
          <p className="muted" style={{ marginTop: 0 }}>
            Staff and Organization Administrators sign in here to manage their
            organization's cases and team.
          </p>
          <LoginForm
            onSubmit={onSubmit}
            loading={loading}
            error={error}
            errorCode={errorCode}
          />
          {onForgotPassword ? (
            <p className="muted" style={{ textAlign: 'center', marginTop: 12 }}>
              <button className="link-button" type="button" onClick={onForgotPassword}>
                Forgot your password?
              </button>
            </p>
          ) : null}
        </section>
      </main>
      <PublicFooter />
    </div>
  )
}

export default StaffLoginPage
