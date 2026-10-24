import { useCallback, useEffect, useState } from 'react'
import type { AccountInvitation, Membership, Organization, OrganizationType } from '../domain'
import { InvitationManager } from './InvitationManager'
import { Icon } from './Icon'
import {
  activateOrganization,
  createOrganization,
  deactivateOrganization,
  deleteOrganization,
  updateOrganization,
} from '../api/organizations'
import {
  createOrganizationType,
  deleteOrganizationType,
  getOrganizationTypes,
  setOrganizationTypeActive,
  updateOrganizationType,
} from '../api/organization-types'

const UNCATEGORIZED_ID = '00000000-0000-0000-0000-000000000001'

type AdminTab = 'organizations' | 'types' | 'members'

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
  // ── Organizations tab state ────────────────────────────────────────────
  const [editingOrg, setEditingOrg] = useState<string | null>(null)
  const [editedName, setEditedName] = useState('')
  const [editedCode, setEditedCode] = useState('')
  const [editedTypeId, setEditedTypeId] = useState('')
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [newOrgName, setNewOrgName] = useState('')
  const [newOrgCode, setNewOrgCode] = useState('')
  const [newOrgTypeId, setNewOrgTypeId] = useState('')

  // ── Shared organization-type catalogue (used by orgs tab + types tab) ──
  const [organizationTypes, setOrganizationTypes] = useState<OrganizationType[]>([])
  const [typesLoading, setTypesLoading] = useState(canManageOrganizations)

  // ── Organization types tab state ───────────────────────────────────────
  const [showTypeForm, setShowTypeForm] = useState(false)
  const [newTypeName, setNewTypeName] = useState('')
  const [newTypeCode, setNewTypeCode] = useState('')
  const [editingTypeId, setEditingTypeId] = useState<string | null>(null)
  const [editedTypeName, setEditedTypeName] = useState('')
  const [editedTypeCode, setEditedTypeCode] = useState('')

  // ── Members & invitations tab state ────────────────────────────────────
  const [filterTypeId, setFilterTypeId] = useState('')
  const [filterOrgId, setFilterOrgId] = useState('')

  const defaultTab: AdminTab = canManageOrganizations ? 'organizations' : 'members'
  const [tab, setTab] = useState<AdminTab>(defaultTab)

  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const loadTypes = useCallback(async () => {
    setTypesLoading(true)
    try {
      const items = await getOrganizationTypes()
      setOrganizationTypes(items)
      setNewOrgTypeId((current) => current || items[0]?.id || '')
    } catch {
      setOrganizationTypes([])
    } finally {
      setTypesLoading(false)
    }
  }, [])

  useEffect(() => {
    if (!canManageOrganizations) return

    const timer = window.setTimeout(() => void loadTypes(), 0)
    return () => window.clearTimeout(timer)
  }, [canManageOrganizations, loadTypes])

  const selected = organizations.find((item) => item.id === selectedOrganizationId)

  const filteredOrganizations = filterTypeId
    ? organizations.filter((org) => org.organizationTypeId === filterTypeId)
    : organizations

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

  // ── Organizations handlers ─────────────────────────────────────────────
  async function handleCreateOrganization(e: React.FormEvent) {
    e.preventDefault()
    await run(
      'create',
      async () => {
        await createOrganization(
          newOrgName.trim(),
          newOrgCode.trim(),
          newOrgTypeId || undefined,
        )
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
        await updateOrganization(
          org.id,
          editedName.trim(),
          editedCode.trim(),
          editedTypeId || undefined,
        )
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
    setEditedTypeId(org.organizationTypeId)
  }

  function viewMembers(orgId: string) {
    onSelectOrganization(orgId)
    setFilterTypeId('')
    setFilterOrgId(orgId)
    if (canManageOrganizations) setTab('members')
  }

  // ── Organization type handlers ─────────────────────────────────────────
  async function handleCreateType(e: React.FormEvent) {
    e.preventDefault()
    await run(
      'type-create',
      async () => {
        await createOrganizationType(newTypeName.trim(), newTypeCode.trim())
        setNewTypeName('')
        setNewTypeCode('')
        setShowTypeForm(false)
        await loadTypes()
      },
      'Organization type created.',
    )
  }

  function startEditingType(item: OrganizationType) {
    setEditingTypeId(item.id)
    setEditedTypeName(item.name)
    setEditedTypeCode(item.code)
    setError(null)
    setNotice(null)
  }

  async function handleUpdateType(item: OrganizationType) {
    await run(
      `type-update-${item.id}`,
      async () => {
        await updateOrganizationType(
          item.id,
          editedTypeName.trim(),
          editedTypeCode.trim(),
        )
        setEditingTypeId(null)
        await loadTypes()
      },
      'Organization type updated.',
    )
  }

  async function handleToggleType(item: OrganizationType) {
    await run(
      `type-toggle-${item.id}`,
      async () => {
        await setOrganizationTypeActive(item.id, !item.isActive)
        await loadTypes()
      },
      item.isActive
        ? 'Organization type deactivated.'
        : 'Organization type activated.',
    )
  }

  async function handleDeleteType(item: OrganizationType) {
    if (
      !confirm(
        `Delete ${item.name}? This cannot be undone. Only unused types can be deleted.`,
      )
    ) {
      return
    }

    await run(
      `type-delete-${item.id}`,
      async () => {
        await deleteOrganizationType(item.id)
        await loadTypes()
      },
      'Organization type deleted.',
    )
  }

  // ── Members & invitations filter handlers ──────────────────────────────
  function applyMembersFilter() {
    if (filterOrgId) {
      onSelectOrganization(filterOrgId)
    } else {
      onSelectOrganization('')
    }
  }

  function resetMembersFilter() {
    setFilterTypeId('')
    setFilterOrgId('')
    onSelectOrganization('')
  }

  // ── Tab strip ──────────────────────────────────────────────────────────
  function renderTabs() {
    if (!canManageOrganizations) return null

    const tabs: { id: AdminTab; label: string; count?: number }[] = [
      { id: 'organizations', label: 'Organizations', count: organizations.length },
      { id: 'types', label: 'Organization types', count: organizationTypes.length },
      { id: 'members', label: 'Members & invitations' },
    ]

    return (
      <div className="admin-row" style={{ marginBottom: 16 }}>
        {tabs.map((entry) => (
          <button
            className={tab === entry.id ? 'primary-button' : 'secondary-button'}
            disabled={busy !== null}
            key={entry.id}
            onClick={() => setTab(entry.id)}
            type="button"
          >
            {entry.label}
            {typeof entry.count === 'number' ? ` (${entry.count})` : ''}
          </button>
        ))}
      </div>
    )
  }

  // ── Organizations tab body ─────────────────────────────────────────────
  function renderOrganizationsBody() {
    return (
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
            {organizationTypes.length > 0 && (
              <select
                aria-label="Organization type"
                value={newOrgTypeId}
                onChange={(e) => setNewOrgTypeId(e.target.value)}
              >
                {organizationTypes.map((type) => (
                  <option key={type.id} value={type.id}>
                    {type.name}
                  </option>
                ))}
              </select>
            )}
            <button className="primary-button" disabled={loading || busy !== null} type="submit">
              <Icon className="button-icon" name="plus" />
              <span>Create</span>
            </button>
          </form>
        )}

        {organizations.length === 0 ? (
          <p className="muted empty-state">No organizations found.</p>
        ) : (
          <div aria-label="Organizations" className="table-wrap" role="region" tabIndex={0}>
            <table>
              <thead>
                <tr>
                  <th>NAME</th>
                  <th>CODE</th>
                  <th>TYPE</th>
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
                        <td>
                          {organizationTypes.length > 0 ? (
                            <select
                              aria-label="Organization type"
                              value={editedTypeId}
                              onChange={(e) => setEditedTypeId(e.target.value)}
                            >
                              {organizationTypes.map((type) => (
                                <option key={type.id} value={type.id}>
                                  {type.name}
                                </option>
                              ))}
                            </select>
                          ) : (
                            <span className="muted">
                              {org.organizationTypeName ?? '—'}
                            </span>
                          )}
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
                          {org.organizationTypeName ?? <span className="muted">—</span>}
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
                            onClick={() => viewMembers(org.id)}
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
    )
  }

  // ── Organization types tab body ────────────────────────────────────────
  function renderTypesBody() {
    return (
      <section className="case-card admin-panel">
        <p className="muted" style={{ marginTop: 0 }}>
          The shared catalogue of organization types. Every organization must
          be assigned one; the default Uncategorized type is used whenever a
          type is not chosen and cannot be deleted or deactivated.
        </p>

        <div className="admin-row" style={{ marginBottom: 16 }}>
          <button
            className="primary-button"
            disabled={typesLoading || busy !== null}
            onClick={() => setShowTypeForm(!showTypeForm)}
            type="button"
          >
            {showTypeForm
              ? 'Cancel'
              : (
                <>
                  <Icon className="button-icon" name="plus" />
                  <span>Create organization type</span>
                </>
              )}
          </button>
        </div>

        {showTypeForm && (
          <form
            className="inline-form"
            onSubmit={handleCreateType}
            style={{ marginBottom: 24 }}
          >
            <input
              required
              maxLength={200}
              placeholder="Type name"
              value={newTypeName}
              onChange={(e) => setNewTypeName(e.target.value)}
            />
            <input
              required
              maxLength={50}
              placeholder="Code"
              value={newTypeCode}
              onChange={(e) => setNewTypeCode(e.target.value)}
            />
            <button
              className="primary-button"
              disabled={typesLoading || busy !== null}
              type="submit"
            >
              <Icon className="button-icon" name="plus" />
              <span>Create</span>
            </button>
          </form>
        )}

        {typesLoading ? (
          <p className="muted empty-state">Loading organization types…</p>
        ) : organizationTypes.length === 0 ? (
          <p className="muted empty-state">No organization types found.</p>
        ) : (
          <div
            aria-label="Organization types"
            className="table-wrap"
            role="region"
            tabIndex={0}
          >
            <table>
              <thead>
                <tr>
                  <th>NAME</th>
                  <th>CODE</th>
                  <th>ORGANIZATIONS</th>
                  <th>STATUS</th>
                  <th>CREATED</th>
                  <th>ACTIONS</th>
                </tr>
              </thead>
              <tbody>
                {organizationTypes.map((item) => {
                  const isUncategorized = item.id === UNCATEGORIZED_ID

                  return (
                    <tr key={item.id}>
                      {editingTypeId === item.id ? (
                        <>
                          <td>
                            <input
                              required
                              maxLength={200}
                              value={editedTypeName}
                              onChange={(e) => setEditedTypeName(e.target.value)}
                            />
                          </td>
                          <td>
                            <input
                              required
                              maxLength={50}
                              value={editedTypeCode}
                              onChange={(e) => setEditedTypeCode(e.target.value)}
                            />
                          </td>
                          <td>{item.organizationCount}</td>
                          <td>{item.isActive ? 'Active' : 'Inactive'}</td>
                          <td>{new Date(item.createdAt).toLocaleDateString()}</td>
                          <td>
                            <button
                              className="primary-button"
                              disabled={busy !== null}
                              onClick={() => void handleUpdateType(item)}
                              type="button"
                            >
                              <Icon className="button-icon" name="save" />
                              <span>Save</span>
                            </button>
                            <button
                              className="secondary-button"
                              disabled={busy !== null}
                              onClick={() => setEditingTypeId(null)}
                              type="button"
                            >
                              Cancel
                            </button>
                          </td>
                        </>
                      ) : (
                        <>
                          <td>
                            <strong>{item.name}</strong>
                            {isUncategorized && (
                              <span
                                className="muted"
                                style={{ marginLeft: 8, fontSize: 12 }}
                              >
                                (default)
                              </span>
                            )}
                          </td>
                          <td>
                            <code>{item.code}</code>
                          </td>
                          <td>{item.organizationCount}</td>
                          <td>
                            <span
                              className={item.isActive ? 'status-green' : 'status-amber'}
                            >
                              {item.isActive ? 'Active' : 'Inactive'}
                            </span>
                          </td>
                          <td>{new Date(item.createdAt).toLocaleDateString()}</td>
                          <td>
                            <button
                              className="secondary-button"
                              disabled={busy !== null}
                              onClick={() => startEditingType(item)}
                              type="button"
                            >
                              <Icon className="button-icon" name="edit" />
                              <span>Edit</span>
                            </button>
                            {!isUncategorized && (
                              <button
                                className="secondary-button"
                                disabled={busy !== null}
                                onClick={() => void handleToggleType(item)}
                                type="button"
                              >
                                {item.isActive ? 'Deactivate' : 'Activate'}
                              </button>
                            )}
                            {!isUncategorized && (
                              <button
                                className="secondary-button danger"
                                disabled={busy !== null}
                                onClick={() => void handleDeleteType(item)}
                                type="button"
                              >
                                <Icon className="button-icon" name="trash" />
                                <span>Delete</span>
                              </button>
                            )}
                          </td>
                        </>
                      )}
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    )
  }

  // ── Members & invitations tab body ─────────────────────────────────────
  function renderMembersBody() {
    return (
      <>
        {canManageOrganizations && (
          <section className="case-card admin-panel">
            <div className="list-toolbar">
              <div>
                <h2>Filter</h2>
                <p className="muted">
                  Narrow the view by organization type and pick an
                  organization to see its members and invitations.
                </p>
              </div>
            </div>
            <form
              className="inline-form"
              onSubmit={(event) => {
                event.preventDefault()
                applyMembersFilter()
              }}
              style={{ padding: '0 18px 18px' }}
            >
              <select
                aria-label="Filter by organization type"
                value={filterTypeId}
                onChange={(event) => {
                  const nextTypeId = event.target.value
                  setFilterTypeId(nextTypeId)

                  if (filterOrgId) {
                    const stillValid = organizations.some((org) =>
                      org.id === filterOrgId &&
                      (nextTypeId === '' || org.organizationTypeId === nextTypeId),
                    )
                    if (!stillValid) setFilterOrgId('')
                  }
                }}
              >
                <option value="">All types</option>
                {organizationTypes.map((type) => (
                  <option key={type.id} value={type.id}>
                    {type.name}
                  </option>
                ))}
              </select>
              <select
                aria-label="Select organization"
                value={filterOrgId}
                onChange={(event) => setFilterOrgId(event.target.value)}
              >
                <option value="">Select an organization…</option>
                {filteredOrganizations.map((org) => (
                  <option key={org.id} value={org.id}>
                    {org.name} ({org.code})
                  </option>
                ))}
              </select>
              <button
                className="primary-button"
                disabled={busy !== null || !filterOrgId}
                type="submit"
              >
                <Icon className="button-icon" name="search" />
                <span>Apply</span>
              </button>
              <button
                className="secondary-button"
                disabled={busy !== null || (!selectedOrganizationId && !filterTypeId && !filterOrgId)}
                onClick={resetMembersFilter}
                type="button"
              >
                Reset
              </button>
            </form>
          </section>
        )}

        {!selectedOrganizationId ? (
          <section className="case-card admin-panel">
            <p className="muted empty-state">
              {canManageOrganizations
                ? 'Select an organization above to view its members and invitations.'
                : 'Pick an organization from the list to view its members and invitations.'}
            </p>
          </section>
        ) : (
          <>
            <section className="case-card admin-panel">
              <h2>Memberships for {selected?.name ?? 'selected organization'}</h2>

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
    )
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ORGANIZATION ADMINISTRATION</p>
          <h1>Organizations</h1>
          <p className="muted">
            {canManageOrganizations
              ? 'Manage organizations, the type catalogue, and their members.'
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
          {error && <p className="form-error" role="alert">{error}</p>}
          {notice && <p className="form-success" role="status">{notice}</p>}

          {renderTabs()}

          {tab === 'organizations' && renderOrganizationsBody()}
          {tab === 'types' && renderTypesBody()}
          {tab === 'members' && renderMembersBody()}
        </>
      )}
    </>
  )
}
