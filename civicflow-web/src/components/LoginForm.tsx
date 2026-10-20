import { useState, type FormEvent } from 'react'
import { useToast } from './use-toast'

export type LoginFormProps = {
  onSubmit: (credentials: { username: string; password: string }) => void
  loading?: boolean
  error?: string | null
}

export function LoginForm({ onSubmit, loading = false, error = null }: LoginFormProps) {
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const { showToast } = useToast()

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!username.trim() || !password.trim()) {
      showToast('Username and password are required.', 'warning')
      return
    }
    onSubmit({ username: username.trim(), password: password.trim() })
  }

  return (
    <form className="case-card create-form" onSubmit={handleSubmit}>
        <h2>Sign In</h2>
      <p className="muted">You will be redirected to the secure identity provider to authenticate.</p>
      {error && (
        <div className="auth-error" role="alert">
          {error}
        </div>
      )}
      <label>
        Username or Email
        <input
          required
          autoComplete="username"
          value={username}
          onChange={(event) => setUsername(event.target.value)}
          disabled={loading}
          placeholder="your username or email"
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
          placeholder="••••••••"
        />
      </label>
      <div>
        <button className="primary-button" type="submit" disabled={loading}>
          {loading ? 'Redirecting…' : 'Continue to Sign In'}
        </button>
      </div>
      <small className="muted">
        Authentication and authorization remain server-authoritative via Keycloak OIDC.
      </small>
    </form>
  )
}
