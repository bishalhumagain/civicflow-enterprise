import { useEffect, useRef, useState, type FormEvent } from "react"
import {
  AuthApiError,
  acceptInvitation,
  lookupInvitation,
  type InvitationDetails,
} from "../auth"
import { AuthPage } from "../components/AuthPage"
import { formatDateTime } from "../utils/format"
import {
  MAX_PASSWORD_LENGTH,
  MIN_PASSWORD_LENGTH,
  PASSWORD_RULES,
  validatePassword,
  validatePasswordConfirmation,
} from "../utils/validation"

export type SetupPasswordPageProps = {
  onSignIn: () => void
}

type PageState = "loading" | "ready" | "unavailable" | "accepted"

const unavailableMessage =
  "This invitation link is invalid, expired, or has already been used. Ask your administrator to send a new invitation."

/**
 * Password setup for an invited staff member. The invitee arrives from the
 * single-use link in their email, reviews which organization invited them,
 * and chooses their own password. The link cannot be reused once accepted.
 */
export function SetupPasswordPage({ onSignIn }: SetupPasswordPageProps) {
  const params = new URLSearchParams(window.location.search)
  const token = params.get("token")?.trim() ?? ""

  const [state, setState] = useState<PageState>(
    token.length >= 16 ? "loading" : "unavailable",
  )
  const [details, setDetails] = useState<InvitationDetails | null>(null)
  const [password, setPassword] = useState("")
  const [confirmPassword, setConfirmPassword] = useState("")
  const [touched, setTouched] = useState({
    password: false,
    confirmPassword: false,
  })
  const [submitted, setSubmitted] = useState(false)
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(
    token.length >= 16 ? null : unavailableMessage,
  )
  const passwordRef = useRef<HTMLInputElement | null>(null)
  const confirmRef = useRef<HTMLInputElement | null>(null)

  useEffect(() => {
    if (token.length < 16) return

    let mounted = true

    void lookupInvitation(token)
      .then((loaded) => {
        if (mounted === false) return
        setDetails(loaded)
        setState("ready")
      })
      .catch((reason: unknown) => {
        if (mounted === false) return
        setState("unavailable")
        setError(
          reason instanceof Error ? reason.message : unavailableMessage,
        )
      })

    return () => {
      mounted = false
    }
  }, [token])

  const passwordValidation = validatePassword(password)
  const confirmationValidation = validatePasswordConfirmation(
    password,
    confirmPassword,
  )
  const passwordError =
    submitted || touched.password ? passwordValidation : null
  const confirmationError =
    submitted || touched.confirmPassword ? confirmationValidation : null

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    setError(null)
    setMessage(null)

    const currentPasswordError = validatePassword(password)
    const currentConfirmationError = validatePasswordConfirmation(
      password,
      confirmPassword,
    )

    if (currentPasswordError !== null || currentConfirmationError !== null) {
      setError("Fix the highlighted fields before creating your account.")
      if (currentPasswordError !== null) {
        passwordRef.current?.focus()
      } else {
        confirmRef.current?.focus()
      }
      return
    }

    setLoading(true)

    try {
      const result = await acceptInvitation(token, password)
      setMessage(result.message)
      setState("accepted")
      setPassword("")
      setConfirmPassword("")
    } catch (reason) {
      if (reason instanceof AuthApiError && reason.status === 410) {
        setState("unavailable")
      }

      setError(
        reason instanceof Error
          ? reason.message
          : "The invitation could not be accepted.",
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <AuthPage
      description="Choose a password to activate your staff account."
      title="Set up your password"
    >
      {state === "loading" && (
        <p aria-live="polite" className="muted empty-state">
          Checking your invitation…
        </p>
      )}

      {state === "unavailable" && (
        <div className="case-card auth-card">
          <div className="auth-error" role="alert">
            {error ?? unavailableMessage}
          </div>
          <p className="muted">
            Invitation links expire after a few days and can be used only
            once. Contact the administrator who invited you to request a
            new link.
          </p>
          <div className="auth-actions">
            <button className="secondary-button" type="button" onClick={onSignIn}>
              Back to sign in
            </button>
          </div>
        </div>
      )}

      {state === "accepted" && (
        <div className="case-card auth-card">
          <div className="auth-info" role="status">
            {message ??
              "Your account is ready. Sign in with the password you just chose."}
          </div>
          <div className="auth-actions">
            <button
              className="primary-button"
              type="button"
              onClick={onSignIn}
            >
              Continue to sign in
            </button>
          </div>
        </div>
      )}

      {state === "ready" && details !== null && (
        <>
          <div className="case-card auth-card auth-summary">
            <p className="eyebrow">YOU WERE INVITED TO JOIN</p>
            <h2>{details.organizationName}</h2>
            <p className="muted">
              {details.fullName} · {details.email}
            </p>
            <p className="muted">
              Role: {details.role} · Link expires{" "}
              {formatDateTime(details.expiresAt)}
            </p>
          </div>

          <form className="case-card auth-card" noValidate onSubmit={handleSubmit}>
            <label htmlFor="setup-password">
              Password
              <input
                aria-describedby={
                  passwordError !== null
                    ? "setup-password-error setup-password-hint"
                    : "setup-password-hint"
                }
                aria-invalid={passwordError !== null}
                autoComplete="new-password"
                disabled={loading}
                id="setup-password"
                maxLength={MAX_PASSWORD_LENGTH}
                minLength={MIN_PASSWORD_LENGTH}
                onBlur={() =>
                  setTouched((current) => ({
                    ...current,
                    password: true,
                  }))
                }
                onChange={(event) => {
                  setPassword(event.target.value)
                  setError(null)
                }}
                ref={passwordRef}
                required
                type="password"
                value={password}
              />
            </label>
            {passwordError !== null && (
              <span
                className="field-error"
                id="setup-password-error"
                role="alert"
              >
                {passwordError}
              </span>
            )}

            <label htmlFor="setup-confirm-password">
              Confirm password
              <input
                aria-describedby={
                  confirmationError !== null
                    ? "setup-confirm-password-error"
                    : undefined
                }
                aria-invalid={confirmationError !== null}
                autoComplete="new-password"
                disabled={loading}
                id="setup-confirm-password"
                maxLength={MAX_PASSWORD_LENGTH}
                minLength={MIN_PASSWORD_LENGTH}
                onBlur={() =>
                  setTouched((current) => ({
                    ...current,
                    confirmPassword: true,
                  }))
                }
                onChange={(event) => {
                  setConfirmPassword(event.target.value)
                  setError(null)
                }}
                ref={confirmRef}
                required
                type="password"
                value={confirmPassword}
              />
            </label>
            {confirmationError !== null && (
              <span
                className="field-error"
                id="setup-confirm-password-error"
                role="alert"
              >
                {confirmationError}
              </span>
            )}
            <small className="muted" id="setup-password-hint">
              {PASSWORD_RULES} No password is ever emailed to you.
            </small>
            {error !== null && (
              <div className="auth-error" role="alert">
                {error}
              </div>
            )}
            <div className="auth-actions">
              <button
                className="primary-button"
                type="submit"
                disabled={loading}
              >
                {loading ? "Creating account…" : "Create account"}
              </button>
            </div>
          </form>
        </>
      )}
    </AuthPage>
  )
}

export default SetupPasswordPage
