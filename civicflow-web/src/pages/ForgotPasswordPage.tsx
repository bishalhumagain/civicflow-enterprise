import { useState, type FormEvent } from 'react'
import { forgotPassword, resendVerification } from '../auth'
import { PublicFooter, PublicHeader } from '../components/PublicLayout'
import { useToast } from '../components/use-toast'
import { normalizeEmail, validateEmail } from '../utils/validation'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [loading, setLoading] = useState(false)
  const [resending, setResending] = useState(false)
  const { showToast } = useToast()

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const validationError = validateEmail(email)

    if (validationError) {
      showToast(validationError, 'warning')
      return
    }

    const address = normalizeEmail(email)
    setLoading(true)

    try {
      await forgotPassword(address)
      showToast(
        `If ${address} is registered, a password recovery email has been sent. ` +
          'Check your inbox and spam folder. If nothing arrives, the address may not be registered.',
        'success',
      )
    } catch (reason) {
      showToast(
        reason instanceof Error
          ? reason.message
          : 'Password recovery failed. Please try again.',
        'error',
      )
    } finally {
      setLoading(false)
    }
  }

  async function handleResendVerification() {
    const validationError = validateEmail(email)

    if (validationError) {
      showToast(validationError, 'warning')
      return
    }

    const address = normalizeEmail(email)
    setResending(true)

    try {
      await resendVerification(address)
      showToast(
        `If ${address} needs verification, a verification email has been sent. ` +
          'Check your inbox and spam folder. If nothing arrives, the address may not be registered or may already be verified.',
        'success',
      )
    } catch (reason) {
      showToast(
        reason instanceof Error
          ? reason.message
          : 'Unable to resend the verification email. Please try again.',
        'error',
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
            <label>
              Email
              <input
                required
                type="email"
                maxLength={254}
                autoComplete="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                disabled={busy}
              />
            </label>
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
