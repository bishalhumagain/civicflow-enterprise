import { useState, type FormEvent } from 'react'
import { useToast } from './use-toast'

export type LoginCredentials = { email: string; password: string }

export type LoginFormProps = {
  onSubmit: (credentials: LoginCredentials) => void
  loading?: boolean
  error?: string | null
}

export function LoginForm({ onSubmit, loading = false, error = null }: LoginFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const { showToast } = useToast()

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!email.trim() || !password) {
      showToast('Email and password are required.', 'warning')
      return
    }
    onSubmit({ email: email.trim(), password })
  }

  return (
    <form className="case-card create-form" onSubmit={handleSubmit}>
      <h2>Sign In</h2>
      <p className="muted">
        Sign in with your CivicFlow account. Authentication is enforced by the
        CivicFlow API using a secure, HttpOnly session cookie.
      </p>
      {error && (
        <div className="auth-error" role="alert">
          {error}
        </div>
      )}
      <label>
        Email
        <input
          required
          type="email"
          autoComplete="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          disabled={loading}
          placeholder="you@example.com"
        />
      </label>
      <label>
        Password
        <input
          required
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          disabled={loading}
          placeholder="••••••••••••"
        />
      </label>
      <div>
        <button className="primary-button" type="submit" disabled={loading}>
          {loading ? 'Signing in…' : 'Sign in'}
        </button>
      </div>
      <small className="muted">
        Sessions are validated server-side; no credentials or tokens are stored
        in the browser.
      </small>
    </form>
  )
}
