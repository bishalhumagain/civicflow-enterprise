import { AuthPage } from '../components/AuthPage'
import { LoginForm, type LoginCredentials } from '../components/LoginForm'

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
    <AuthPage
      description="Sign in to submit service requests, track progress, and manage your citizen profile."
      footer={
        <div className="auth-links">
          <button className="link-button" type="button" onClick={onRegister}>
            Create an account
          </button>
          <button className="link-button" type="button" onClick={onForgotPassword}>
            Forgot your password?
          </button>
        </div>
      }
      title="Citizen Sign In"
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

export default CitizenLoginPage
