import { useState } from 'react'
import type { StaffMember } from '../domain'

export function StaffDirectory({
  staff,
  authenticated,
  loading,
  onCreate,
  onToggle,
}: {
  staff: StaffMember[]
  authenticated: boolean
  loading: boolean
  onCreate: (fullName: string, email: string) => Promise<void>
  onToggle: (member: StaffMember) => Promise<void>
}) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ORGANIZATION DIRECTORY</p>
          <h1>Staff &amp; team</h1>
          <p className="muted">Active staff available for case assignment in the selected organization.</p>
        </div>
      </div>

      {authenticated && (
        <form
          className="case-card inline-form"
          onSubmit={(event) => {
            event.preventDefault()
            void onCreate(fullName, email)
            setFullName('')
            setEmail('')
          }}
        >
          <input
            required
            minLength={1}
            maxLength={150}
            placeholder="Full name"
            value={fullName}
            onChange={(event) => setFullName(event.target.value)}
          />
          <input
            required
            type="email"
            maxLength={254}
            placeholder="Email address"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
          <button className="primary-button" disabled={loading} type="submit">Add staff</button>
        </form>
      )}

      <section className="case-card directory-card">
        {!authenticated ? (
          <p className="muted empty-state">Sign in with a staff identity to load the organization directory.</p>
        ) : staff.length === 0 ? (
          <p className="muted empty-state">No active staff members were returned, or this role cannot access staff administration.</p>
        ) : (
          <div className="directory-grid">
            {staff.map((member) => (
              <article className="directory-member" key={member.id}>
                <span className="avatar">{member.fullName.slice(0, 2).toUpperCase()}</span>
                <div>
                  <strong>{member.fullName}</strong>
                  <span>{member.email}</span>
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
