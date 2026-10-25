import { AuthPage } from '../components/AuthPage'
import { LoginForm, type LoginCredentials } from '../components/LoginForm'

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
    <AuthPage
      description="Staff and Organization Administrators sign in here to manage their organization's cases and team."
      footer={
        onForgotPassword ? (
          <div className="auth-links">
            <button className="link-button" type="button" onClick={onForgotPassword}>
              Forgot your password?
            </button>
          </div>
        ) : undefined
      }
      title="Organization Sign In"
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

export default StaffLoginPage
