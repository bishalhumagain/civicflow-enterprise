import { useState } from 'react'
import type { AccountInvitation, StaffMember } from '../domain'
import { InvitationManager } from './InvitationManager'

export function StaffDirectory({
  staff,
  invitations,
  authenticated,
  loading,
  onInvite,
  onResend,
  onRevoke,
  onToggle,
  onUpdatePhone,
}: {
  staff: StaffMember[]
  invitations: AccountInvitation[]
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
            Invite staff to this organization and manage who can work on cases.
            Each invitee receives a single-use link and chooses their own
            password.
          </p>
        </div>
      </div>

      {authenticated ? (
        <InvitationManager
          invitations={invitations}
          loading={loading}
          onInvite={onInvite}
          onResend={onResend}
          onRevoke={onRevoke}
        />
      ) : null}

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
                  {editingPhoneFor === member.id ? (
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
                      {member.phoneNumber ?? 'No phone number'}{' '}
                      <button
                        className="link-button"
                        disabled={loading}
                        onClick={() => startEditing(member)}
                        style={{ display: 'inline', padding: 0 }}
                        type="button"
                      >
                        Edit
                      </button>
                    </span>
                  )}
                  <small>{member.isActive ? 'Active' : 'Inactive'}</small>
                </div>
                <button
                  className="secondary-button"
                  disabled={loading}
                  onClick={() => void onToggle(member)}
                  type="button"
                >
                  {member.isActive ? 'Deactivate' : 'Reactivate'}
                </button>
              </article>
            ))}
          </div>
        )}
      </section>
    </>
  )
}
