import { useRef, useState, type FormEvent } from 'react'
import { forgotPassword, resendVerification } from '../auth'
import { PublicFooter, PublicHeader } from '../components/PublicLayout'
import {
  MAX_EMAIL_LENGTH,
  normalizeEmail,
  validateEmail,
} from '../utils/validation'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [emailError, setEmailError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [resending, setResending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const emailRef = useRef<HTMLInputElement | null>(null)

  function validateAddress(): string | null {
    const validationError = validateEmail(email)
    setEmailError(validationError)
    return validationError
  }

  function showFailure(reason: unknown, fallback: string) {
    setMessage(null)
    setError(reason instanceof Error ? reason.message : fallback)
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setMessage(null)

    if (validateAddress() !== null) {
      emailRef.current?.focus()
      return
    }

    const address = normalizeEmail(email)
    setLoading(true)

    try {
      await forgotPassword(address)
      setMessage(
        `If ${address} is registered, a password recovery email has been sent. ` +
          'Check your inbox and spam folder.',
      )
    } catch (reason) {
      showFailure(reason, 'Password recovery failed. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  async function handleResendVerification() {
    setError(null)
    setMessage(null)

    if (validateAddress() !== null) {
      emailRef.current?.focus()
      return
    }

    const address = normalizeEmail(email)
    setResending(true)

    try {
      await resendVerification(address)
      setMessage(
        `If ${address} needs verification, a verification email has been sent. ` +
          'Check your inbox and spam folder.',
      )
    } catch (reason) {
      showFailure(
        reason,
        'Unable to resend the verification email. Please try again.',
      )
    } finally {
      setResending(false)
    }
  }

  const busy = loading || resending

  return (
    <div className="landing-page">
      <PublicHeader />
      <main className="landing-main">
        <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
          <h2 style={{ marginTop: 0 }}>Reset your password</h2>
          <form
            className="case-card create-form"
            noValidate
            onSubmit={handleSubmit}
          >
            <p className="muted">
              Enter your account email and we will send a password recovery link
              if the address is registered. If you need to verify your email,
              request a verification link here too.
            </p>
            <label htmlFor="recovery-email">
              Email
              <input
                aria-describedby={
                  emailError !== null
                    ? 'recovery-email-error'
                    : 'recovery-email-hint'
                }
                aria-invalid={emailError !== null}
                autoComplete="email"
                disabled={busy}
                id="recovery-email"
                maxLength={MAX_EMAIL_LENGTH}
                onChange={(event) => {
                  setEmail(event.target.value)
                  setEmailError(null)
                  setError(null)
                  setMessage(null)
                }}
                ref={emailRef}
                required
                type="email"
                value={email}
              />
            </label>
            <small className="muted" id="recovery-email-hint">
              Use the address you registered with.
            </small>
            {emailError !== null && (
              <span
                className="field-error"
                id="recovery-email-error"
                role="alert"
              >
                {emailError}
              </span>
            )}

            {error !== null && (
              <div className="auth-error" role="alert">
                {error}
              </div>
            )}
            {message !== null && (
              <div className="success-message" role="status">
                {message}
              </div>
            )}

            <div className="recovery-actions">
              <button className="primary-button" type="submit" disabled={busy}>
                {loading ? 'Sending…' : 'Send recovery link'}
              </button>
              <button
                className="secondary-button"
                type="button"
                onClick={() => void handleResendVerification()}
                disabled={busy}
              >
                {resending ? 'Resending…' : 'Resend verification email'}
              </button>
            </div>
          </form>
        </section>
      </main>
      <PublicFooter />
    </div>
  )
}
