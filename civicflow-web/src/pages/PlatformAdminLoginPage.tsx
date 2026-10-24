import { LoginForm, type LoginCredentials } from '../components/LoginForm'
import { Icon } from '../components/Icon'
import { PublicFooter, PublicHeader } from '../components/PublicLayout'

export type PlatformAdminLoginPageProps = {
  onSubmit: (credentials: LoginCredentials) => void
  onBack?: () => void
  onForgotPassword?: () => void
  loading?: boolean
  error?: string | null
  errorCode?: string | null
}

/**
 * Dedicated sign-in surface for platform (super) administrators. It is served
 * at its own URL and is intentionally not linked from the public landing
 * page. The API binds this portal to the super administrator role; staff and
 * citizen accounts are rejected here even with valid credentials.
 */
export function PlatformAdminLoginPage({
  onSubmit,
  onBack,
  onForgotPassword,
  loading = false,
  error = null,
  errorCode = null,
}: PlatformAdminLoginPageProps) {
  return (
    <div className="landing-page">
      <PublicHeader />
      <main className="landing-main">
        <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
            <h2 style={{ margin: 0 }}>Platform Administrator Sign In</h2>
            {onBack ? (
              <button className="secondary-button" type="button" onClick={onBack}>
                <Icon name="arrow-left" />
                Back
              </button>
            ) : null}
          </div>
          <p className="muted" style={{ marginTop: 0 }}>
            This portal is reserved for platform administrators. Organization
            staff sign in through the organization portal, and citizens sign in
            through the citizen portal.
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

export default PlatformAdminLoginPage
