import { Fragment, useMemo, useState } from 'react'
import type {
  PlatformCitizen,
  PlatformCitizenUpdate,
  PlatformOverview,
  PlatformStaffUpdate,
  PlatformUser,
  PlatformUserOrganization,
} from '../domain'
import { Icon } from './Icon'
import { formatDate } from '../utils/format'
import {
  MAX_ADDRESS_LENGTH,
  MAX_GENDER_LENGTH,
  MAX_IDENTIFIER_LENGTH,
  MAX_NAME_LENGTH,
  MAX_PHONE_LENGTH,
  normalizeOptional,
  normalizeOptionalIdentifier,
  normalizeWhitespace,
  sanitizePhoneInput,
  validateAge,
  validateIdentifier,
  validateLength,
  validateName,
  validatePhone,
} from '../utils/validation'
import {
  toggleCitizenAccountActive,
  updateCitizenProfile,
} from '../api/citizens-admin'
import {
  activatePlatformUser,
  changePlatformRole,
  deactivatePlatformUser,
  updatePlatformStaff,
} from '../api/platform'

const PAGE_SIZE = 10
const PLATFORM_ROLES = ['None', 'SuperAdmin'] as const

type DirectoryTab = 'citizens' | 'staff' | 'users'
type SortField =
  | 'fullName'
  | 'email'
  | 'createdAt'
  | 'accountType'
  | 'organization'
type AccountFilter = 'all' | 'citizens' | 'staff'

function Pagination({
  page,
  pageCount,
  onPageChange,
}: {
  page: number
  pageCount: number
  onPageChange: (page: number) => void
}) {
  if (pageCount <= 1) return null

  return (
    <div className="admin-row" style={{ marginTop: 12, justifyContent: 'flex-end' }}>
      <button
        className="secondary-button"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
        type="button"
      >
        Previous
      </button>
      <span className="muted">
        Page {page} of {pageCount}
      </span>
      <button
        className="secondary-button"
        disabled={page >= pageCount}
        onClick={() => onPageChange(page + 1)}
        type="button"
      >
        Next
      </button>
    </div>
  )
}

