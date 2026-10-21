import { useState, type FormEvent } from 'react'
import { forgotPassword } from '../auth'

export type ForgotPasswordPageProps = {
  onBack: () => void
  onSignIn: () => void
}

export function ForgotPasswordPage({ onBack, onSignIn }: ForgotPasswordPageProps) {
  const [email, setEmail] = useState('')
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setMessage(null)
    setLoading(true)

    try {
      const result = await forgotPassword(email.trim())
      setMessage(result.message)
    } catch (reason) {
      setError(
        reason instanceof Error ? reason.message : 'Password recovery failed.',
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
            <h2 style={{ margin: 0 }}>Reset your password</h2>
            <button className="secondary-button" type="button" onClick={onBack}>
              ← Back
            </button>
          </div>
          <form className="case-card create-form" onSubmit={handleSubmit}>
            <p className="muted">
              Enter your account email and we will send a password recovery link
              if the address is registered.
            </p>
            <label>
              Email
              <input
                required
                type="email"
                maxLength={254}
                autoComplete="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                disabled={loading}
              />
            </label>
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
                {loading ? 'Sending…' : 'Send recovery link'}
              </button>
              <button className="secondary-button" type="button" onClick={onSignIn}>
                Back to sign in
              </button>
            </div>
          </form>
        </section>
      </main>
      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
      </footer>
    </div>
  )
}
