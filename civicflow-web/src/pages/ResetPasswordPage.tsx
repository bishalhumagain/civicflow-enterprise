import { useState, type FormEvent } from 'react'
import { resetPassword } from '../auth'

export type ResetPasswordPageProps = {
  onBack: () => void
  onSignIn: () => void
}

export function ResetPasswordPage({ onBack, onSignIn }: ResetPasswordPageProps) {
  const params = new URLSearchParams(window.location.search)
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''

  const [newPassword, setNewPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const linkIncomplete = !email || !token

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setMessage(null)
    setLoading(true)

    try {
      const result = await resetPassword(email, token, newPassword)
      setMessage(result.message)
      setNewPassword('')
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : 'Password reset failed.',
      )
    } finally {
      setLoading(false)
    }
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
        <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
            <h2 style={{ margin: 0 }}>Choose a new password</h2>
            <button className="secondary-button" type="button" onClick={onBack}>
              ← Back
            </button>
          </div>

          {linkIncomplete ? (
            <div className="auth-error" role="alert">
              This password reset link is incomplete or invalid. Request a new
              recovery link and try again.
            </div>
          ) : (
            <form className="case-card create-form" onSubmit={handleSubmit}>
              <label>
                New password
                <input
                  required
                  type="password"
                  minLength={12}
                  maxLength={128}
                  autoComplete="new-password"
                  value={newPassword}
                  onChange={(event) => setNewPassword(event.target.value)}
                  disabled={loading}
                />
              </label>
              <small className="muted">
                At least 12 characters with upper case, lower case, a number,
                and a symbol.
              </small>
              {error && (
                <div className="auth-error" role="alert">
                  {error}
                </div>
              )}
              {message && (
                <div className="auth-info" role="status">
                  {message}
                </div>
              )}
              <div>
                <button className="primary-button" type="submit" disabled={loading}>
                  {loading ? 'Saving…' : 'Reset password'}
                </button>
                <button className="secondary-button" type="button" onClick={onSignIn}>
                  Back to sign in
                </button>
              </div>
            </form>
          )}
        </section>
      </main>
      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
      </footer>
    </div>
  )
}