function CitizenEditor({
  citizen,
  busy,
  onCancel,
  onSave,
}: {
  citizen: PlatformCitizen
  busy: boolean
  onCancel: () => void
  onSave: (request: PlatformCitizenUpdate) => Promise<void>
}) {
  const [fullName, setFullName] = useState(citizen.fullName)
  const [phoneNumber, setPhoneNumber] = useState(citizen.phoneNumber ?? '')
  const [nidNumber, setNidNumber] = useState(citizen.nidNumber ?? '')
  const [citizenshipNumber, setCitizenshipNumber] = useState(citizen.citizenshipNumber ?? '')
  const [age, setAge] = useState(citizen.age?.toString() ?? '')
  const [gender, setGender] = useState(citizen.gender ?? '')
  const [permanentAddress, setPermanentAddress] = useState(citizen.permanentAddress ?? '')
  const [temporaryAddress, setTemporaryAddress] = useState(citizen.temporaryAddress ?? '')
  const [submitted, setSubmitted] = useState(false)

  const errors = {
    fullName: validateName(fullName),
    phoneNumber: validatePhone(phoneNumber),
    nidNumber: validateIdentifier(nidNumber, "NID number"),
    citizenshipNumber: validateIdentifier(
      citizenshipNumber,
      "Citizenship number",
    ),
    age: validateAge(age),
    gender: validateLength(gender, "Gender", MAX_GENDER_LENGTH),
    permanentAddress: validateLength(
      permanentAddress,
      "Permanent address",
      MAX_ADDRESS_LENGTH,
    ),
    temporaryAddress: validateLength(
      temporaryAddress,
      "Temporary address",
      MAX_ADDRESS_LENGTH,
    ),
  }
  const hasErrors = Object.values(errors).some((value) => value !== null)

  function showError(field: keyof typeof errors): string | null {
    return submitted ? errors[field] : null
  }

  return (
    <tr>
      <td colSpan={10}>
        <form
          className="inline-form"
          noValidate
          style={{ flexWrap: 'wrap', gap: 8 }}
          onSubmit={(event) => {
            event.preventDefault()
            setSubmitted(true)

            if (hasErrors) return

            void onSave({
              fullName: normalizeWhitespace(fullName),
              phoneNumber: normalizeOptional(phoneNumber),
              nidNumber: normalizeOptionalIdentifier(nidNumber),
              citizenshipNumber: normalizeOptionalIdentifier(citizenshipNumber),
              age: age.trim().length > 0 ? Number.parseInt(age, 10) : null,
              gender: normalizeOptional(gender),
              permanentAddress: normalizeOptional(permanentAddress),
              temporaryAddress: normalizeOptional(temporaryAddress),
            })
          }}
        >
          <input
            aria-describedby={
              showError("fullName") !== null
                ? "platform-citizen-name-error"
                : undefined
            }
            aria-invalid={showError("fullName") !== null}
            aria-label="Full name"
            id="platform-citizen-name"
            maxLength={MAX_NAME_LENGTH}
            onChange={(event) => setFullName(event.target.value)}
            required
            value={fullName}
          />
          {showError("fullName") !== null && (
            <span className="field-error" id="platform-citizen-name-error" role="alert">
              {errors.fullName}
            </span>
          )}

          <input
            aria-describedby={
              showError("phoneNumber") !== null
                ? "platform-citizen-phone-error"
                : undefined
            }
            aria-invalid={showError("phoneNumber") !== null}
            aria-label="Phone number"
            id="platform-citizen-phone"
            inputMode="tel"
            maxLength={MAX_PHONE_LENGTH}
            onChange={(event) =>
              setPhoneNumber(sanitizePhoneInput(event.target.value))
            }
            placeholder="Phone number"
            type="tel"
            value={phoneNumber}
          />
          {showError("phoneNumber") !== null && (
            <span className="field-error" id="platform-citizen-phone-error" role="alert">
              {errors.phoneNumber}
            </span>
          )}

          <input
            aria-describedby={
              showError("nidNumber") !== null
                ? "platform-citizen-nid-error"
                : undefined
            }
            aria-invalid={showError("nidNumber") !== null}
            aria-label="NID number"
            id="platform-citizen-nid"
            maxLength={MAX_IDENTIFIER_LENGTH}
            onChange={(event) => setNidNumber(event.target.value.toUpperCase())}
            placeholder="NID number"
            value={nidNumber}
          />
          {showError("nidNumber") !== null && (
            <span className="field-error" id="platform-citizen-nid-error" role="alert">
              {errors.nidNumber}
            </span>
          )}

          <input
            aria-describedby={
              showError("citizenshipNumber") !== null
                ? "platform-citizen-citizenship-error"
                : undefined
            }
            aria-invalid={showError("citizenshipNumber") !== null}
            aria-label="Citizenship number"
            id="platform-citizen-citizenship"
            maxLength={MAX_IDENTIFIER_LENGTH}
            onChange={(event) =>
              setCitizenshipNumber(event.target.value.toUpperCase())
            }
            placeholder="Citizenship number"
            value={citizenshipNumber}
          />
          {showError("citizenshipNumber") !== null && (
            <span
              className="field-error"
              id="platform-citizen-citizenship-error"
              role="alert"
            >
              {errors.citizenshipNumber}
            </span>
          )}

          <input
            aria-describedby={
              showError("age") !== null ? "platform-citizen-age-error" : undefined
            }
            aria-invalid={showError("age") !== null}
            aria-label="Age"
            id="platform-citizen-age"
            inputMode="numeric"
            max={150}
            min={0}
            onChange={(event) => setAge(event.target.value)}
            placeholder="Age"
            type="number"
            value={age}
          />
          {showError("age") !== null && (
            <span className="field-error" id="platform-citizen-age-error" role="alert">
              {errors.age}
            </span>
          )}

          <input
            aria-describedby={
              showError("gender") !== null
                ? "platform-citizen-gender-error"
                : undefined
            }
            aria-invalid={showError("gender") !== null}
            aria-label="Gender"
            id="platform-citizen-gender"
            maxLength={MAX_GENDER_LENGTH}
            onChange={(event) => setGender(event.target.value)}
            placeholder="Gender"
            value={gender}
          />
          {showError("gender") !== null && (
            <span className="field-error" id="platform-citizen-gender-error" role="alert">
              {errors.gender}
            </span>
          )}

          <input
            aria-describedby={
              showError("permanentAddress") !== null
                ? "platform-citizen-permanent-error"
                : undefined
            }
            aria-invalid={showError("permanentAddress") !== null}
            aria-label="Permanent address"
            id="platform-citizen-permanent"
            maxLength={MAX_ADDRESS_LENGTH}
            onChange={(event) => setPermanentAddress(event.target.value)}
            placeholder="Permanent address"
            value={permanentAddress}
          />
          {showError("permanentAddress") !== null && (
            <span
              className="field-error"
              id="platform-citizen-permanent-error"
              role="alert"
            >
              {errors.permanentAddress}
            </span>
          )}

          <input
            aria-describedby={
              showError("temporaryAddress") !== null
                ? "platform-citizen-temporary-error"
                : undefined
            }
            aria-invalid={showError("temporaryAddress") !== null}
            aria-label="Temporary address"
            id="platform-citizen-temporary"
            maxLength={MAX_ADDRESS_LENGTH}
            onChange={(event) => setTemporaryAddress(event.target.value)}
            placeholder="Temporary address"
            value={temporaryAddress}
          />
          {showError("temporaryAddress") !== null && (
            <span
              className="field-error"
              id="platform-citizen-temporary-error"
              role="alert"
            >
              {errors.temporaryAddress}
            </span>
          )}

          <button className="primary-button" disabled={busy} type="submit">
            Save
          </button>
          <button
            className="secondary-button"
            disabled={busy}
            onClick={onCancel}
            type="button"
          >
            Cancel
          </button>
        </form>
      </td>
    </tr>
  )
}

