import { Fragment, useMemo, useRef, useState } from 'react'
import type {
  AccountInvitation,
  Organization,
  PlatformCitizen,
  PlatformCitizenUpdate,
  PlatformOverview,
  PlatformStaff,
} from '../domain'
import { Icon } from './Icon'
import { InvitationManager } from './InvitationManager'
import { invitationRoles } from './invitation-utils'
import {
  toggleCitizenAccountActive,
  updateCitizenProfile,
} from '../api/citizens-admin'
import {
  createInvitation,
  getInvitations,
  resendInvitation,
  revokeInvitation,
  setMembershipActive,
  transferMembership,
  updateMembershipRole,
} from '../api/organizations'

const PAGE_SIZE = 10
const CITIZEN_ROLES: Array<'Viewer' | 'CaseWorker' | 'OrganizationAdmin'> = [
  'Viewer',
  'CaseWorker',
  'OrganizationAdmin',
]

type CitizenSortField = 'fullName' | 'email' | 'createdAt'
type MemberSortField =
  | 'fullName'
  | 'email'
  | 'organizationName'
  | 'membershipCreatedAt'

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

  return (
    <tr>
      <td colSpan={10}>
        <form
          className="inline-form"
          style={{ flexWrap: 'wrap', gap: 8 }}
          onSubmit={(event) => {
            event.preventDefault()
            void onSave({
              fullName: fullName.trim(),
              phoneNumber: phoneNumber.trim() || null,
              nidNumber: nidNumber.trim() || null,
              citizenshipNumber: citizenshipNumber.trim() || null,
              age: age.trim() ? Number(age) : null,
              gender: gender.trim() || null,
              permanentAddress: permanentAddress.trim() || null,
              temporaryAddress: temporaryAddress.trim() || null,
            })
          }}
        >
          <input
            aria-label="Full name"
            required
            maxLength={150}
            value={fullName}
            onChange={(event) => setFullName(event.target.value)}
          />
          <input
            aria-label="Phone number"
            maxLength={30}
            placeholder="Phone number"
            value={phoneNumber}
            onChange={(event) => setPhoneNumber(event.target.value)}
          />
          <input
            aria-label="NID number"
            maxLength={50}
            placeholder="NID number"
            value={nidNumber}
            onChange={(event) => setNidNumber(event.target.value)}
          />
          <input
            aria-label="Citizenship number"
            maxLength={50}
            placeholder="Citizenship number"
            value={citizenshipNumber}
            onChange={(event) => setCitizenshipNumber(event.target.value)}
          />
          <input
            aria-label="Age"
            type="number"
            min={0}
            max={150}
            placeholder="Age"
            value={age}
            onChange={(event) => setAge(event.target.value)}
          />
          <input
            aria-label="Gender"
            maxLength={20}
            placeholder="Gender"
            value={gender}
            onChange={(event) => setGender(event.target.value)}
          />
          <input
            aria-label="Permanent address"
            maxLength={500}
            placeholder="Permanent address"
            value={permanentAddress}
            onChange={(event) => setPermanentAddress(event.target.value)}
          />
          <input
            aria-label="Temporary address"
            maxLength={500}
            placeholder="Temporary address"
            value={temporaryAddress}
            onChange={(event) => setTemporaryAddress(event.target.value)}
          />
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

/**
 * Platform-wide directory for platform administrators. Citizens and
 * organization members are deliberately kept as separate grids: a person can
 * be a citizen, a member of one or more organizations, or both, and the
 * account identity is never duplicated into a membership record.
 */
export function PlatformDirectory({
  loading,
  overview,
  citizens,
  staff,
  organizations,
  refreshData,
}: {
  loading: boolean
  overview: PlatformOverview | null
  citizens: PlatformCitizen[]
  staff: PlatformStaff[]
  organizations: Organization[]
  refreshData: () => void
}) {
  const [tab, setTab] = useState<'citizens' | 'members'>('citizens')
  const [searchQuery, setSearchQuery] = useState('')
  const [citizenSort, setCitizenSort] = useState<CitizenSortField>('fullName')
  const [memberSort, setMemberSort] = useState<MemberSortField>('fullName')
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc')
  const [citizenPage, setCitizenPage] = useState(1)
  const [memberPage, setMemberPage] = useState(1)
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [editingCitizenId, setEditingCitizenId] = useState<string | null>(null)
  const [invitationOrganizationId, setInvitationOrganizationId] = useState('')
  const [invitations, setInvitations] = useState<AccountInvitation[]>([])
  const [invitationsLoading, setInvitationsLoading] = useState(false)

  const invitationRequest = useRef(0)

  const query = searchQuery.trim().toLowerCase()

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
        if (citizenSort === 'fullName') {
          return a.fullName.localeCompare(b.fullName) * multiplier
        }
        if (citizenSort === 'email') {
          return a.email.localeCompare(b.email) * multiplier
        }
        return (
          (new Date(a.createdAt).getTime() -
            new Date(b.createdAt).getTime()) *
          multiplier
        )
      })
  }, [citizens, query, citizenSort, sortOrder])

  const filteredStaff = useMemo(() => {
    const multiplier = sortOrder === 'asc' ? 1 : -1
    return staff
      .filter(
        (member) =>
          !query ||
          member.fullName.toLowerCase().includes(query) ||
          member.email.toLowerCase().includes(query) ||
          member.organizationName.toLowerCase().includes(query) ||
          member.role.toLowerCase().includes(query),
      )
      .sort((a, b) => {
        if (memberSort === 'fullName') {
          return a.fullName.localeCompare(b.fullName) * multiplier
        }
        if (memberSort === 'email') {
          return a.email.localeCompare(b.email) * multiplier
        }
        if (memberSort === 'organizationName') {
          return (
            a.organizationName.localeCompare(b.organizationName) * multiplier
          )
        }
        return (
          (new Date(a.membershipCreatedAt).getTime() -
            new Date(b.membershipCreatedAt).getTime()) *
          multiplier
        )
      })
  }, [staff, query, memberSort, sortOrder])

  const citizenPageCount = Math.max(
    1,
    Math.ceil(filteredCitizens.length / PAGE_SIZE),
  )
  const memberPageCount = Math.max(
    1,
    Math.ceil(filteredStaff.length / PAGE_SIZE),
  )
  const safeCitizenPage = Math.min(citizenPage, citizenPageCount)
  const safeMemberPage = Math.min(memberPage, memberPageCount)
  const visibleCitizens = filteredCitizens.slice(
    (safeCitizenPage - 1) * PAGE_SIZE,
    safeCitizenPage * PAGE_SIZE,
  )
  const visibleStaff = filteredStaff.slice(
    (safeMemberPage - 1) * PAGE_SIZE,
    safeMemberPage * PAGE_SIZE,
  )

  function resetPaging() {
    setCitizenPage(1)
    setMemberPage(1)
  }

  async function run(
    key: string,
    action: () => Promise<void>,
    successMessage?: string,
  ) {
    setBusy(key)
    setError(null)
    setNotice(null)

    try {
      await action()
      if (successMessage) setNotice(successMessage)
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

  async function handleToggleCitizen(citizen: PlatformCitizen) {
    await run(
      `citizen-${citizen.userId}`,
      async () => {
        await toggleCitizenAccountActive(citizen.userId, !citizen.isActive)
        refreshData()
      },
      citizen.isActive ? 'Citizen account deactivated.' : 'Citizen account activated.',
    )
  }

  async function handleChangeRole(member: PlatformStaff, role: string) {
    await run(
      `member-${member.membershipId}`,
      async () => {
        await updateMembershipRole(member.organizationId, member.membershipId, role)
        refreshData()
      },
      'Membership role updated.',
    )
  }

  async function handleToggleMembership(member: PlatformStaff) {
    await run(
      `member-${member.membershipId}`,
      async () => {
        await setMembershipActive(
          member.organizationId,
          member.membershipId,
          !member.isActive,
        )
        refreshData()
      },
      member.isActive ? 'Membership deactivated.' : 'Membership activated.',
    )
  }

  async function handleTransfer(member: PlatformStaff, destinationId: string) {
    if (!destinationId) return

    const destination = organizations.find((item) => item.id === destinationId)
    if (!destination) return

    const confirmed = window.confirm(
      `Move ${member.fullName} from ${member.organizationName} to ${destination.name}? The current membership will be deactivated and a new membership will be created in the destination organization.`,
    )
    if (!confirmed) return

    await run(
      `transfer-${member.membershipId}`,
      async () => {
        await transferMembership(
          member.organizationId,
          member.membershipId,
          destinationId,
          member.role,
        )
        refreshData()
      },
      `${member.fullName} moved to ${destination.name}.`,
    )
  }

  async function handleInvite(
    fullName: string,
    email: string,
    phoneNumber: string,
    role: string,
  ): Promise<boolean> {
    if (!invitationOrganizationId) return false

    setBusy('invite')
    setError(null)
    setNotice(null)

    try {
      const created = await createInvitation(invitationOrganizationId, {
        fullName,
        email,
        phoneNumber: phoneNumber || null,
        role,
      })
      setInvitations((current) => [created, ...current])
      refreshData()
      setNotice(
        created.deliveryFailed
          ? 'Invitation created, but email delivery failed. Use Resend to retry.'
          : 'Invitation created and sent.',
      )
      return true
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : 'The invitation could not be created.',
      )
      return false
    } finally {
      setBusy(null)
    }
  }

  async function handleResend(invitationId: string) {
    await run(
      `invitation-${invitationId}`,
      async () => {
        const updated = await resendInvitation(
          invitationOrganizationId,
          invitationId,
        )
        setInvitations((current) =>
          current.map((item) => (item.id === updated.id ? updated : item)),
        )
        refreshData()
      },
      'Invitation resent.',
    )
  }

  async function handleRevoke(invitationId: string) {
    await run(
      `invitation-${invitationId}`,
      async () => {
        await revokeInvitation(invitationOrganizationId, invitationId)
        setInvitations((current) =>
          current.filter((item) => item.id !== invitationId),
        )
        refreshData()
      },
      'Invitation revoked.',
    )
  }

  async function manageInvitations(organizationId: string) {
    const requestId = invitationRequest.current + 1
    invitationRequest.current = requestId
    setInvitationOrganizationId(organizationId)
    setInvitations([])
    setNotice(null)
    setError(null)

    if (!organizationId) {
      setInvitationsLoading(false)
      return
    }

    setInvitationsLoading(true)

    try {
      const loaded = await getInvitations(organizationId)
      if (requestId === invitationRequest.current) setInvitations(loaded)
    } catch (reason) {
      if (requestId === invitationRequest.current) {
        setError(
          reason instanceof Error
            ? reason.message
            : 'Invitations could not be loaded.',
        )
      }
    } finally {
      if (requestId === invitationRequest.current) setInvitationsLoading(false)
    }
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PLATFORM DIRECTORY</p>
          <h1>People across the platform</h1>
          <p className="muted">
            Citizen accounts and organization memberships, kept distinct and
            managed from one place.
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
            onClick={() => { setTab('citizens'); resetPaging() }}
            type="button"
          >
            Citizens ({citizens.length})
          </button>
          <button
            className={tab === 'members' ? 'primary-button' : 'secondary-button'}
            onClick={() => { setTab('members'); resetPaging() }}
            type="button"
          >
            Organization Members ({staff.length})
          </button>
        </div>

        <div className="admin-row" style={{ marginBottom: 16 }}>
          <input
            aria-label="Search directory"
            type="search"
            placeholder="Search by name, email, phone, organization or role…"
            value={searchQuery}
            onChange={(event) => {
              setSearchQuery(event.target.value)
              resetPaging()
            }}
            style={{ flex: 1, minWidth: 240 }}
          />
          <select
            aria-label="Sort by"
            value={tab === 'citizens' ? citizenSort : memberSort}
            onChange={(event) => {
              if (tab === 'citizens') {
                setCitizenSort(event.target.value as CitizenSortField)
              } else {
                setMemberSort(event.target.value as MemberSortField)
              }
              resetPaging()
            }}
          >
            <option value="fullName">Name</option>
            <option value="email">Email</option>
            {tab === 'citizens' ? (
              <option value="createdAt">Created</option>
            ) : (
              <>
                <option value="organizationName">Organization</option>
                <option value="membershipCreatedAt">Member since</option>
              </>
            )}
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
        ) : tab === 'citizens' ? (
          filteredCitizens.length === 0 ? (
            <p className="muted empty-state">
              {query
                ? 'No citizens match your search.'
                : 'No citizen accounts were returned.'}
            </p>
          ) : (
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
                          <td>{new Date(citizen.createdAt).toLocaleDateString()}</td>
                          <td>{new Date(citizen.updatedAt).toLocaleDateString()}</td>
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
              <Pagination
                page={safeCitizenPage}
                pageCount={citizenPageCount}
                onPageChange={setCitizenPage}
              />
            </>
          )
        ) : filteredStaff.length === 0 ? (
          <p className="muted empty-state">
            {query
              ? 'No organization members match your search.'
              : 'No organization memberships were returned.'}
          </p>
        ) : (
          <>
            <div aria-label="Platform organization members" className="table-wrap" role="region" tabIndex={0}>
              <table>
                <thead>
                  <tr>
                    <th>NAME</th>
                    <th>CONTACT</th>
                    <th>ORGANIZATION</th>
                    <th>ROLE</th>
                    <th>MEMBERSHIP</th>
                    <th>ACCOUNT</th>
                    <th>MEMBER SINCE</th>
                    <th>ACTIONS</th>
                  </tr>
                </thead>
                <tbody>
                  {visibleStaff.map((member) => (
                    <tr key={member.membershipId}>
                      <td><strong>{member.fullName}</strong></td>
                      <td>
                        {member.email}
                        <span style={{ display: 'block' }}>
                          {member.phoneNumber ?? '—'}
                        </span>
                      </td>
                      <td>
                        <strong>{member.organizationName}</strong>
                        <span style={{ display: 'block' }}>{member.organizationCode}</span>
                      </td>
                      <td>
                        <select
                          aria-label={`Role for ${member.fullName}`}
                          value={member.role}
                          disabled={busy !== null}
                          onChange={(event) =>
                            void handleChangeRole(member, event.target.value)}
                        >
                          {CITIZEN_ROLES.map((role) => (
                            <option key={role} value={role}>{role}</option>
                          ))}
                        </select>
                      </td>
                      <td>
                        <span className={member.isActive ? 'status-green' : 'status-amber'}>
                          {member.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td>
                        <span className={member.accountIsActive ? 'status-green' : 'status-amber'}>
                          {member.accountIsActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td>{new Date(member.membershipCreatedAt).toLocaleDateString()}</td>
                      <td>
                        <button
                          className="secondary-button"
                          disabled={busy !== null}
                          onClick={() => void handleToggleMembership(member)}
                          type="button"
                        >
                          {member.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                        <select
                          aria-label={`Move ${member.fullName} to another organization`}
                          value=""
                          disabled={busy !== null || organizations.length < 2}
                          onChange={(event) =>
                            void handleTransfer(member, event.target.value)}
                        >
                          <option value="">Move to…</option>
                          {organizations
                            .filter(
                              (organization) =>
                                organization.id !== member.organizationId &&
                                organization.isActive,
                            )
                            .map((organization) => (
                              <option key={organization.id} value={organization.id}>
                                {organization.name}
                              </option>
                            ))}
                        </select>
                        <button
                          className="secondary-button"
                          disabled={busy !== null}
                          onClick={() => void manageInvitations(member.organizationId)}
                          type="button"
                        >
                          Invitations
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={safeMemberPage}
              pageCount={memberPageCount}
              onPageChange={setMemberPage}
            />
          </>
        )}
      </section>

      <section className="case-card admin-panel">
        <h2>Member invitations</h2>
        <p className="muted">
          Invitations are scoped to one organization. Inviting a person creates
          a single-use link; access is granted only after the invitation is
          accepted and the membership becomes active.
        </p>
        <div className="admin-row" style={{ marginBottom: 16 }}>
          <select
            aria-label="Invitation organization"
            value={invitationOrganizationId}
            onChange={(event) => void manageInvitations(event.target.value)}
          >
            <option value="">Select an organization…</option>
            {organizations.map((organization) => (
              <option key={organization.id} value={organization.id}>
                {organization.name} ({organization.code})
                {organization.isActive ? '' : ' · inactive'}
              </option>
            ))}
          </select>
        </div>

        {invitationOrganizationId ? (
          <InvitationManager
            invitations={invitations}
            loading={busy !== null || invitationsLoading}
            onInvite={handleInvite}
            onResend={handleResend}
            onRevoke={handleRevoke}
            roleOptions={invitationRoles}
          />
        ) : (
          <p className="muted empty-state">
            Select an organization to view or send invitations.
          </p>
        )}
      </section>
    </>
  )
}
