import { useState } from 'react'
import type { AccountInvitation, Membership, StaffMember } from '../domain'
import { InvitationManager } from './InvitationManager'

export function StaffDirectory({
  staff,
  invitations,
  memberships,
  canManage,
  authenticated,
  loading,
  onInvite,
  onResend,
  onRevoke,
  onToggle,
  onUpdatePhone,
  onUpdateMembership,
  onToggleMembership,
}: {
  staff: StaffMember[]
  invitations: AccountInvitation[]
  memberships: Membership[]
  canManage: boolean
  authenticated: boolean
  loading: boolean
  onInvite: (
    fullName: string,
    email: string,
    phoneNumber: string,
    role: string,
  ) => Promise<boolean>
  onResend: (invitationId: string) => Promise<void>
  onRevoke: (invitationId: string) => Promise<void>
  onToggle: (member: StaffMember) => Promise<void>
  onUpdatePhone: (member: StaffMember, phoneNumber: string) => Promise<void>
  onUpdateMembership: (membership: Membership, role: string) => Promise<void>
  onToggleMembership: (membership: Membership) => Promise<void>
}) {
  const [editingPhoneFor, setEditingPhoneFor] = useState<string | null>(null)
  const [phoneNumber, setPhoneNumber] = useState('')

  function startEditing(member: StaffMember) {
    setEditingPhoneFor(member.id)
    setPhoneNumber(member.phoneNumber ?? '')
  }

  async function savePhone(member: StaffMember) {
    await onUpdatePhone(member, phoneNumber.trim())
    setEditingPhoneFor(null)
    setPhoneNumber('')
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ORGANIZATION DIRECTORY</p>
          <h1>Staff &amp; team</h1>
          <p className="muted">
            {canManage
              ? 'Invite staff to this organization and manage who can work on cases.'
              : 'View the staff directory and case assignment contacts.'}
            {canManage ? ' Each invitee receives a single-use link and chooses their own password.' : ''}
          </p>
        </div>
      </div>

      {authenticated && canManage ? (
        <InvitationManager
          invitations={invitations}
          loading={loading}
          onInvite={onInvite}
          onResend={onResend}
          onRevoke={onRevoke}
        />
      ) : null}

      {canManage && (
        <section className="case-card directory-card">
          <h2>Organization memberships</h2>
          {memberships.length === 0 ? (
            <p className="muted empty-state">No memberships were returned.</p>
          ) : (
            <div className="membership-list">
              {memberships.map((membership) => (
                <article className="membership-row" key={membership.id}>
                  <code title={membership.userId}>{membership.userId}</code>
                  <select
                    aria-label={`Role for ${membership.userId}`}
                    disabled={loading}
                    onChange={(event) =>
                      void onUpdateMembership(membership, event.target.value)}
                    value={membership.role}
                  >
                    <option value="Viewer">Viewer</option>
                    <option value="CaseWorker">CaseWorker</option>
                    <option value="OrganizationAdmin">OrganizationAdmin</option>
                  </select>
                  <span>{membership.isActive ? 'Active' : 'Inactive'}</span>
                  <button
                    className="secondary-button"
                    disabled={loading}
                    onClick={() => void onToggleMembership(membership)}
                    type="button"
                  >
                    {membership.isActive ? 'Deactivate' : 'Reactivate'}
                  </button>
                </article>
              ))}
            </div>
          )}
        </section>
      )}

      <section className="case-card directory-card">
        <h2>Active staff</h2>
        {!authenticated ? (
          <p className="muted empty-state">Sign in with a staff identity to load the organization directory.</p>
        ) : staff.length === 0 ? (
          <p className="muted empty-state">No staff members were returned, or this role cannot access staff administration.</p>
        ) : (
          <div className="directory-grid">
            {staff.map((member) => (
              <article className="directory-member" key={member.id}>
                <span className="avatar">{member.fullName.slice(0, 2).toUpperCase()}</span>
                <div>
                  <strong>{member.fullName}</strong>
                  <span>{member.email}</span>
                  {canManage && editingPhoneFor === member.id ? (
                    <span style={{ display: 'flex', gap: 6, marginTop: 6 }}>
                      <input
                        aria-label={`Phone number for ${member.fullName}`}
                        autoFocus
                        type="tel"
                        maxLength={30}
                        placeholder="Phone number"
                        value={phoneNumber}
                        onChange={(event) => setPhoneNumber(event.target.value)}
                      />
                      <button
                        className="primary-button"
                        disabled={loading}
                        onClick={() => void savePhone(member)}
                        type="button"
                      >
                        Save
                      </button>
                      <button
                        className="secondary-button"
                        disabled={loading}
                        onClick={() => setEditingPhoneFor(null)}
                        type="button"
                      >
                        Cancel
                      </button>
                    </span>
                  ) : (
                    <span>
                      {canManage
                        ? member.phoneNumber ?? 'No phone number'
                        : 'Contact details restricted'}{' '}
                      {canManage && <button
                        className="link-button"
                        disabled={loading}
                        onClick={() => startEditing(member)}
                        style={{ display: 'inline', padding: 0 }}
                        type="button"
                      >
                        Edit
                      </button>}
                    </span>
                  )}
                  <small>{member.isActive ? 'Active' : 'Inactive'}</small>
                </div>
                {canManage && (
                  <button
                    className="secondary-button"
                    disabled={loading}
                    onClick={() => void onToggle(member)}
                    type="button"
                  >
                    {member.isActive ? 'Deactivate' : 'Reactivate'}
                  </button>
                )}
              </article>
            ))}
          </div>
        )}
      </section>
    </>
  )
}