function StaffEditor({
  user,
  busy,
  onCancel,
  onSave,
}: {
  user: PlatformUser
  busy: boolean
  onCancel: () => void
  onSave: (request: PlatformStaffUpdate) => Promise<void>
}) {
  const [fullName, setFullName] = useState(user.fullName)
  const [phoneNumber, setPhoneNumber] = useState(user.phoneNumber ?? '')
  const [submitted, setSubmitted] = useState(false)

  const fullNameError = validateName(fullName)
  const phoneError = validatePhone(phoneNumber)
  const hasErrors = fullNameError !== null || phoneError !== null

  function showNameError(): string | null {
    return submitted ? fullNameError : null
  }

  function showPhoneError(): string | null {
    return submitted ? phoneError : null
  }

  return (
    <tr>
      <td colSpan={8}>
        <form
          className="inline-form"
          noValidate
          style={{ flexWrap: 'wrap', gap: 8 }}
          onSubmit={(event) => {
            event.preventDefault()
            setSubmitted(true)

            if (hasErrors) return

            void onSave({
              fullName: normalizeWhitespace(fullName),
              phoneNumber: normalizeOptional(phoneNumber),
            })
          }}
        >
          <input
            aria-describedby={
              showNameError() !== null ? "platform-staff-name-error" : undefined
            }
            aria-invalid={showNameError() !== null}
            aria-label="Full name"
            id="platform-staff-name"
            maxLength={MAX_NAME_LENGTH}
            onChange={(event) => setFullName(event.target.value)}
            required
            value={fullName}
          />
          {showNameError() !== null && (
            <span className="field-error" id="platform-staff-name-error" role="alert">
              {fullNameError}
            </span>
          )}

          <input
            aria-describedby={
              showPhoneError() !== null ? "platform-staff-phone-error" : undefined
            }
            aria-invalid={showPhoneError() !== null}
            aria-label="Phone number"
            id="platform-staff-phone"
            inputMode="tel"
            maxLength={MAX_PHONE_LENGTH}
            onChange={(event) =>
              setPhoneNumber(sanitizePhoneInput(event.target.value))
            }
            placeholder="Phone number"
            type="tel"
            value={phoneNumber}
          />
          {showPhoneError() !== null && (
            <span className="field-error" id="platform-staff-phone-error" role="alert">
              {phoneError}
            </span>
          )}

          <button className="primary-button" disabled={busy} type="submit">
            Save
          </button>
          <button
            className="secondary-button"
            disabled={busy}
            onClick={onCancel}
            type="button"
          >
            Cancel
          </button>
        </form>
      </td>
    </tr>
  )
}

