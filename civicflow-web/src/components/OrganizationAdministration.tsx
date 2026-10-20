import { useState } from 'react'
import type { Membership, Organization } from '../domain'

export function OrganizationAdministration({
  authenticated,
  loading,
  organizations,
  memberships,
  selectedOrganizationId,
  onSelectOrganization,
  onCreateOrganization,
  onToggleOrganization,
  onCreateMembership,
  onUpdateMembership,
}: {
  authenticated: boolean
  loading: boolean
  organizations: Organization[]
  memberships: Membership[]
  selectedOrganizationId: string
  onSelectOrganization: (id: string) => void
  onCreateOrganization: (name: string, code: string) => Promise<void>
  onToggleOrganization: (organization: Organization) => Promise<void>
  onCreateMembership: (userId: string, role: string) => Promise<void>
  onUpdateMembership: (membership: Membership, action: 'role' | 'active', role?: string) => Promise<void>
}) {
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [userId, setUserId] = useState('')
  const [role, setRole] = useState('Viewer')

  const selected = organizations.find((item) => item.id === selectedOrganizationId)

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ACCESS ADMINISTRATION</p>
          <h1>Organizations &amp; memberships</h1>
          <p className="muted">Platform administrators can manage organizations; organization administrators can manage memberships in their own organization.</p>
        </div>
      </div>

      {!authenticated ? (
        <section className="case-card empty-state">
          <p className="muted">Sign in to access authorized administration features.</p>
        </section>
      ) : (
        <>
          <section className="case-card admin-panel">
            <h2>Organizations</h2>

            {organizations.length === 0 ? (
              <p className="muted">No platform organizations loaded. If you are an OrganizationAdmin, select the configured organization below to manage its memberships.</p>
            ) : (
              <div className="admin-row">
                <select
                  aria-label="Selected organization"
                  value={selectedOrganizationId}
                  onChange={(event) => onSelectOrganization(event.target.value)}
                >
                  {organizations.map((item) => (
                    <option key={item.id} value={item.id}>{item.name} ({item.code})</option>
                  ))}
                </select>
                {selected && (
                  <button
                    className="secondary-button"
                    disabled={loading}
                    onClick={() => void onToggleOrganization(selected)}
                    type="button"
                  >
                    {selected.isActive ? 'Deactivate' : 'Reactivate'}
                  </button>
                )}
              </div>
            )}

            <form
              className="inline-form"
              onSubmit={(event) => {
                event.preventDefault()
                void onCreateOrganization(name, code)
                setName('')
                setCode('')
              }}
            >
              <input required placeholder="Organization name" value={name} onChange={(event) => setName(event.target.value)} />
              <input required placeholder="Code" value={code} onChange={(event) => setCode(event.target.value)} />
              <button className="primary-button" disabled={loading} type="submit">Create organization</button>
            </form>
          </section>

          <section className="case-card admin-panel">
            <h2>Memberships {selectedOrganizationId && <small>{selectedOrganizationId}</small>}</h2>

            <form
              className="inline-form"
              onSubmit={(event) => {
                event.preventDefault()
                void onCreateMembership(userId, role)
                setUserId('')
              }}
            >
              <input
                required
                placeholder="Application user ID"
                value={userId}
                onChange={(event) => setUserId(event.target.value)}
              />
              <select value={role} onChange={(event) => setRole(event.target.value)}>
                <option>Viewer</option>
                <option>CaseWorker</option>
                <option>OrganizationAdmin</option>
              </select>
              <button className="primary-button" disabled={loading || !selectedOrganizationId} type="submit">
                Add membership
              </button>
            </form>

            {memberships.length === 0 ? (
              <p className="muted empty-state">No memberships returned, or this identity is not authorized for the selected organization.</p>
            ) : (
              <div className="membership-list">
                {memberships.map((membership) => (
                  <div className="membership-row" key={membership.id}>
                    <code>{membership.userId}</code>
                    <select
                      value={membership.role}
                      disabled={loading}
                      onChange={(event) => void onUpdateMembership(membership, 'role', event.target.value)}
                    >
                      <option>Viewer</option>
                      <option>CaseWorker</option>
                      <option>OrganizationAdmin</option>
                    </select>
                    <span>{membership.isActive ? 'Active' : 'Inactive'}</span>
                    <button
                      className="secondary-button"
                      disabled={loading}
                      onClick={() => void onUpdateMembership(membership, 'active')}
                      type="button"
                    >
                      {membership.isActive ? 'Deactivate' : 'Reactivate'}
                    </button>
                  </div>
                ))}
              </div>
            )}
          </section>
        </>
      )}
    </>
  )
}
