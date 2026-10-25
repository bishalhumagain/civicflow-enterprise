import { useState, type FormEvent } from 'react'
import type { AccountInvitation } from '../domain'
import {
  canResend,
  canRevoke,
  invitationRoles,
  invitationStatus,
} from './invitation-utils'
import { formatDateTime } from '../utils/format'
import {
  MAX_EMAIL_LENGTH,
  MAX_NAME_LENGTH,
  MAX_PHONE_LENGTH,
  normalizeEmail,
  normalizeOptional,
  normalizeWhitespace,
  sanitizePhoneInput,
  validateEmail,
  validateName,
  validatePhone,
} from '../utils/validation'
import { Icon } from './Icon'

type InvitationField = "fullName" | "email" | "phoneNumber"

type InvitationErrors = Partial<Record<InvitationField, string>>

function validateInvitation(
  fullName: string,
  email: string,
  phoneNumber: string,
): InvitationErrors {
  const errors: InvitationErrors = {}

  const nameError = validateName(fullName)
  if (nameError !== null) errors.fullName = nameError

  const emailError = validateEmail(email)
  if (emailError !== null) errors.email = emailError

  const phoneError = validatePhone(phoneNumber)
  if (phoneError !== null) errors.phoneNumber = phoneError

  return errors
}

/**
 * Invitation form and pending-invitation list shared by the staff directory
 * and the platform organization administration screen. Invitations never
 * contain a password: the invitee chooses one from the emailed single-use
 * link.
 */
