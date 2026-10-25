import { useEffect, useState } from 'react'
import { resendVerification, verifyEmail } from '../auth'
import { AuthPage } from '../components/AuthPage'

export type VerifyEmailPageProps = {
  onSignIn: () => void
}

export function VerifyEmailPage({ onSignIn }: VerifyEmailPageProps) {
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
    <AuthPage
      description="Confirm your email address to finish activating your account."
      title="Verify your email"
    >
      <div className="case-card auth-card">
        {status === 'verifying' && (
          <p aria-live="polite" className="muted">
            Verifying your email address…
          </p>
        )}

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

        <div className="auth-actions">
          {email && (
            <button className="secondary-button" type="button" onClick={handleResend}>
              Resend verification email
            </button>
          )}
          <button className="primary-button" type="button" onClick={onSignIn}>
            Go to sign in
          </button>
        </div>
      </div>
    </AuthPage>
  )
}
