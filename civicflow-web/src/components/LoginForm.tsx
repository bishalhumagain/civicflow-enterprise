import { useRef, useState, type FormEvent } from "react"
import { MAX_EMAIL_LENGTH, normalizeEmail, validateEmail } from "../utils/validation"
import { useToast } from "./use-toast"

export type LoginCredentials = { email: string; password: string }

export type LoginFormProps = {
  onSubmit: (credentials: LoginCredentials) => void
  loading?: boolean
  error?: string | null
  errorCode?: string | null
}

export function LoginForm({
  onSubmit,
  loading = false,
  error = null,
}: LoginFormProps) {
  const [email, setEmail] = useState("")
  const [password, setPassword] = useState("")
  const [touched, setTouched] = useState({ email: false, password: false })
  const [submitted, setSubmitted] = useState(false)
  const emailRef = useRef<HTMLInputElement | null>(null)
  const passwordRef = useRef<HTMLInputElement | null>(null)
  const { showToast } = useToast()

  const emailValidation = validateEmail(email)
  const passwordValidation =
    password.length > 0 ? null : "Enter your password."
  const emailError =
    submitted || touched.email ? emailValidation : null
  const passwordError =
    submitted || touched.password ? passwordValidation : null

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)

    if (emailValidation !== null || passwordValidation !== null) {
      showToast("Check the highlighted fields and try again.", "warning")
      if (emailValidation !== null) {
        emailRef.current?.focus()
      } else {
        passwordRef.current?.focus()
      }
      return
    }

    onSubmit({ email: normalizeEmail(email), password })
  }

  return (
    <form className="case-card create-form" noValidate onSubmit={handleSubmit}>
      <h2>Sign In</h2>
      <p className="muted">
        Sign in with your CivicFlow account. Authentication is enforced by the
        CivicFlow API using a secure, HttpOnly session cookie.
      </p>
      {error !== null && (
        <div className="auth-error" role="alert">
          {error}
        </div>
      )}
      <label htmlFor="login-email">
        Email
        <input
          aria-describedby={emailError !== null ? "login-email-error" : undefined}
          aria-invalid={emailError !== null}
          autoCapitalize="none"
          autoComplete="email"
          disabled={loading}
          id="login-email"
          inputMode="email"
          maxLength={MAX_EMAIL_LENGTH}
          onBlur={() =>
            setTouched((current) => ({ ...current, email: true }))
          }
          onChange={(event) => setEmail(event.target.value)}
          placeholder="you@example.com"
          ref={emailRef}
          required
          spellCheck={false}
          type="email"
          value={email}
        />
      </label>
      {emailError !== null && (
        <span className="field-error" id="login-email-error" role="alert">
          {emailError}
        </span>
      )}
      <label htmlFor="login-password">
        Password
        <input
          aria-describedby={
            passwordError !== null ? "login-password-error" : undefined
          }
          aria-invalid={passwordError !== null}
          autoComplete="current-password"
          disabled={loading}
          id="login-password"
          onBlur={() =>
            setTouched((current) => ({ ...current, password: true }))
          }
          onChange={(event) => setPassword(event.target.value)}
          placeholder="••••••••••••"
          ref={passwordRef}
          required
          type="password"
          value={password}
        />
      </label>
      {passwordError !== null && (
        <span className="field-error" id="login-password-error" role="alert">
          {passwordError}
        </span>
      )}
      <div>
        <button className="primary-button" type="submit" disabled={loading}>
          {loading ? "Signing in…" : "Sign in"}
        </button>
      </div>
      <small className="muted">
        Sessions are validated server-side; no credentials or tokens are stored
        in the browser.
      </small>
    </form>
  )
}
