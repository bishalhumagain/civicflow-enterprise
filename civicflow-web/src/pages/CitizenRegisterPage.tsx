import { useRef, useState, type FormEvent, type RefObject } from "react"
import { register } from "../auth"
import { AuthPage } from "../components/AuthPage"
import {
  MAX_EMAIL_LENGTH,
  MAX_IDENTIFIER_LENGTH,
  MAX_NAME_LENGTH,
  MAX_PASSWORD_LENGTH,
  MAX_PHONE_LENGTH,
  MIN_PASSWORD_LENGTH,
  PASSWORD_RULES,
  normalizeEmail,
  normalizeIdentifier,
  normalizeWhitespace,
  sanitizePhoneInput,
  validateEmail,
  validateIdentifier,
  validateName,
  validatePassword,
  validatePhone,
} from "../utils/validation"

export type CitizenAuthPageProps = {
  onSignIn: () => void
  onVerifiedPending: (email: string) => void
}

type FieldName =
  | "fullName"
  | "email"
  | "phoneNumber"
  | "nidNumber"
  | "citizenshipNumber"
  | "password"

type RegistrationForm = {
  fullName: string
  email: string
  phoneNumber: string
  nidNumber: string
  citizenshipNumber: string
  password: string
}

type FieldErrors = Partial<Record<FieldName, string>>

const fieldOrder: FieldName[] = [
  "fullName",
  "email",
  "phoneNumber",
  "nidNumber",
  "citizenshipNumber",
  "password",
]

function validateForm(form: RegistrationForm): FieldErrors {
  const errors: FieldErrors = {}

  const fullName = validateName(form.fullName)
  if (fullName !== null) errors.fullName = fullName

  const email = validateEmail(form.email)
  if (email !== null) errors.email = email

  const phone = validatePhone(form.phoneNumber, true)
  if (phone !== null) errors.phoneNumber = phone

  const nid = validateIdentifier(form.nidNumber, "National ID number")
  if (nid !== null) {
    errors.nidNumber = nid
  } else if (form.nidNumber.trim().length === 0) {
    errors.nidNumber = "National ID number is required."
  }

  const citizenship = validateIdentifier(
    form.citizenshipNumber,
    "Citizenship number",
  )
  if (citizenship !== null) {
    errors.citizenshipNumber = citizenship
  } else if (form.citizenshipNumber.trim().length === 0) {
    errors.citizenshipNumber = "Citizenship number is required."
  }

  const password = validatePassword(form.password)
  if (password !== null) errors.password = password

  return errors
}

