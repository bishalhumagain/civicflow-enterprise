import { useState } from 'react'
import type { AccountInvitation, Membership, Organization } from '../domain'
import { InvitationManager } from './InvitationManager'
import { Icon } from './Icon'
import { createOrganization, updateOrganization, activateOrganization, deactivateOrganization, deleteOrganization } from '../api/organizations'

export function OrganizationAdministration({
  authenticated,
  loading,
  organizations,
  memberships,
  invitations,
  selectedOrganizationId,
  canManageOrganizations,
  onSelectOrganization,
  refreshData,
  onUpdateMembership,
  onInvite,
  onResendInvitation,
  onRevokeInvitation,
}: {
  authenticated: boolean
  loading: boolean
  organizations: Organization[]
  memberships: Membership[]
  invitations: AccountInvitation[]
  selectedOrganizationId: string
  canManageOrganizations: boolean
  onSelectOrganization: (id: string) => void
  refreshData: () => void
  onUpdateMembership: (membership: Membership, action: 'role' | 'active', role?: string) => Promise<void>
  onInvite: (
    fullName: string,
    email: string,
    phoneNumber: string,
    role: string,
  ) => Promise<boolean>
  onResendInvitation: (invitationId: string) => Promise<void>
  onRevokeInvitation: (invitationId: string) => Promise<void>
}) {
  const [editingOrg, setEditingOrg] = useState<string | null>(null)
  const [editedName, setEditedName] = useState('')
  const [editedCode, setEditedCode] = useState('')
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [newOrgName, setNewOrgName] = useState('')
  const [newOrgCode, setNewOrgCode] = useState('')
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const selected = organizations.find((item) => item.id === selectedOrganizationId)

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
          : 'The organization operation could not be completed.',
      )
    } finally {
      setBusy(null)
    }
  }

  async function handleCreateOrganization(e: React.FormEvent) {
    e.preventDefault()
    await run(
      'create',
      async () => {
        await createOrganization(newOrgName.trim(), newOrgCode.trim())
        setNewOrgName('')
        setNewOrgCode('')
        setShowCreateForm(false)
        refreshData()
      },
      'Organization created.',
    )
  }

  async function handleUpdateOrganization(org: Organization) {
    await run(
      `update-${org.id}`,
      async () => {
        await updateOrganization(org.id, editedName.trim(), editedCode.trim())
        setEditingOrg(null)
        refreshData()
      },
      'Organization updated.',
    )
  }

  async function handleToggleOrganization(org: Organization) {
    await run(
      `toggle-${org.id}`,
      async () => {
        if (org.isActive) {
          await deactivateOrganization(org.id)
        } else {
          await activateOrganization(org.id)
        }
        refreshData()
      },
      org.isActive ? 'Organization deactivated.' : 'Organization activated.',
    )
  }

  async function handleDeleteOrganization(org: Organization) {
    if (!confirm(`Are you sure you want to delete ${org.name}? This action cannot be undone.`)) {
      return
    }

    await run(
      `delete-${org.id}`,
      async () => {
        await deleteOrganization(org.id)
        refreshData()
      },
      'Organization deleted.',
    )
  }

  function startEditing(org: Organization) {
    setEditingOrg(org.id)
    setEditedName(org.name)
    setEditedCode(org.code)
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ORGANIZATION ADMINISTRATION</p>
          <h1>Organizations</h1>
          <p className="muted">
            {canManageOrganizations
              ? 'Manage organizations and their members.'
              : 'Manage members and invitations for the organizations you administer.'}
          </p>
        </div>
      </div>

      {!authenticated ? (
        <section className="case-card empty-state">
          <p className="muted">Sign in to access authorized administration features.</p>
        </section>
      ) : (
        <>
          <section className="case-card admin-panel">
            {canManageOrganizations && (
              <div className="admin-row" style={{ marginBottom: 16 }}>
                <button
                  className="primary-button"
                  disabled={loading || busy !== null}
                  onClick={() => setShowCreateForm(!showCreateForm)}
                  type="button"
                >
                  {showCreateForm
                    ? 'Cancel'
                    : <><Icon className="button-icon" name="plus" /><span>Create organization</span></>}
                </button>
              </div>
            )}

            {canManageOrganizations && showCreateForm && (
              <form
                className="inline-form"
                onSubmit={handleCreateOrganization}
                style={{ marginBottom: 24 }}
              >
                <input
                  required
                  maxLength={200}
                  placeholder="Organization name"
                  value={newOrgName}
                  onChange={(e) => setNewOrgName(e.target.value)}
                />
                <input
                  required
                  maxLength={50}
                  placeholder="Code"
                  value={newOrgCode}
                  onChange={(e) => setNewOrgCode(e.target.value)}
                />
                <button className="primary-button" disabled={loading || busy !== null} type="submit">
                  <Icon className="button-icon" name="plus" />
                  <span>Create</span>
                </button>
              </form>
            )}

            {error && <p className="form-error" role="alert">{error}</p>}
            {notice && <p className="form-success" role="status">{notice}</p>}

            {organizations.length === 0 ? (
              <p className="muted empty-state">No organizations found.</p>
            ) : (
              <div aria-label="Organizations" className="table-wrap" role="region" tabIndex={0}>
                <table>
                  <thead>
                    <tr>
                      <th>NAME</th>
                      <th>CODE</th>
                      <th>STATUS</th>
                      <th>CREATED</th>
                      <th>ACTIONS</th>
                    </tr>
                  </thead>
                  <tbody>
                    {organizations.map((org) => (
                      <tr key={org.id}>
                        {editingOrg === org.id ? (
                          <>
                            <td>
                              <input
                                required
                                maxLength={200}
                                value={editedName}
                                onChange={(e) => setEditedName(e.target.value)}
                              />
                            </td>
                            <td>
                              <input
                                required
                                maxLength={50}
                                value={editedCode}
                                onChange={(e) => setEditedCode(e.target.value)}
                              />
                            </td>
                            <td>{org.isActive ? 'Active' : 'Inactive'}</td>
                            <td>{new Date(org.createdAt).toLocaleDateString()}</td>
                            <td>
                              <button
                                className="primary-button"
                                disabled={busy !== null}
                                onClick={() => void handleUpdateOrganization(org)}
                                type="button"
                              >
                                <Icon className="button-icon" name="save" />
                                <span>Save</span>
                              </button>
                              <button
                                className="secondary-button"
                                disabled={busy !== null}
                                onClick={() => setEditingOrg(null)}
                                type="button"
                              >
                                Cancel
                              </button>
                            </td>
                          </>
                        ) : (
                          <>
                            <td>
                              <strong>{org.name}</strong>
                            </td>
                            <td>
                              <code>{org.code}</code>
                            </td>
                            <td>
                              <span className={org.isActive ? 'status-green' : 'status-amber'}>
                                {org.isActive ? 'Active' : 'Inactive'}
                              </span>
                            </td>
                            <td>{new Date(org.createdAt).toLocaleDateString()}</td>
                            <td>
                              <button
                                className="secondary-button"
                                disabled={busy !== null}
                                onClick={() => onSelectOrganization(org.id)}
                                type="button"
                              >
                                <Icon className="button-icon" name="eye" />
                                <span>View members</span>
                              </button>
                              {canManageOrganizations && (
                                <>
                                  <button
                                    className="secondary-button"
                                    disabled={busy !== null}
                                    onClick={() => startEditing(org)}
                                    type="button"
                                  >
                                    <Icon className="button-icon" name="edit" />
                                    <span>Edit</span>
                                  </button>
                                  <button
                                    className="secondary-button"
                                    disabled={busy !== null}
                                    onClick={() => void handleToggleOrganization(org)}
                                    type="button"
                                  >
                                    {org.isActive ? 'Deactivate' : 'Activate'}
                                  </button>
                                  <button
                                    className="secondary-button danger"
                                    disabled={busy !== null}
                                    onClick={() => void handleDeleteOrganization(org)}
                                    type="button"
                                  >
                                    <Icon className="button-icon" name="trash" />
                                    <span>Delete</span>
                                  </button>
                                </>
                              )}
                            </td>
                          </>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {selectedOrganizationId && (
            <>
              <section className="case-card admin-panel">
                <h2>Memberships for {selected?.name}</h2>

                {memberships.length === 0 ? (
                  <p className="muted empty-state">No memberships found for this organization.</p>
                ) : (
                  <div aria-label="Organization memberships" className="table-wrap" role="region" tabIndex={0}>
                    <table>
                      <thead>
                        <tr>
                          <th>USER ID</th>
                          <th>ROLE</th>
                          <th>STATUS</th>
                          <th>CREATED</th>
                          <th>ACTIONS</th>
                        </tr>
                      </thead>
                      <tbody>
                        {memberships.map((membership) => (
                          <tr key={membership.id}>
                            <td>
                              <code title={membership.userId}>{membership.userId.slice(0, 8)}...</code>
                            </td>
                            <td>
                              <select
                                value={membership.role}
                                disabled={busy !== null || loading}
                                onChange={(e) => void onUpdateMembership(membership, 'role', e.target.value)}
                              >
                                <option value="Viewer">Viewer</option>
                                <option value="CaseWorker">CaseWorker</option>
                                <option value="OrganizationAdmin">OrganizationAdmin</option>
                              </select>
                            </td>
                            <td>
                              <span className={membership.isActive ? 'status-green' : 'status-amber'}>
                                {membership.isActive ? 'Active' : 'Inactive'}
                              </span>
                            </td>
                            <td>{new Date(membership.createdAt).toLocaleDateString()}</td>
                            <td>
                              <button
                                className="secondary-button"
                                disabled={busy !== null || loading}
                                onClick={() => void onUpdateMembership(membership, 'active')}
                                type="button"
                              >
                                {membership.isActive ? 'Deactivate' : 'Activate'}
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>

              <InvitationManager
                disabled={!selectedOrganizationId}
                invitations={invitations}
                loading={loading || busy !== null}
                onInvite={onInvite}
                onResend={onResendInvitation}
                onRevoke={onRevokeInvitation}
              />
            </>
          )}
        </>
      )}
    </>
  )
}
