import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { ApiError } from '../api-client'
import {
  getMyProfile,
  updateMyProfile,
  type Profile,
  type UpdateProfileRequest,
} from '../api/profile'
import { StatCard } from '../components/StatCard'
import { formatDate } from '../utils/format'
import {
  MAX_ADDRESS_LENGTH,
  MAX_IDENTIFIER_LENGTH,
  MAX_NAME_LENGTH,
  MAX_PHONE_LENGTH,
  countDigits,
  normalizeIdentifier,
  normalizeWhitespace,
  sanitizePhoneInput,
  validateAge,
  validateIdentifier,
  validateLength,
  validateName,
  validatePhone,
} from '../utils/validation'

const genderOptions = ['Female', 'Male', 'Other', 'Prefer not to say']

const roleLabels: Record<string, string> = {
  citizen: 'Citizen',
  staff: 'Organization staff',
}

function formatRoles(roles: string[]): string {
  const labels = [...new Set(roles.map((role) => roleLabels[role] ?? 'Account holder'))]
  return labels.length > 0 ? labels.join(', ') : '—'
}

type ProfileForm = {
  fullName: string
  phoneNumber: string
  nidNumber: string
  citizenshipNumber: string
  age: string
  gender: string
  permanentAddress: string
  temporaryAddress: string
}

const emptyForm: ProfileForm = {
  fullName: '',
  phoneNumber: '',
  nidNumber: '',
  citizenshipNumber: '',
  age: '',
  gender: '',
  permanentAddress: '',
  temporaryAddress: '',
}

function toForm(profile: Profile): ProfileForm {
  return {
    fullName: profile.fullName ?? '',
    phoneNumber: profile.phoneNumber ?? '',
    nidNumber: profile.nidNumber ?? '',
    citizenshipNumber: profile.citizenshipNumber ?? '',
    age: profile.age === null || profile.age === undefined ? '' : String(profile.age),
    gender: profile.gender ?? '',
    permanentAddress: profile.permanentAddress ?? '',
    temporaryAddress: profile.temporaryAddress ?? '',
  }
}

type FieldErrors = Partial<
  Record<keyof ProfileForm, string>
>

function validate(form: ProfileForm, isCitizen: boolean): FieldErrors {
  const errors: FieldErrors = {}

  const fullName = validateName(form.fullName)
  if (fullName !== null) errors.fullName = fullName

  const phone = validatePhone(form.phoneNumber)
  if (phone !== null) errors.phoneNumber = phone

  if (isCitizen === false) return errors

  const nid = validateIdentifier(form.nidNumber, "National ID number")
  if (nid !== null) errors.nidNumber = nid

  const citizenship = validateIdentifier(
    form.citizenshipNumber,
    "Citizenship number",
  )
  if (citizenship !== null) errors.citizenshipNumber = citizenship

  const age = validateAge(form.age)
  if (age !== null) errors.age = age

  const permanentAddress = validateLength(
    form.permanentAddress,
    "Permanent address",
    MAX_ADDRESS_LENGTH,
  )
  if (permanentAddress !== null) errors.permanentAddress = permanentAddress

  const temporaryAddress = validateLength(
    form.temporaryAddress,
    "Temporary address",
    MAX_ADDRESS_LENGTH,
  )
  if (temporaryAddress !== null) errors.temporaryAddress = temporaryAddress

  return errors
}

/**
 * The single self-service profile surface shared by citizens and organization
 * staff. Citizens also maintain their civil registry details; staff maintain
 * their contact details. Email stays read-only because changing it requires a
 * fresh verification.
 */
