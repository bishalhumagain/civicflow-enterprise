import { useState, type FormEvent } from 'react'
import { register } from '../auth'

export type CitizenAuthPageProps = {
  onBack: () => void
  onSignIn: () => void
  onVerifiedPending: (email: string) => void
}

export function CitizenRegisterPage({
  onBack,
  onSignIn,
  onVerifiedPending,
}: CitizenAuthPageProps) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [nidNumber, setNidNumber] = useState('')
  const [citizenshipNumber, setCitizenshipNumber] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [info, setInfo] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setInfo(null)
    setLoading(true)

    try {
      const result = await register({
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim(),
        nidNumber: nidNumber.trim(),
        citizenshipNumber: citizenshipNumber.trim(),
        password,
      })
      setInfo(result.message)
      onVerifiedPending(email.trim())
    } catch (reason) {
      const message =
        reason instanceof Error ? reason.message : 'Registration failed.'
      setError(message)
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
            <h2 style={{ margin: 0 }}>Create a Citizen Account</h2>
            <button className="secondary-button" type="button" onClick={onBack}>
              ← Back
            </button>
          </div>
          <form className="case-card create-form" onSubmit={handleSubmit}>
            <label>
              Full name
              <input
                required
                maxLength={150}
                autoComplete="name"
                value={fullName}
                onChange={(event) => setFullName(event.target.value)}
                disabled={loading}
              />
            </label>
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
            <label>
              Phone number
              <input
                required
                type="tel"
                maxLength={32}
                autoComplete="tel"
                value={phoneNumber}
                onChange={(event) => setPhoneNumber(event.target.value)}
                disabled={loading}
              />
            </label>
            <label>
              Citizenship number
              <input
                required
                maxLength={64}
                value={citizenshipNumber}
                onChange={(event) => setCitizenshipNumber(event.target.value)}
                disabled={loading}
              />
            </label>
            <label>
              NID number
              <input
                required
                maxLength={64}
                value={nidNumber}
                onChange={(event) => setNidNumber(event.target.value)}
                disabled={loading}
              />
            </label>
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
            <small className="muted">
              Passwords must be at least 12 characters and include upper case,
              lower case, a number, and a symbol.
            </small>
            {error && (
              <div className="auth-error" role="alert">
                {error}
              </div>
            )}
            {info && (
              <div className="auth-info" role="status">
                {info}
              </div>
            )}
            <div>
              <button className="primary-button" type="submit" disabled={loading}>
                {loading ? 'Creating account…' : 'Register'}
              </button>
              <button className="secondary-button" type="button" onClick={onSignIn}>
                I already have an account
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
