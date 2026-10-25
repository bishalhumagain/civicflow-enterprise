import { AuthPage } from '../components/AuthPage'
import { LoginForm, type LoginCredentials } from '../components/LoginForm'

export type PlatformAdminLoginPageProps = {
  onSubmit: (credentials: LoginCredentials) => void
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
  onForgotPassword,
  loading = false,
  error = null,
  errorCode = null,
}: PlatformAdminLoginPageProps) {
  return (
    <AuthPage
      description="This portal is reserved for platform administrators. Organization staff sign in through the organization portal, and citizens sign in through the citizen portal."
      footer={
        onForgotPassword ? (
          <div className="auth-links">
            <button className="link-button" type="button" onClick={onForgotPassword}>
              Forgot your password?
            </button>
          </div>
        ) : undefined
      }
      title="Platform Administrator Sign In"
    >
      <LoginForm
        onSubmit={onSubmit}
        loading={loading}
        error={error}
        errorCode={errorCode}
      />
    </AuthPage>
  )
}

export default PlatformAdminLoginPage
