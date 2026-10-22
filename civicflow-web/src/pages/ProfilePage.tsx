import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react'
import { ApiError } from '../api-client'
import {
  getMyProfile,
  updateMyProfile,
  type Profile,
  type UpdateProfileRequest,
} from '../api/profile'
import { StatCard } from '../components/StatCard'

const genderOptions = ['Female', 'Male', 'Other', 'Prefer not to say']

const MAX_NAME = 150
const MAX_PHONE = 30
const MAX_IDENTIFIER = 50
const MAX_ADDRESS = 500

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

/** Collapses runs of whitespace so the stored full name reads consistently. */
function normalizeFullName(value: string): string {
  return value.trim().replace(/\s+/g, ' ')
}

/** Keeps only the characters a phone number may contain. */
function sanitizePhone(value: string): string {
  return value.replace(/[^0-9+()\-./\s]/g, '')
}

/** Identifiers are stored upper-case so lookups stay case-insensitive. */
function normalizeIdentifier(value: string): string {
  return value.trim().toUpperCase()
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

function formatDate(value: string | null | undefined): string {
  if (!value) return '—'

  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return '—'

  return parsed.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  })
}

type FieldErrors = Partial<
  Record<keyof ProfileForm, string>
>

function validate(form: ProfileForm, isCitizen: boolean): FieldErrors {
  const errors: FieldErrors = {}

  const fullName = normalizeFullName(form.fullName)

  if (!fullName) {
    errors.fullName = 'A full name is required.'
  } else if (fullName.length > MAX_NAME) {
    errors.fullName = `The full name must be ${MAX_NAME} characters or fewer.`
  }

  const phone = sanitizePhone(form.phoneNumber).trim()

  if (phone) {
    const digits = phone.replace(/\D/g, '')

    if (phone.length > MAX_PHONE) {
      errors.phoneNumber = `The phone number must be ${MAX_PHONE} characters or fewer.`
    } else if (digits.length < 6) {
      errors.phoneNumber = 'Enter a phone number with at least 6 digits.'
    }
  }

  if (!isCitizen) return errors

  if (normalizeIdentifier(form.nidNumber).length > MAX_IDENTIFIER) {
    errors.nidNumber = `The national ID must be ${MAX_IDENTIFIER} characters or fewer.`
  }

  if (normalizeIdentifier(form.citizenshipNumber).length > MAX_IDENTIFIER) {
    errors.citizenshipNumber = `The citizenship number must be ${MAX_IDENTIFIER} characters or fewer.`
  }

  if (form.age.trim()) {
    const age = Number.parseInt(form.age, 10)

    if (!Number.isFinite(age) || age < 0 || age > 150) {
      errors.age = 'Enter an age between 0 and 150.'
    }
  }

  if (form.permanentAddress.length > MAX_ADDRESS) {
    errors.permanentAddress = `The address must be ${MAX_ADDRESS} characters or fewer.`
  }

  if (form.temporaryAddress.length > MAX_ADDRESS) {
    errors.temporaryAddress = `The address must be ${MAX_ADDRESS} characters or fewer.`
  }

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

  const isCitizen = profile?.accountType === 'Citizen'

  const load = useCallback(async () => {
    try {
      setLoading(true)
      const data = await getMyProfile()
      setProfile(data)
      setForm(toForm(data))
      setError(null)
    } catch (reason) {
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
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  const errors = useMemo(
    () => validate(form, isCitizen),
    [form, isCitizen],
  )

  const normalizedName = useMemo(
    () => normalizeFullName(form.fullName),
    [form.fullName],
  )

  const phoneDigits = useMemo(
    () => sanitizePhone(form.phoneNumber).replace(/\D/g, '').length,
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
      phoneNumber: sanitizePhone(form.phoneNumber).trim() || null,
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
          <button className="secondary-button" type="button" onClick={load}>
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

      <div className="stat-grid">
        <StatCard
          label="Account type"
          value={profile.accountType === 'Citizen' ? 'Citizen' : 'Organization staff'}
          detail={profile.roles.join(', ') || 'No role assigned'}
          tone="blue"
        />
        <StatCard
          label="Email address"
          value={profile.email}
          detail={profile.emailConfirmed ? 'Verified' : 'Confirmation pending'}
          tone={profile.emailConfirmed ? 'green' : 'amber'}
        />
        <StatCard
          label="Workspace"
          value={profile.organizationName ?? 'Citizen services'}
          detail={profile.organizationRole ?? 'Personal account'}
          tone="purple"
        />
        <StatCard
          label="Last updated"
          value={formatDate(profile.updatedAt)}
          detail={`User ${profile.userId.slice(0, 8)}`}
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
              type="text"
              maxLength={MAX_NAME}
              value={form.fullName}
              onChange={(event) => update('fullName', event.target.value)}
              onBlur={() => markTouched('fullName')}
              disabled={saving}
            />
            {errorFor('fullName') ? (
              <span className="field-error">{errorFor('fullName')}</span>
            ) : (
              <span className="field-hint">
                Saved as “{normalizedName || '—'}”
              </span>
            )}
          </label>

          <label>
            Email address
            <input type="email" value={profile.email} readOnly disabled />
            <span className="field-hint">
              {profile.emailConfirmed
                ? 'Your email address is verified.'
                : 'Your email address is not verified yet. Contact support to change it.'}
            </span>
          </label>

          <label>
            Phone number
            <input
              type="tel"
              maxLength={MAX_PHONE}
              value={form.phoneNumber}
              onChange={(event) =>
                update('phoneNumber', sanitizePhone(event.target.value))
              }
              onBlur={() => markTouched('phoneNumber')}
              disabled={saving}
              placeholder="+977 98XXXXXXXX"
            />
            {errorFor('phoneNumber') ? (
              <span className="field-error">{errorFor('phoneNumber')}</span>
            ) : (
              <span className="field-hint">
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
                    type="text"
                    maxLength={MAX_IDENTIFIER}
                    value={form.nidNumber}
                    onChange={(event) =>
                      update('nidNumber', normalizeIdentifier(event.target.value))
                    }
                    onBlur={() => markTouched('nidNumber')}
                    disabled={saving}
                  />
                  {errorFor('nidNumber') && (
                    <span className="field-error">{errorFor('nidNumber')}</span>
                  )}
                </label>

                <label>
                  Citizenship number
                  <input
                    type="text"
                    maxLength={MAX_IDENTIFIER}
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
                    <span className="field-error">
                      {errorFor('citizenshipNumber')}
                    </span>
                  )}
                </label>
              </div>

              <div className="profile-form-row">
                <label>
                  Age
                  <input
                    type="number"
                    min={0}
                    max={150}
                    value={form.age}
                    onChange={(event) => update('age', event.target.value)}
                    onBlur={() => markTouched('age')}
                    disabled={saving}
                  />
                  {errorFor('age') && (
                    <span className="field-error">{errorFor('age')}</span>
                  )}
                </label>

                <label>
                  Gender
                  <select
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
                  maxLength={MAX_ADDRESS}
                  value={form.permanentAddress}
                  onChange={(event) =>
                    update('permanentAddress', event.target.value)
                  }
                  onBlur={() => markTouched('permanentAddress')}
                  disabled={saving}
                />
                <span className="field-hint">
                  {form.permanentAddress.length}/{MAX_ADDRESS} characters
                </span>
              </label>

              <label>
                Temporary address
                <textarea
                  maxLength={MAX_ADDRESS}
                  value={form.temporaryAddress}
                  onChange={(event) =>
                    update('temporaryAddress', event.target.value)
                  }
                  onBlur={() => markTouched('temporaryAddress')}
                  disabled={saving}
                />
                <span className="field-hint">
                  {form.temporaryAddress.length}/{MAX_ADDRESS} characters
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
              <dd>{profile.roles.join(', ') || '—'}</dd>
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