export default function ProfilePage() {
  const [profile, setProfile] = useState<Profile | null>(null)
  const [form, setForm] = useState<ProfileForm>(emptyForm)
  const [touched, setTouched] = useState<Record<string, boolean>>({})
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [submitted, setSubmitted] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)

  const isCitizen = profile?.accountType === 'Citizen'

  useEffect(() => {
    let cancelled = false

    const run = async () => {
      try {
        const data = await getMyProfile()
        if (cancelled) return
        setProfile(data)
        setForm(toForm(data))
        setError(null)
      } catch (reason) {
        if (cancelled) return
        if (reason instanceof ApiError) {
          setError(
            reason.status === 403
              ? 'Your account is not allowed to maintain a profile.'
              : reason.message,
          )
        } else {
          setError('Unable to load your profile.')
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    void run()
    return () => {
      cancelled = true
    }
  }, [reloadToken])

  function reload() {
    setLoading(true)
    setReloadToken((value) => value + 1)
  }

  const errors = useMemo(
    () => validate(form, isCitizen),
    [form, isCitizen],
  )

  const normalizedName = useMemo(
    () => normalizeWhitespace(form.fullName),
    [form.fullName],
  )

  const phoneDigits = useMemo(
    () => countDigits(form.phoneNumber),
    [form.phoneNumber],
  )

  function update<K extends keyof ProfileForm>(field: K, value: ProfileForm[K]) {
    setForm((current) => ({ ...current, [field]: value }))
  }

  function markTouched(field: keyof ProfileForm) {
    setTouched((current) => ({ ...current, [field]: true }))
  }

  function errorFor(field: keyof ProfileForm): string | null {
    if (!submitted && !touched[field]) return null
    return errors[field] ?? null
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSubmitted(true)
    setSuccess(null)

    if (Object.keys(errors).length > 0) {
      setError('Fix the highlighted fields before saving your profile.')
      return
    }

    const request: UpdateProfileRequest = {
      fullName: normalizedName,
      phoneNumber: sanitizePhoneInput(form.phoneNumber).trim() || null,
    }

    if (isCitizen) {
      request.nidNumber = normalizeIdentifier(form.nidNumber) || null
      request.citizenshipNumber =
        normalizeIdentifier(form.citizenshipNumber) || null
      request.age = form.age.trim()
        ? Number.parseInt(form.age, 10)
        : null
      request.gender = form.gender || null
      request.permanentAddress = form.permanentAddress.trim() || null
      request.temporaryAddress = form.temporaryAddress.trim() || null
    }

    try {
      setSaving(true)
      setError(null)

      const updated = await updateMyProfile(request)

      setProfile(updated)
      setForm(toForm(updated))
      setSuccess('Your profile has been updated.')
    } catch (reason) {
      if (reason instanceof ApiError) {
        setError(reason.message)
      } else {
        setError('Unable to save your profile.')
      }
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return <p className="muted">Loading your profile…</p>
  }

  if (!profile) {
    return (
      <div className="case-card profile-form">
        <h2>Profile unavailable</h2>
        <p className="muted">{error ?? 'Unable to load your profile.'}</p>
        <div>
          <button className="secondary-button" type="button" onClick={reload}>
            Try again
          </button>
        </div>
      </div>
    )
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <div className="eyebrow">Account</div>
          <h1>Update profile</h1>
          <p className="muted">
            Keep your contact details current so case updates reach you without
            delay.
          </p>
        </div>
      </div>

      <div className="stat-grid profile-stat-grid">
        <StatCard
          icon="profile"
          label="Account type"
          value={profile.accountType === 'Citizen' ? 'Citizen' : 'Organization staff'}
          detail={formatRoles(profile.roles)}
          tone="blue"
        />
        <StatCard
          icon="mail"
          label="Email address"
          value={profile.email}
          detail={profile.emailConfirmed ? 'Verified' : 'Confirmation pending'}
          tone={profile.emailConfirmed ? 'green' : 'amber'}
        />
        <StatCard
          icon="building"
          label="Workspace"
          value={profile.organizationName ?? 'Citizen services'}
          detail={profile.organizationRole ?? 'Personal account'}
          tone="purple"
        />
        <StatCard
          icon="calendar"
          label="Last updated"
          value={formatDate(profile.updatedAt)}
          detail="Most recent change"
          tone="amber"
        />
      </div>

      {error && (
        <div className="auth-error" role="alert">
          {error}
        </div>
      )}

      {success && (
        <div className="info-banner" role="status">
          {success}
        </div>
      )}

      <div className="profile-layout">
        <form className="case-card profile-form" onSubmit={handleSubmit}>
          <div className="section-heading">
            <div>
              <h2>Personal details</h2>
              <p className="muted">
                {isCitizen
                  ? 'Your civil registry details are used to verify submitted cases.'
                  : 'Your contact details are visible to your organization colleagues.'}
              </p>
            </div>
          </div>

          <label>
            Full name
            <input
              aria-describedby={
                errorFor('fullName')
                  ? 'profile-full-name-error'
                  : 'profile-full-name-hint'
              }
              aria-invalid={errorFor('fullName') !== null}
              id="profile-full-name"
              type="text"
              maxLength={MAX_NAME_LENGTH}
              value={form.fullName}
              onChange={(event) => update('fullName', event.target.value)}
              onBlur={() => markTouched('fullName')}
              disabled={saving}
            />
            {errorFor('fullName') ? (
              <span className="field-error" id="profile-full-name-error" role="alert">{errorFor('fullName')}</span>
            ) : (
              <span className="field-hint" id="profile-full-name-hint">
                Saved as “{normalizedName || '—'}”
              </span>
            )}
          </label>

          <label>
            Email address
            <input
              aria-describedby="profile-email-hint"
              id="profile-email"
              type="email"
              value={profile.email}
              readOnly
              disabled
            />
            <span className="field-hint" id="profile-email-hint">
              {profile.emailConfirmed
                ? 'Your email address is verified.'
                : 'Your email address is not verified yet. Contact support to change it.'}
            </span>
          </label>

          <label>
            Phone number
            <input
              aria-describedby={
                errorFor('phoneNumber')
                  ? 'profile-phone-error'
                  : 'profile-phone-hint'
              }
              aria-invalid={errorFor('phoneNumber') !== null}
              id="profile-phone"
              type="tel"
              maxLength={MAX_PHONE_LENGTH}
              value={form.phoneNumber}
              onChange={(event) =>
                update('phoneNumber', sanitizePhoneInput(event.target.value))
              }
              onBlur={() => markTouched('phoneNumber')}
              disabled={saving}
              placeholder="+977 98XXXXXXXX"
            />
            {errorFor('phoneNumber') ? (
              <span className="field-error" id="profile-phone-error" role="alert">{errorFor('phoneNumber')}</span>
            ) : (
              <span className="field-hint" id="profile-phone-hint">
                {phoneDigits > 0
                  ? `${phoneDigits} digits entered.`
                  : 'Optional. Used for case notifications.'}
              </span>
            )}
          </label>

          {isCitizen && (
            <>
              <div className="profile-form-row">
                <label>
                  National ID number
                  <input
                    aria-describedby={
                      errorFor('nidNumber')
                        ? 'profile-nid-error'
                        : undefined
                    }
                    aria-invalid={errorFor('nidNumber') !== null}
                    id="profile-nid"
                    type="text"
                    maxLength={MAX_IDENTIFIER_LENGTH}
                    value={form.nidNumber}
                    onChange={(event) =>
                      update('nidNumber', normalizeIdentifier(event.target.value))
                    }
                    onBlur={() => markTouched('nidNumber')}
                    disabled={saving}
                  />
                  {errorFor('nidNumber') && (
                    <span className="field-error" id="profile-nid-error" role="alert">{errorFor('nidNumber')}</span>
                  )}
                </label>

                <label>
                  Citizenship number
                  <input
                    aria-describedby={
                      errorFor('citizenshipNumber')
                        ? 'profile-citizenship-error'
                        : undefined
                    }
                    aria-invalid={errorFor('citizenshipNumber') !== null}
                    id="profile-citizenship"
                    type="text"
                    maxLength={MAX_IDENTIFIER_LENGTH}
                    value={form.citizenshipNumber}
                    onChange={(event) =>
                      update(
                        'citizenshipNumber',
                        normalizeIdentifier(event.target.value),
                      )
                    }
                    onBlur={() => markTouched('citizenshipNumber')}
                    disabled={saving}
                  />
                  {errorFor('citizenshipNumber') && (
                    <span
                      className="field-error"
                      id="profile-citizenship-error"
                      role="alert"
                    >
                      {errorFor('citizenshipNumber')}
                    </span>
                  )}
                </label>
              </div>

              <div className="profile-form-row">
                <label>
                  Age
                  <input
                    aria-describedby={
                      errorFor('age') ? 'profile-age-error' : undefined
                    }
                    aria-invalid={errorFor('age') !== null}
                    id="profile-age"
                    type="number"
                    min={0}
                    max={150}
                    value={form.age}
                    onChange={(event) => update('age', event.target.value)}
                    onBlur={() => markTouched('age')}
                    disabled={saving}
                  />
                  {errorFor('age') && (
                    <span className="field-error" id="profile-age-error" role="alert">{errorFor('age')}</span>
                  )}
                </label>

                <label>
                  Gender
                  <select
                    id="profile-gender"
                    value={form.gender}
                    onChange={(event) => update('gender', event.target.value)}
                    disabled={saving}
                  >
                    <option value="">Not specified</option>
                    {genderOptions.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                    {form.gender &&
                      !genderOptions.includes(form.gender) && (
                        <option value={form.gender}>{form.gender}</option>
                      )}
                  </select>
                </label>
              </div>

              <label>
                Permanent address
                <textarea
                  aria-describedby={
                    errorFor('permanentAddress')
                      ? 'profile-permanent-address-error'
                      : 'profile-permanent-address-hint'
                  }
                  aria-invalid={errorFor('permanentAddress') !== null}
                  id="profile-permanent-address"
                  maxLength={MAX_ADDRESS_LENGTH}
                  value={form.permanentAddress}
                  onChange={(event) =>
                    update('permanentAddress', event.target.value)
                  }
                  onBlur={() => markTouched('permanentAddress')}
                  disabled={saving}
                />
                {errorFor('permanentAddress') && (
                  <span
                    className="field-error"
                    id="profile-permanent-address-error"
                    role="alert"
                  >
                    {errorFor('permanentAddress')}
                  </span>
                )}
                <span className="field-hint" id="profile-permanent-address-hint">
                  {form.permanentAddress.length}/{MAX_ADDRESS_LENGTH} characters
                </span>
              </label>

              <label>
                Temporary address
                <textarea
                  aria-describedby={
                    errorFor('temporaryAddress')
                      ? 'profile-temporary-address-error'
                      : 'profile-temporary-address-hint'
                  }
                  aria-invalid={errorFor('temporaryAddress') !== null}
                  id="profile-temporary-address"
                  maxLength={MAX_ADDRESS_LENGTH}
                  value={form.temporaryAddress}
                  onChange={(event) =>
                    update('temporaryAddress', event.target.value)
                  }
                  onBlur={() => markTouched('temporaryAddress')}
                  disabled={saving}
                />
                {errorFor('temporaryAddress') && (
                  <span
                    className="field-error"
                    id="profile-temporary-address-error"
                    role="alert"
                  >
                    {errorFor('temporaryAddress')}
                  </span>
                )}
                <span className="field-hint" id="profile-temporary-address-hint">
                  {form.temporaryAddress.length}/{MAX_ADDRESS_LENGTH} characters
                </span>
              </label>
            </>
          )}

          <div className="profile-form-actions">
            <button className="primary-button" type="submit" disabled={saving}>
              {saving ? 'Saving…' : 'Save changes'}
            </button>
            <button
              className="secondary-button"
              type="button"
              onClick={() => {
                setForm(toForm(profile))
                setTouched({})
                setSubmitted(false)
                setError(null)
                setSuccess(null)
              }}
              disabled={saving}
            >
              Reset
            </button>
          </div>
        </form>

        <aside className="detail-card profile-summary">
          <div className="detail-header">
            <span>Account summary</span>
          </div>

          <dl>
            <div>
              <dt>Full name</dt>
              <dd>{profile.fullName || '—'}</dd>
            </div>
            <div>
              <dt>Email</dt>
              <dd>{profile.email}</dd>
            </div>
            <div>
              <dt>Phone</dt>
              <dd>{profile.phoneNumber || 'Not provided'}</dd>
            </div>
            <div>
              <dt>Roles</dt>
              <dd>{formatRoles(profile.roles)}</dd>
            </div>
            <div>
              <dt>Organization</dt>
              <dd>{profile.organizationName ?? '—'}</dd>
            </div>
            <div>
              <dt>Last updated</dt>
              <dd>{formatDate(profile.updatedAt)}</dd>
            </div>
          </dl>

          <p className="muted">
            Changes are recorded in the audit trail with your account and the
            fields you edited.
          </p>
        </aside>
      </div>
    </>
  )
}
