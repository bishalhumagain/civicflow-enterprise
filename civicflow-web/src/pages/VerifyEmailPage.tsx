import { useEffect, useState } from 'react'
import { resendVerification, verifyEmail } from '../auth'

export type VerifyEmailPageProps = {
  onBack: () => void
  onSignIn: () => void
}

export function VerifyEmailPage({ onBack, onSignIn }: VerifyEmailPageProps) {
  const params = new URLSearchParams(window.location.search)
  const email = params.get('email') ?? ''
  const token = params.get('token')

  const [status, setStatus] = useState<'idle' | 'verifying' | 'verified' | 'error'>(
    token ? 'verifying' : 'idle',
  )
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!token) return

    let mounted = true

    void verifyEmail(email, token)
      .then((result) => {
        if (!mounted) return
        setStatus('verified')
        setMessage(result.message)
      })
      .catch((reason: unknown) => {
        if (!mounted) return
        setStatus('error')
        setMessage(
          reason instanceof Error ? reason.message : 'Verification failed.',
        )
      })

    return () => {
      mounted = false
    }
  }, [email, token])

  async function handleResend() {
    setMessage(null)
    try {
      const result = await resendVerification(email)
      setMessage(result.message)
    } catch (reason) {
      setMessage(
        reason instanceof Error ? reason.message : 'Unable to resend.',
      )
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
          <h2>Verify your email</h2>

          {status === 'verifying' && <p>Verifying your email address…</p>}

          {status === 'verified' && (
            <div className="auth-info" role="status">
              {message ?? 'Your email has been verified.'}
            </div>
          )}

          {status === 'error' && (
            <div className="auth-error" role="alert">
              {message ?? 'Verification failed.'}
            </div>
          )}

          {status === 'idle' && (
            <p className="muted">
              {email
                ? `Check your inbox at ${email} for the verification link.`
                : 'Open the verification link from your email to activate your account.'}
            </p>
          )}

          {message && status !== 'error' && status !== 'verified' && (
            <div className="auth-info" role="status">
              {message}
            </div>
          )}

          <div style={{ display: 'flex', gap: 8, marginTop: 16, flexWrap: 'wrap' }}>
            {email && (
              <button className="secondary-button" type="button" onClick={handleResend}>
                Resend verification email
              </button>
            )}
            <button className="primary-button" type="button" onClick={onSignIn}>
              Go to sign in
            </button>
            <button className="link-button" type="button" onClick={onBack}>
              Back to landing
            </button>
          </div>
        </section>
      </main>
      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
      </footer>
    </div>
  )
}