export function CitizenRegisterPage({
  onSignIn,
  onVerifiedPending,
}: CitizenAuthPageProps) {
  const [form, setForm] = useState<RegistrationForm>({
    fullName: "",
    email: "",
    phoneNumber: "",
    nidNumber: "",
    citizenshipNumber: "",
    password: "",
  })
  const [touched, setTouched] = useState<Partial<Record<FieldName, boolean>>>({})
  const [submitted, setSubmitted] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [info, setInfo] = useState<string | null>(null)

  const fullNameRef = useRef<HTMLInputElement | null>(null)
  const emailRef = useRef<HTMLInputElement | null>(null)
  const phoneRef = useRef<HTMLInputElement | null>(null)
  const nidRef = useRef<HTMLInputElement | null>(null)
  const citizenshipRef = useRef<HTMLInputElement | null>(null)
  const passwordRef = useRef<HTMLInputElement | null>(null)

  const fieldRefs: Record<FieldName, RefObject<HTMLInputElement | null>> = {
    fullName: fullNameRef,
    email: emailRef,
    phoneNumber: phoneRef,
    nidNumber: nidRef,
    citizenshipNumber: citizenshipRef,
    password: passwordRef,
  }

  const errors = validateForm(form)

  function update(field: FieldName, value: string) {
    setForm((current) => ({ ...current, [field]: value }))
    setError(null)
    setInfo(null)
  }

  function markTouched(field: FieldName) {
    setTouched((current) => ({ ...current, [field]: true }))
  }

  function errorFor(field: FieldName): string | null {
    if (submitted === false && touched[field] !== true) return null
    return errors[field] ?? null
  }

  function focusFirstInvalid(currentErrors: FieldErrors) {
    const firstInvalid = fieldOrder.find(
      (field) => currentErrors[field] !== undefined,
    )
    if (firstInvalid !== undefined) {
      fieldRefs[firstInvalid].current?.focus()
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    setInfo(null)
    setSubmitted(true)

    const currentErrors = validateForm(form)

    if (Object.keys(currentErrors).length > 0) {
      setError("Fix the highlighted fields before creating your account.")
      focusFirstInvalid(currentErrors)
      return
    }

    const payload = {
      fullName: normalizeWhitespace(form.fullName),
      email: normalizeEmail(form.email),
      phoneNumber: form.phoneNumber.trim(),
      nidNumber: normalizeIdentifier(form.nidNumber),
      citizenshipNumber: normalizeIdentifier(form.citizenshipNumber),
      password: form.password,
    }

    setLoading(true)

    try {
      const result = await register(payload)
      setInfo(result.message)
      onVerifiedPending(payload.email)
    } catch (reason) {
      const message =
        reason instanceof Error ? reason.message : "Registration failed."
      setError(message)
    } finally {
      setLoading(false)
    }
  }

  const fullNameError = errorFor("fullName")
  const emailError = errorFor("email")
  const phoneError = errorFor("phoneNumber")
  const nidError = errorFor("nidNumber")
  const citizenshipError = errorFor("citizenshipNumber")
  const passwordError = errorFor("password")

  return (
    <AuthPage
      description="Register to submit public service requests and follow their progress."
      title="Create a Citizen Account"
    >
      <form className="case-card auth-card" noValidate onSubmit={handleSubmit}>
        <label htmlFor="register-full-name">
          Full name
          <input
            aria-describedby={
              fullNameError ? "register-full-name-error" : undefined
            }
            aria-invalid={fullNameError !== null}
            autoComplete="name"
            disabled={loading}
            id="register-full-name"
            maxLength={MAX_NAME_LENGTH}
            onBlur={() => markTouched("fullName")}
            onChange={(event) => update("fullName", event.target.value)}
            ref={fullNameRef}
            required
            value={form.fullName}
          />
        </label>
        {fullNameError !== null && (
          <span
            className="field-error"
            id="register-full-name-error"
            role="alert"
          >
            {fullNameError}
          </span>
        )}

        <label htmlFor="register-email">
          Email
          <input
            aria-describedby={
              emailError ? "register-email-error" : undefined
            }
            aria-invalid={emailError !== null}
            autoCapitalize="none"
            autoComplete="email"
            disabled={loading}
            id="register-email"
            inputMode="email"
            maxLength={MAX_EMAIL_LENGTH}
            onBlur={() => markTouched("email")}
            onChange={(event) => update("email", event.target.value)}
            ref={emailRef}
            required
            spellCheck={false}
            type="email"
            value={form.email}
          />
        </label>
        {emailError !== null && (
          <span className="field-error" id="register-email-error" role="alert">
            {emailError}
          </span>
        )}

        <label htmlFor="register-phone">
          Phone number
          <input
            aria-describedby={
              phoneError ? "register-phone-error" : undefined
            }
            aria-invalid={phoneError !== null}
            autoComplete="tel"
            disabled={loading}
            id="register-phone"
            inputMode="tel"
            maxLength={MAX_PHONE_LENGTH}
            onBlur={() => markTouched("phoneNumber")}
            onChange={(event) =>
              update("phoneNumber", sanitizePhoneInput(event.target.value))
            }
            ref={phoneRef}
            required
            type="tel"
            value={form.phoneNumber}
          />
        </label>
        {phoneError !== null && (
          <span className="field-error" id="register-phone-error" role="alert">
            {phoneError}
          </span>
        )}

        <label htmlFor="register-citizenship">
          Citizenship number
          <input
            aria-describedby={
              citizenshipError
                ? "register-citizenship-error"
                : undefined
            }
            aria-invalid={citizenshipError !== null}
            autoCapitalize="characters"
            disabled={loading}
            id="register-citizenship"
            maxLength={MAX_IDENTIFIER_LENGTH}
            onBlur={() => markTouched("citizenshipNumber")}
            onChange={(event) =>
              update(
                "citizenshipNumber",
                event.target.value.toUpperCase(),
              )
            }
            ref={citizenshipRef}
            required
            spellCheck={false}
            value={form.citizenshipNumber}
          />
        </label>
        {citizenshipError !== null && (
          <span
            className="field-error"
            id="register-citizenship-error"
            role="alert"
          >
            {citizenshipError}
          </span>
        )}

        <label htmlFor="register-nid">
          NID number
          <input
            aria-describedby={nidError ? "register-nid-error" : undefined}
            aria-invalid={nidError !== null}
            autoCapitalize="characters"
            disabled={loading}
            id="register-nid"
            maxLength={MAX_IDENTIFIER_LENGTH}
            onBlur={() => markTouched("nidNumber")}
            onChange={(event) =>
              update("nidNumber", event.target.value.toUpperCase())
            }
            ref={nidRef}
            required
            spellCheck={false}
            value={form.nidNumber}
          />
        </label>
        {nidError !== null && (
          <span className="field-error" id="register-nid-error" role="alert">
            {nidError}
          </span>
        )}

        <label htmlFor="register-password">
          Password
          <input
            aria-describedby={
              passwordError
                ? "register-password-error register-password-hint"
                : "register-password-hint"
            }
            aria-invalid={passwordError !== null}
            autoComplete="new-password"
            disabled={loading}
            id="register-password"
            maxLength={MAX_PASSWORD_LENGTH}
            minLength={MIN_PASSWORD_LENGTH}
            onBlur={() => markTouched("password")}
            onChange={(event) => update("password", event.target.value)}
            ref={passwordRef}
            required
            type="password"
            value={form.password}
          />
        </label>
        {passwordError !== null && (
          <span
            className="field-error"
            id="register-password-error"
            role="alert"
          >
            {passwordError}
          </span>
        )}
        <small className="muted" id="register-password-hint">
          {PASSWORD_RULES}
        </small>

        {error !== null && (
          <div className="auth-error" role="alert">
            {error}
          </div>
        )}
        {info !== null && (
          <div className="auth-info" role="status">
            {info}
          </div>
        )}
        <div className="auth-actions">
          <button className="primary-button" type="submit" disabled={loading}>
            {loading ? "Creating account…" : "Register"}
          </button>
          <button
            className="secondary-button"
            type="button"
            onClick={onSignIn}
          >
            I already have an account
          </button>
        </div>
      </form>
    </AuthPage>
  )
}
