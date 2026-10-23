import { useEffect, useState, type FormEvent } from 'react'
import {
  AuthApiError,
  acceptInvitation,
  lookupInvitation,
  type InvitationDetails,
} from '../auth'
import { Icon } from '../components/Icon'

export type SetupPasswordPageProps = {
  onSignIn: () => void
  onBack: () => void
}

type PageState = 'loading' | 'ready' | 'unavailable' | 'accepted'

const unavailableMessage =
  'This invitation link is invalid, expired, or has already been used. Ask your administrator to send a new invitation.'

/**
 * Password setup for an invited staff member. The invitee arrives from the
 * single-use link in their email, reviews which organization invited them,
 * and chooses their own password. The link cannot be reused once accepted.
 */
export function SetupPasswordPage({ onSignIn, onBack }: SetupPasswordPageProps) {
  const params = new URLSearchParams(window.location.search)
  const token = params.get('token')?.trim() ?? ''

  const [state, setState] = useState<PageState>(
    token.length >= 16 ? 'loading' : 'unavailable',
  )
  const [details, setDetails] = useState<InvitationDetails | null>(null)
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(
    token.length >= 16 ? null : unavailableMessage,
  )

  useEffect(() => {
    if (token.length < 16) return

    let mounted = true

    void lookupInvitation(token)
      .then((loaded) => {
        if (!mounted) return
        setDetails(loaded)
        setState('ready')
      })
      .catch((reason: unknown) => {
        if (!mounted) return
        setState('unavailable')
        setError(
          reason instanceof Error
            ? reason.message
            : unavailableMessage,
        )
      })

    return () => {
      mounted = false
    }
  }, [token])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (password !== confirmPassword) {
      setError('The two passwords do not match.')
      return
    }

    setLoading(true)
    setError(null)
    setMessage(null)

    try {
      const result = await acceptInvitation(token, password)
      setMessage(result.message)
      setState('accepted')
      setPassword('')
      setConfirmPassword('')
    } catch (reason) {
      if (reason instanceof AuthApiError && reason.status === 410) {
        setState('unavailable')
      }

      setError(
        reason instanceof Error
          ? reason.message
          : 'The invitation could not be accepted.',
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
            <h2 style={{ margin: 0 }}>Set up your password</h2>
            <button className="secondary-button" type="button" onClick={onBack}>
              <Icon name="arrow-left" />
              Back
            </button>
          </div>

          {state === 'loading' && (
            <p className="muted">Checking your invitation…</p>
          )}

          {state === 'unavailable' && (
            <>
              <div className="auth-error" role="alert">
                {error ?? unavailableMessage}
              </div>
              <p className="muted">
                Invitation links expire after a few days and can be used only
                once. Contact the administrator who invited you to request a
                new link.
              </p>
            </>
          )}

          {state === 'accepted' && (
            <>
              <div className="auth-info" role="status">
                {message ??
                  'Your account is ready. Sign in with the password you just chose.'}
              </div>
              <div style={{ marginTop: 16 }}>
                <button className="primary-button" type="button" onClick={onSignIn}>
                  Continue to sign in
                </button>
              </div>
            </>
          )}

          {state === 'ready' && details && (
            <>
              <div className="case-card" style={{ marginBottom: 16 }}>
                <p className="eyebrow" style={{ marginTop: 0 }}>
                  YOU WERE INVITED TO JOIN
                </p>
                <h3 style={{ margin: '4px 0' }}>{details.organizationName}</h3>
                <p className="muted" style={{ margin: '4px 0' }}>
                  {details.fullName} · {details.email}
                </p>
                <p className="muted" style={{ margin: '4px 0' }}>
                  Role: {details.role} · Link expires{' '}
                  {new Date(details.expiresAt).toLocaleString()}
                </p>
              </div>

              <form className="case-card create-form" onSubmit={handleSubmit}>
                <label>
                  Password
                  <input
                    required
                    type="password"
                    minLength={12}
                    maxLength={128}
                    autoComplete="new-password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    disabled={loading}
                  />
                </label>
                <label>
                  Confirm password
                  <input
                    required
                    type="password"
                    minLength={12}
                    maxLength={128}
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(event) => setConfirmPassword(event.target.value)}
                    disabled={loading}
                  />
                </label>
                <small className="muted">
                  At least 12 characters with upper case, lower case, a number,
                  and a symbol. No password is ever emailed to you.
                </small>
                {error && (
                  <div className="auth-error" role="alert">
                    {error}
                  </div>
                )}
                <div>
                  <button className="primary-button" type="submit" disabled={loading}>
                    {loading ? 'Creating account…' : 'Create account'}
                  </button>
                </div>
              </form>
            </>
          )}
        </section>
      </main>
      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
      </footer>
    </div>
  )
}

export default SetupPasswordPage
