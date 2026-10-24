import { useState } from 'react'
import type { StaffMember } from '../domain'
import { Icon } from './Icon'

/**
 * The organization's team contact list. Membership roles, activation, and
 * invitations are managed in the Organizations administration surface so
 * there is a single workflow for each operation.
 */
export function StaffDirectory({
  staff,
  canManage,
  authenticated,
  loading,
  onToggle,
  onUpdatePhone,
}: {
  staff: StaffMember[]
  canManage: boolean
  authenticated: boolean
  loading: boolean
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
          <h1>Team contacts</h1>
          <p className="muted">
            {canManage
              ? 'Contact details for the people who work on this organization’s cases. Membership roles and invitations are managed from Organizations.'
              : 'View the staff directory and case assignment contacts.'}
          </p>
        </div>
      </div>

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
                        <Icon className="button-icon" name="save" />
                        <span>Save</span>
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
                      {canManage && (
                        <button
                          className="link-button"
                          disabled={loading}
                          onClick={() => startEditing(member)}
                          style={{ display: 'inline', padding: 0 }}
                          type="button"
                        >
                          <Icon className="button-icon" name="edit" />
                          <span>Edit</span>
                        </button>
                      )}
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
