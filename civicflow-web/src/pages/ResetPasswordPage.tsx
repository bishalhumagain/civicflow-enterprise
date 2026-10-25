import { useRef, useState, type FormEvent } from "react"
import { resetPassword } from "../auth"
import { AuthPage } from "../components/AuthPage"
import {
  MAX_PASSWORD_LENGTH,
  MIN_PASSWORD_LENGTH,
  PASSWORD_RULES,
  validatePassword,
} from "../utils/validation"

export type ResetPasswordPageProps = {
  onSignIn: () => void
}

export function ResetPasswordPage({ onSignIn }: ResetPasswordPageProps) {
  const params = new URLSearchParams(window.location.search)
  const email = params.get("email")?.trim() ?? ""
  const token = params.get("token")?.trim() ?? ""

  const [newPassword, setNewPassword] = useState("")
  const [touched, setTouched] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const passwordRef = useRef<HTMLInputElement | null>(null)

  const linkIncomplete = email.length === 0 || token.length === 0
  const passwordError = validatePassword(newPassword)
  const visiblePasswordError =
    submitted || touched ? passwordError : null

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setMessage(null)
    setSubmitted(true)

    const currentError = validatePassword(newPassword)
    if (currentError !== null) {
      setError("Fix the highlighted field before resetting your password.")
      passwordRef.current?.focus()
      return
    }

    setLoading(true)

    try {
      const result = await resetPassword(email, token, newPassword)
      setMessage(result.message)
      setNewPassword("")
      setTouched(false)
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : "Password reset failed.",
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthPage
      description="Choose a strong password for your account."
      title="Choose a new password"
    >
      {linkIncomplete ? (
        <div className="case-card auth-card">
          <div className="auth-error" role="alert">
            This password reset link is incomplete or invalid. Request a new
            recovery link and try again.
          </div>
          <div className="auth-actions">
            <button className="secondary-button" type="button" onClick={onSignIn}>
              Back to sign in
            </button>
          </div>
        </div>
      ) : (
        <form className="case-card auth-card" noValidate onSubmit={handleSubmit}>
          <label htmlFor="reset-new-password">
            New password
            <input
              aria-describedby={
                visiblePasswordError !== null
                  ? "reset-password-error reset-password-hint"
                  : "reset-password-hint"
              }
              aria-invalid={visiblePasswordError !== null}
              autoComplete="new-password"
              disabled={loading}
              id="reset-new-password"
              maxLength={MAX_PASSWORD_LENGTH}
              minLength={MIN_PASSWORD_LENGTH}
              onBlur={() => setTouched(true)}
              onChange={(event) => {
                setNewPassword(event.target.value)
                setError(null)
                setMessage(null)
              }}
              ref={passwordRef}
              required
              type="password"
              value={newPassword}
            />
          </label>
          {visiblePasswordError !== null && (
            <span
              className="field-error"
              id="reset-password-error"
              role="alert"
            >
              {visiblePasswordError}
            </span>
          )}
          <small className="muted" id="reset-password-hint">
            {PASSWORD_RULES}
          </small>
          {error !== null && (
            <div className="auth-error" role="alert">
              {error}
            </div>
          )}
          {message !== null && (
            <div className="auth-info" role="status">
              {message}
            </div>
          )}
          <div className="auth-actions">
            <button
              className="primary-button"
              type="submit"
              disabled={loading}
            >
              {loading ? "Saving…" : "Reset password"}
            </button>
            <button
              className="secondary-button"
              type="button"
              onClick={onSignIn}
            >
              Back to sign in
            </button>
          </div>
        </form>
      )}
    </AuthPage>
  )
}