function accountType(user: PlatformUser): string {
  if (user.isCitizen && user.isStaff) return 'Citizen + Staff'
  if (user.isCitizen) return 'Citizen'
  if (user.isStaff) return 'Staff'
  return 'Account'
}

function organizationSummary(organizations: PlatformUserOrganization[]): string {
  if (organizations.length === 0) return 'No memberships'

  return organizations
    .map((organization) =>
      organization.isActive
        ? `${organization.organizationName} (${organization.role})`
        : `${organization.organizationName} (${organization.role}, inactive)`,
    )
    .join(', ')
}

/**
 * Platform-wide user directory for platform administrators. Citizen accounts,
 * staff accounts, and the consolidated account view are three views of the
 * same identity data: a user appears once per grid regardless of how many
 * organization memberships they hold.
 */
export function PlatformDirectory({
  loading,
  overview,
  citizens,
  users,
  refreshData,
  onOpenOrganization,
}: {
  loading: boolean
  overview: PlatformOverview | null
  citizens: PlatformCitizen[]
  users: PlatformUser[]
  refreshData: () => void
  onOpenOrganization: (organizationId: string) => void
}) {
  const [tab, setTab] = useState<DirectoryTab>('citizens')
  const [searchQuery, setSearchQuery] = useState('')
  const [accountFilter, setAccountFilter] = useState<AccountFilter>('all')
  const [sortField, setSortField] = useState<SortField>('fullName')
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc')
  const [page, setPage] = useState(1)
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [editingCitizenId, setEditingCitizenId] = useState<string | null>(null)
  const [editingStaffId, setEditingStaffId] = useState<string | null>(null)

  const query = searchQuery.trim().toLowerCase()
  const staffAccounts = useMemo(() => users.filter((user) => user.isStaff), [users])

  const filteredCitizens = useMemo(() => {
    const multiplier = sortOrder === 'asc' ? 1 : -1
    return citizens
      .filter(
        (citizen) =>
          !query ||
          citizen.fullName.toLowerCase().includes(query) ||
          citizen.email.toLowerCase().includes(query) ||
          (citizen.phoneNumber ?? '').toLowerCase().includes(query),
      )
      .sort((a, b) => {
        if (sortField === 'email') {
          return a.email.localeCompare(b.email) * multiplier
        }
        if (sortField === 'createdAt') {
          return (
            (new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()) *
            multiplier
          )
        }
        return a.fullName.localeCompare(b.fullName) * multiplier
      })
  }, [citizens, query, sortField, sortOrder])

  const filteredStaff = useMemo(() => {
    const multiplier = sortOrder === 'asc' ? 1 : -1
    return staffAccounts
      .filter(
        (user) =>
          !query ||
          user.fullName.toLowerCase().includes(query) ||
          user.email.toLowerCase().includes(query) ||
          (user.phoneNumber ?? '').toLowerCase().includes(query) ||
          user.organizations.some(
            (organization) =>
              organization.organizationName.toLowerCase().includes(query) ||
              organization.role.toLowerCase().includes(query),
          ),
      )
      .sort((a, b) => {
        if (sortField === 'email') {
          return a.email.localeCompare(b.email) * multiplier
        }
        if (sortField === 'createdAt') {
          return (
            (new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()) *
            multiplier
          )
        }
        if (sortField === 'organization') {
          return (
            organizationSummary(a.organizations).localeCompare(
              organizationSummary(b.organizations),
            ) * multiplier
          )
        }
        return a.fullName.localeCompare(b.fullName) * multiplier
      })
  }, [staffAccounts, query, sortField, sortOrder])

  const filteredUsers = useMemo(() => {
    const multiplier = sortOrder === 'asc' ? 1 : -1
    return users
      .filter((user) => {
        if (accountFilter === 'citizens' && !user.isCitizen) return false
        if (accountFilter === 'staff' && !user.isStaff) return false
        if (!query) return true

        return (
          user.fullName.toLowerCase().includes(query) ||
          user.email.toLowerCase().includes(query) ||
          (user.phoneNumber ?? '').toLowerCase().includes(query) ||
          accountType(user).toLowerCase().includes(query) ||
          user.organizations.some(
            (organization) =>
              organization.organizationName.toLowerCase().includes(query) ||
              organization.role.toLowerCase().includes(query),
          )
        )
      })
      .sort((a, b) => {
        if (sortField === 'email') {
          return a.email.localeCompare(b.email) * multiplier
        }
        if (sortField === 'createdAt') {
          return (
            (new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()) *
            multiplier
          )
        }
        if (sortField === 'accountType') {
          return accountType(a).localeCompare(accountType(b)) * multiplier
        }
        if (sortField === 'organization') {
          return (
            organizationSummary(a.organizations).localeCompare(
              organizationSummary(b.organizations),
            ) * multiplier
          )
        }
        return a.fullName.localeCompare(b.fullName) * multiplier
      })
  }, [users, accountFilter, query, sortField, sortOrder])

  const currentRows =
    tab === 'citizens'
      ? filteredCitizens
      : tab === 'staff'
        ? filteredStaff
        : filteredUsers
  const pageCount = Math.max(1, Math.ceil(currentRows.length / PAGE_SIZE))
  const safePage = Math.min(page, pageCount)
  const visibleCitizens = filteredCitizens.slice(
    (safePage - 1) * PAGE_SIZE,
    safePage * PAGE_SIZE,
  )
  const visibleStaff = filteredStaff.slice(
    (safePage - 1) * PAGE_SIZE,
    safePage * PAGE_SIZE,
  )
  const visibleUsers = filteredUsers.slice(
    (safePage - 1) * PAGE_SIZE,
    safePage * PAGE_SIZE,
  )

  function switchTab(nextTab: DirectoryTab) {
    setTab(nextTab)
    setPage(1)
    setEditingCitizenId(null)
    setEditingStaffId(null)
    if (nextTab === 'citizens') setSortField('fullName')
    else if (nextTab === 'staff') setSortField('organization')
    else setSortField('fullName')
  }

  async function run(
    key: string,
    action: () => Promise<void>,
    successMessage: string,
  ) {
    setBusy(key)
    setError(null)
    setNotice(null)

    try {
      await action()
      setNotice(successMessage)
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : 'The operation could not be completed.',
      )
    } finally {
      setBusy(null)
    }
  }

  async function handleSaveCitizen(
    citizen: PlatformCitizen,
    request: PlatformCitizenUpdate,
  ) {
    await run(
      `citizen-${citizen.userId}`,
      async () => {
        await updateCitizenProfile(citizen.userId, request)
        setEditingCitizenId(null)
        refreshData()
      },
      'Citizen profile updated.',
    )
  }

  async function handleSaveStaff(
    user: PlatformUser,
    request: PlatformStaffUpdate,
  ) {
    await run(
      `staff-${user.id}`,
      async () => {
        await updatePlatformStaff(user.id, request)
        setEditingStaffId(null)
        refreshData()
      },
      'Staff account updated.',
    )
  }

  async function handleToggleCitizen(citizen: PlatformCitizen) {
    await run(
      `citizen-${citizen.userId}`,
      async () => {
        await toggleCitizenAccountActive(citizen.userId, !citizen.isActive)
        refreshData()
      },
      citizen.isActive
        ? 'Citizen account deactivated.'
        : 'Citizen account activated.',
    )
  }

  async function handleToggleUser(user: PlatformUser) {
    await run(
      `user-${user.id}`,
      async () => {
        if (user.isActive) {
          await deactivatePlatformUser(user.id)
        } else {
          await activatePlatformUser(user.id)
        }
        refreshData()
      },
      user.isActive ? 'Account deactivated.' : 'Account activated.',
    )
  }

  async function handleChangePlatformRole(user: PlatformUser, role: string) {
    await run(
      `role-${user.id}`,
      async () => {
        await changePlatformRole(user.id, role)
        refreshData()
      },
      'Platform role updated.',
    )
  }

  const sortOptions =
    tab === 'citizens' ? (
      <>
        <option value="fullName">Name</option>
        <option value="email">Email</option>
        <option value="createdAt">Created</option>
      </>
    ) : tab === 'staff' ? (
      <>
        <option value="fullName">Name</option>
        <option value="email">Email</option>
        <option value="organization">Organization</option>
        <option value="createdAt">Created</option>
      </>
    ) : (
      <>
        <option value="fullName">Name</option>
        <option value="email">Email</option>
        <option value="accountType">Account type</option>
        <option value="organization">Organization</option>
        <option value="createdAt">Created</option>
      </>
    )

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PLATFORM DIRECTORY</p>
          <h1>User accounts</h1>
          <p className="muted">
            Citizen and staff accounts, plus a consolidated identity view.
            Organization memberships are managed from the Organizations tab.
          </p>
        </div>
      </div>

      {overview && (
        <div className="stat-grid">
          <div className="stat-card">
            <span className="stat-icon blue"><Icon className="app-icon" name="building" /></span>
            <div>
              <span>Accounts</span>
              <strong>{overview.userCount}</strong>
              <small>{overview.activeUserCount} active</small>
            </div>
          </div>
          <div className="stat-card">
            <span className="stat-icon purple"><Icon className="app-icon" name="directory" /></span>
            <div>
              <span>Staff memberships</span>
              <strong>{overview.staffCount}</strong>
              <small>{overview.activeStaffCount} active</small>
            </div>
          </div>
          <div className="stat-card">
            <span className="stat-icon green"><Icon className="app-icon" name="staff" /></span>
            <div>
              <span>Organizations</span>
              <strong>{overview.organizationCount}</strong>
              <small>{overview.activeOrganizationCount} active</small>
            </div>
          </div>
          <div className="stat-card">
            <span className="stat-icon amber"><Icon className="app-icon" name="mail" /></span>
            <div>
              <span>Invitations</span>
              <strong>{overview.invitationCount}</strong>
              <small>{overview.pendingInvitationCount} pending</small>
            </div>
          </div>
        </div>
      )}

      <section className="case-card admin-panel">
        <div className="admin-row" style={{ marginBottom: 16 }}>
          <button
            className={tab === 'citizens' ? 'primary-button' : 'secondary-button'}
            onClick={() => switchTab('citizens')}
            type="button"
          >
            Citizens ({citizens.length})
          </button>
          <button
            className={tab === 'staff' ? 'primary-button' : 'secondary-button'}
            onClick={() => switchTab('staff')}
            type="button"
          >
            Staff ({staffAccounts.length})
          </button>
          <button
            className={tab === 'users' ? 'primary-button' : 'secondary-button'}
            onClick={() => switchTab('users')}
            type="button"
          >
            All Users ({users.length})
          </button>
        </div>

        <div className="admin-row" style={{ marginBottom: 16 }}>
          <input
            aria-label="Search directory"
            maxLength={200}
            type="search"
            placeholder="Search by name, email, phone, type, organization or role…"
            value={searchQuery}
            onChange={(event) => {
              setSearchQuery(event.target.value)
              setPage(1)
            }}
            style={{ flex: 1, minWidth: 240 }}
          />
          {tab === 'users' && (
            <select
              aria-label="Filter account type"
              value={accountFilter}
              onChange={(event) => {
                setAccountFilter(event.target.value as AccountFilter)
                setPage(1)
              }}
            >
              <option value="all">All accounts</option>
              <option value="citizens">Citizens only</option>
              <option value="staff">Staff only</option>
            </select>
          )}
          <select
            aria-label="Sort by"
            value={sortField}
            onChange={(event) => {
              setSortField(event.target.value as SortField)
              setPage(1)
            }}
          >
            {sortOptions}
          </select>
          <button
            className="secondary-button"
            onClick={() => setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc')}
            type="button"
          >
            {sortOrder === 'asc' ? 'Ascending ↑' : 'Descending ↓'}
          </button>
        </div>

        {error && <p className="form-error" role="alert">{error}</p>}
        {notice && <p className="form-success" role="status">{notice}</p>}

        {loading ? (
          <p className="muted empty-state">Loading the platform directory…</p>
        ) : currentRows.length === 0 ? (
          <p className="muted empty-state">
            {query || (tab === 'users' && accountFilter !== 'all')
              ? 'No accounts match the current filters.'
              : tab === 'citizens'
                ? 'No citizen accounts were returned.'
                : tab === 'staff'
                  ? 'No staff accounts were returned.'
                  : 'No user accounts were returned.'}
          </p>
        ) : tab === 'citizens' ? (
          <>
            <div aria-label="Platform citizens" className="table-wrap" role="region" tabIndex={0}>
              <table>
                <thead>
                  <tr>
                    <th>NAME</th>
                    <th>CONTACT</th>
                    <th>ACCOUNT</th>
                    <th>EMAIL</th>
                    <th>NID</th>
                    <th>GENDER</th>
                    <th>AGE</th>
                    <th>CREATED</th>
                    <th>PROFILE UPDATED</th>
                    <th>ACTIONS</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleCitizens.map((citizen) => (
                    <Fragment key={citizen.userId}>
                      <tr>
                        <td><strong>{citizen.fullName}</strong></td>
                        <td>
                          {citizen.email}
                          <span style={{ display: 'block' }}>
                            {citizen.phoneNumber ?? '—'}
                          </span>
                        </td>
                        <td>
                          <span className={citizen.isActive ? 'status-green' : 'status-amber'}>
                            {citizen.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td>
                          <span className={citizen.emailConfirmed ? 'status-green' : 'status-amber'}>
                            {citizen.emailConfirmed ? 'Confirmed' : 'Unconfirmed'}
                          </span>
                        </td>
                        <td>{citizen.nidNumber ?? '—'}</td>
                        <td>{citizen.gender ?? '—'}</td>
                        <td>{citizen.age ?? '—'}</td>
                        <td>{formatDate(citizen.createdAt)}</td>
                        <td>{formatDate(citizen.updatedAt)}</td>
                        <td>
                          <button
                            className="secondary-button"
                            disabled={busy !== null}
                            onClick={() =>
                              setEditingCitizenId(
                                editingCitizenId === citizen.userId
                                  ? null
                                  : citizen.userId,
                              )
                            }
                            type="button"
                          >
                            {editingCitizenId === citizen.userId ? 'Close' : 'Edit'}
                          </button>
                          <button
                            className="secondary-button"
                            disabled={busy !== null}
                            onClick={() => void handleToggleCitizen(citizen)}
                            type="button"
                          >
                            {citizen.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                        </td>
                      </tr>
                      {editingCitizenId === citizen.userId && (
                        <CitizenEditor
                          key={`${citizen.userId}-editor`}
                          citizen={citizen}
                          busy={busy !== null}
                          onCancel={() => setEditingCitizenId(null)}
                          onSave={(request) => handleSaveCitizen(citizen, request)}
                        />
                      )}
                    </Fragment>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        ) : tab === 'staff' ? (
          <div aria-label="Platform staff accounts" className="table-wrap" role="region" tabIndex={0}>
            <table>
              <thead>
                <tr>
                  <th>NAME</th>
                  <th>CONTACT</th>
                  <th>ORGANIZATIONS &amp; ROLES</th>
                  <th>ACCOUNT</th>
                  <th>EMAIL</th>
                  <th>PLATFORM ROLE</th>
                  <th>CREATED</th>
                  <th>ACTIONS</th>
                </tr>
              </thead>
              <tbody>
                {visibleStaff.map((user) => (
                  <Fragment key={user.id}>
                    <tr>
                      <td><strong>{user.fullName}</strong></td>
                      <td>
                        {user.email}
                        <span style={{ display: 'block' }}>
                          {user.phoneNumber ?? '—'}
                        </span>
                      </td>
                      <td>
                        {user.organizations.length === 0 ? (
                          <span className="muted">No memberships</span>
                        ) : (
                          user.organizations.map((organization) => (
                            <button
                              className="link-button"
                              key={organization.membershipId}
                              onClick={() => onOpenOrganization(organization.organizationId)}
                              style={{ display: 'block', padding: 0 }}
                              type="button"
                            >
                              {organization.organizationName} · {organization.role}
                              {organization.isActive ? '' : ' (inactive)'}
                            </button>
                          ))
                        )}
                      </td>
                      <td>
                        <span className={user.isActive ? 'status-green' : 'status-amber'}>
                          {user.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td>
                        <span className={user.emailConfirmed ? 'status-green' : 'status-amber'}>
                          {user.emailConfirmed ? 'Confirmed' : 'Unconfirmed'}
                        </span>
                      </td>
                      <td>{user.platformRole}</td>
                      <td>{formatDate(user.createdAt)}</td>
                      <td>
                        <button
                          className="secondary-button"
                          disabled={busy !== null}
                          onClick={() =>
                            setEditingStaffId(
                              editingStaffId === user.id ? null : user.id,
                            )
                          }
                          type="button"
                        >
                          {editingStaffId === user.id ? 'Close' : 'Edit'}
                        </button>
                        <button
                          className="secondary-button"
                          disabled={busy !== null}
                          onClick={() => void handleToggleUser(user)}
                          type="button"
                        >
                          {user.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </td>
                    </tr>
                    {editingStaffId === user.id && (
                      <StaffEditor
                        key={`${user.id}-editor`}
                        user={user}
                        busy={busy !== null}
                        onCancel={() => setEditingStaffId(null)}
                        onSave={(request) => handleSaveStaff(user, request)}
                      />
                    )}
                  </Fragment>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
          <div aria-label="All platform users" className="table-wrap" role="region" tabIndex={0}>
            <table>
              <thead>
                <tr>
                  <th>NAME</th>
                  <th>CONTACT</th>
                  <th>TYPE</th>
                  <th>ACCOUNT</th>
                  <th>EMAIL</th>
                  <th>PLATFORM ROLE</th>
                  <th>MEMBERSHIPS</th>
                  <th>CREATED</th>
                  <th>ACTIONS</th>
                </tr>
              </thead>
              <tbody>
                {visibleUsers.map((user) => (
                  <Fragment key={user.id}>
                    <tr>
                      <td><strong>{user.fullName}</strong></td>
                      <td>
                        {user.email}
                        <span style={{ display: 'block' }}>
                          {user.phoneNumber ?? '—'}
                        </span>
                      </td>
                      <td>
                        <span className="status-chip">{accountType(user)}</span>
                      </td>
                      <td>
                        <span className={user.isActive ? 'status-green' : 'status-amber'}>
                          {user.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td>
                        <span className={user.emailConfirmed ? 'status-green' : 'status-amber'}>
                          {user.emailConfirmed ? 'Confirmed' : 'Unconfirmed'}
                        </span>
                      </td>
                      <td>
                        <select
                          aria-label={`Platform role for ${user.fullName}`}
                          value={user.platformRole}
                          disabled={busy !== null}
                          onChange={(event) =>
                            void handleChangePlatformRole(user, event.target.value)}
                        >
                          {PLATFORM_ROLES.map((role) => (
                            <option key={role} value={role}>{role}</option>
                          ))}
                        </select>
                      </td>
                      <td>
                        {user.organizations.length === 0 ? (
                          <span className="muted">None</span>
                        ) : (
                          user.organizations.map((organization) => (
                            <button
                              className="link-button"
                              key={organization.membershipId}
                              onClick={() => onOpenOrganization(organization.organizationId)}
                              style={{ display: 'block', padding: 0 }}
                              type="button"
                            >
                              {organization.organizationName} · {organization.role}
                              {organization.isActive ? '' : ' (inactive)'}
                            </button>
                          ))
                        )}
                      </td>
                      <td>{formatDate(user.createdAt)}</td>
                      <td>
                        <button
                          className="secondary-button"
                          disabled={busy !== null}
                          onClick={() => void handleToggleUser(user)}
                          type="button"
                        >
                          {user.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </td>
                    </tr>
                  </Fragment>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <Pagination page={safePage} pageCount={pageCount} onPageChange={setPage} />
        {tab === 'users' && (
          <p className="muted" style={{ marginTop: 8 }}>
            Each identity appears once. Organizations shows every membership
            without creating duplicate account rows.
          </p>
        )}
      </section>
    </>
  )
}