export function InvitationManager({
  invitations,
  loading,
  disabled = false,
  onInvite,
  onResend,
  onRevoke,
  roleOptions = invitationRoles,
}: {
  invitations: AccountInvitation[]
  loading: boolean
  disabled?: boolean
  onInvite: (
    fullName: string,
    email: string,
    phoneNumber: string,
    role: string,
  ) => Promise<boolean>
  onResend: (invitationId: string) => Promise<void>
  onRevoke: (invitationId: string) => Promise<void>
  roleOptions?: string[]
}) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phoneNumber, setPhoneNumber] = useState('')
  const [role, setRole] = useState('CaseWorker')
  const [touched, setTouched] = useState<Partial<Record<InvitationField, boolean>>>({})
  const [submitted, setSubmitted] = useState(false)

  const errors = validateInvitation(fullName, email, phoneNumber)

  function errorFor(field: InvitationField): string | null {
    if (submitted === false && touched[field] !== true) return null
    return errors[field] ?? null
  }

  function markTouched(field: InvitationField) {
    setTouched((current) => ({ ...current, [field]: true }))
  }

  async function handleInvite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)

    const currentErrors = validateInvitation(fullName, email, phoneNumber)

    if (Object.keys(currentErrors).length > 0) return

    const invited = await onInvite(
      normalizeWhitespace(fullName),
      normalizeEmail(email),
      normalizeOptional(phoneNumber) ?? "",
      role,
    )

    if (invited) {
      setFullName('')
      setEmail('')
      setPhoneNumber('')
      setRole('CaseWorker')
      setTouched({})
      setSubmitted(false)
    }
  }

  const fullNameError = errorFor("fullName")
  const emailError = errorFor("email")
  const phoneError = errorFor("phoneNumber")

  return (
    <>
      <form className="case-card inline-form" noValidate onSubmit={handleInvite}>
        <input
          aria-describedby={
            fullNameError !== null ? "invitation-full-name-error" : undefined
          }
          aria-invalid={fullNameError !== null}
          aria-label="Full name"
          disabled={loading || disabled}
          id="invitation-full-name"
          maxLength={MAX_NAME_LENGTH}
          onBlur={() => markTouched("fullName")}
          onChange={(event) => setFullName(event.target.value)}
          placeholder="Full name"
          required
          value={fullName}
        />
        {fullNameError !== null && (
          <span
            className="field-error"
            id="invitation-full-name-error"
            role="alert"
          >
            {fullNameError}
          </span>
        )}
        <input
          aria-describedby={
            emailError !== null ? "invitation-email-error" : undefined
          }
          aria-invalid={emailError !== null}
          aria-label="Email address"
          autoCapitalize="none"
          disabled={loading || disabled}
          id="invitation-email"
          inputMode="email"
          maxLength={MAX_EMAIL_LENGTH}
          onBlur={() => markTouched("email")}
          onChange={(event) => setEmail(event.target.value)}
          placeholder="Email address"
          required
          spellCheck={false}
          type="email"
          value={email}
        />
        {emailError !== null && (
          <span className="field-error" id="invitation-email-error" role="alert">
            {emailError}
          </span>
        )}
        <input
          aria-describedby={
            phoneError !== null ? "invitation-phone-error" : undefined
          }
          aria-invalid={phoneError !== null}
          aria-label="Phone number"
          disabled={loading || disabled}
          id="invitation-phone"
          inputMode="tel"
          maxLength={MAX_PHONE_LENGTH}
          onBlur={() => markTouched("phoneNumber")}
          onChange={(event) =>
            setPhoneNumber(sanitizePhoneInput(event.target.value))
          }
          placeholder="Phone number (optional)"
          type="tel"
          value={phoneNumber}
        />
        {phoneError !== null && (
          <span className="field-error" id="invitation-phone-error" role="alert">
            {phoneError}
          </span>
        )}
        <select
          aria-label="Role"
          disabled={loading || disabled}
          id="invitation-role"
          value={role}
          onChange={(event) => setRole(event.target.value)}
        >
          {roleOptions.map((option) => (
            <option key={option} value={option}>{option}</option>
          ))}
        </select>
        <button
          className="primary-button"
          disabled={loading || disabled}
          type="submit"
        >
          <Icon className="button-icon" name="invite" />
          <span>{loading ? 'Inviting…' : 'Invite member'}</span>
        </button>
      </form>

      <small className="muted" style={{ display: 'block', margin: '0 0 16px 4px' }}>
        No password is created or emailed by CivicFlow. The invitee sets their
        own password from the link, and the link stops working once used.
      </small>

      <section className="case-card admin-panel">
        <h2>Invitations</h2>

        {invitations.length === 0 ? (
          <p className="muted empty-state">
            No invitations have been sent for this organization yet.
          </p>
        ) : (
          <div className="membership-list">
            {invitations.map((invitation) => (
              <div className="membership-row" key={invitation.id}>
                <div style={{ minWidth: 220 }}>
                  <strong>{invitation.fullName}</strong>
                  <span style={{ display: 'block' }}>{invitation.email}</span>
                  <small className="muted">
                    {invitation.role} · expires{' '}
                    {formatDateTime(invitation.expiresAt)}
                  </small>
                  {invitation.phoneNumber && (
                    <small className="muted">{invitation.phoneNumber}</small>
                  )}
                  {invitation.deliveryFailed && (
                    <small className="muted">
                      Email delivery failed. Use Resend to retry.
                    </small>
                  )}
                </div>
                <span
                  className={
                    invitationStatus(invitation) === 'Pending'
                      ? 'status-green'
                      : invitationStatus(invitation) === 'Delivery failed'
                        ? 'status-amber'
                        : undefined
                  }
                >
                  {invitationStatus(invitation)}
                </span>
                <div style={{ display: 'flex', gap: 8 }}>
                  {canResend(invitation) && (
                    <button
                      className="secondary-button"
                      disabled={loading}
                      onClick={() => void onResend(invitation.id)}
                      type="button"
                    >
                      <Icon className="button-icon" name="refresh" />
                      <span>Resend</span>
                    </button>
                  )}
                  {canRevoke(invitation) && (
                    <button
                      className="secondary-button"
                      disabled={loading}
                      onClick={() => void onRevoke(invitation.id)}
                      type="button"
                    >
                      <Icon className="button-icon" name="x" />
                      <span>Revoke</span>
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </>
  )
}
