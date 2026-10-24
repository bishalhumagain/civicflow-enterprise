import { useState, type FormEvent } from 'react'
import { forgotPassword, resendVerification } from '../auth'
import { Icon } from '../components/Icon'
import { PublicFooter, PublicHeader } from '../components/PublicLayout'

export type ForgotPasswordPageProps = {
  onBack: () => void
  onSignIn: () => void
}

export function ForgotPasswordPage({ onBack, onSignIn }: ForgotPasswordPageProps) {
  const [email, setEmail] = useState('')
  const [loading, setLoading] = useState(false)
  const [resending, setResending] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [verificationMessage, setVerificationMessage] = useState<string | null>(null)
  const [verificationError, setVerificationError] = useState<string | null>(null)

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

  async function handleResendVerification() {
    if (!email.trim()) {
      setVerificationError('Enter your account email first.')
      setVerificationMessage(null)
      return
    }

    setVerificationError(null)
    setVerificationMessage(null)
    setResending(true)

    try {
      const result = await resendVerification(email.trim())
      setVerificationMessage(result.message)
    } catch (reason) {
      setVerificationError(
        reason instanceof Error
          ? reason.message
          : 'Unable to resend the verification email.',
      )
    } finally {
      setResending(false)
    }
  }

  return (
    <div className="landing-page">
      <PublicHeader />
      <main className="landing-main">
        <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
            <h2 style={{ margin: 0 }}>Reset your password</h2>
            <button className="secondary-button" type="button" onClick={onBack}>
              <Icon name="arrow-left" />
              Back
            </button>
          </div>
          <form className="case-card create-form" onSubmit={handleSubmit}>
            <p className="muted">
              Enter your account email and we will send a password recovery link
              if the address is registered. If you need to verify your email,
              request a verification link here too.
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
            <div className="recovery-actions">
              <button className="primary-button" type="submit" disabled={loading}>
                {loading ? 'Sending…' : 'Send recovery link'}
              </button>
              <button
                className="secondary-button"
                type="button"
                onClick={() => void handleResendVerification()}
                disabled={resending || loading}
              >
                {resending ? 'Resending…' : 'Resend verification email'}
              </button>
            </div>
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
            {verificationError && (
              <div className="auth-error" role="alert">
                {verificationError}
              </div>
            )}
            {verificationMessage && (
              <div className="auth-info" role="status">
                {verificationMessage}
              </div>
            )}
            <div className="recovery-footer">
              <button className="secondary-button" type="button" onClick={onSignIn}>
                <Icon name="arrow-left" />
                Back to sign in
              </button>
            </div>
          </form>
        </section>
      </main>
      <PublicFooter />
    </div>
  )
}
