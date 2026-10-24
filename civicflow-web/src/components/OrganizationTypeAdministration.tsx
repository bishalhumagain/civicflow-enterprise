import { useCallback, useEffect, useState } from 'react'
import type { OrganizationType } from '../domain'
import { Icon } from './Icon'
import {
  createOrganizationType,
  deleteOrganizationType,
  getOrganizationTypes,
  setOrganizationTypeActive,
  updateOrganizationType,
} from '../api/organization-types'

const UNCATEGORIZED_ID = '00000000-0000-0000-0000-000000000001'

export function OrganizationTypeAdministration() {
  const [types, setTypes] = useState<OrganizationType[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const [showCreateForm, setShowCreateForm] = useState(false)
  const [newName, setNewName] = useState('')
  const [newCode, setNewCode] = useState('')

  const [editingId, setEditingId] = useState<string | null>(null)
  const [editedName, setEditedName] = useState('')
  const [editedCode, setEditedCode] = useState('')

  const loadTypes = useCallback(async () => {
    setLoading(true)
    try {
      const items = await getOrganizationTypes()
      setTypes(items)
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : 'Organization types could not be loaded.',
      )
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadTypes()
  }, [loadTypes])

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
          : 'The organization type operation could not be completed.',
      )
    } finally {
      setBusy(null)
    }
  }

  async function handleCreate(event: React.FormEvent) {
    event.preventDefault()
    await run(
      'create',
      async () => {
        await createOrganizationType(newName.trim(), newCode.trim())
        setNewName('')
        setNewCode('')
        setShowCreateForm(false)
        await loadTypes()
      },
      'Organization type created.',
    )
  }

  function startEditing(item: OrganizationType) {
    setEditingId(item.id)
    setEditedName(item.name)
    setEditedCode(item.code)
    setNotice(null)
    setError(null)
  }

  async function handleUpdate(item: OrganizationType) {
    await run(
      `update-${item.id}`,
      async () => {
        await updateOrganizationType(
          item.id,
          editedName.trim(),
          editedCode.trim(),
        )
        setEditingId(null)
        await loadTypes()
      },
      'Organization type updated.',
    )
  }

  async function handleToggle(item: OrganizationType) {
    await run(
      `toggle-${item.id}`,
      async () => {
        await setOrganizationTypeActive(item.id, !item.isActive)
        await loadTypes()
      },
      item.isActive
        ? 'Organization type deactivated.'
        : 'Organization type activated.',
    )
  }

  async function handleDelete(item: OrganizationType) {
    if (
      !confirm(
        `Delete ${item.name}? This cannot be undone. Only unused types can be deleted.`,
      )
    ) {
      return
    }

    await run(
      `delete-${item.id}`,
      async () => {
        await deleteOrganizationType(item.id)
        await loadTypes()
      },
      'Organization type deleted.',
    )
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PLATFORM ADMINISTRATION</p>
          <h1>Organization types</h1>
          <p className="muted">
            The shared catalogue of organization types. Every organization must
            be assigned one; the default Uncategorized type is used when a type
            is not specified.
          </p>
        </div>
      </div>

      <section className="case-card admin-panel">
        <div className="admin-row" style={{ marginBottom: 16 }}>
          <button
            className="primary-button"
            disabled={loading || busy !== null}
            onClick={() => setShowCreateForm(!showCreateForm)}
            type="button"
          >
            {showCreateForm
              ? 'Cancel'
              : (
                <>
                  <Icon className="button-icon" name="plus" />
                  <span>Create organization type</span>
                </>
              )}
          </button>
        </div>

        {showCreateForm && (
          <form
            className="inline-form"
            onSubmit={handleCreate}
            style={{ marginBottom: 24 }}
          >
            <input
              required
              maxLength={200}
              placeholder="Type name"
              value={newName}
              onChange={(event) => setNewName(event.target.value)}
            />
            <input
              required
              maxLength={50}
              placeholder="Code"
              value={newCode}
              onChange={(event) => setNewCode(event.target.value)}
            />
            <button
              className="primary-button"
              disabled={loading || busy !== null}
              type="submit"
            >
              <Icon className="button-icon" name="plus" />
              <span>Create</span>
            </button>
          </form>
        )}

        {error && <p className="form-error" role="alert">{error}</p>}
        {notice && <p className="form-success" role="status">{notice}</p>}

        {loading ? (
          <p className="muted empty-state">Loading organization types…</p>
        ) : types.length === 0 ? (
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
                {types.map((item) => {
                  const isUncategorized = item.id === UNCATEGORIZED_ID

                  return (
                    <tr key={item.id}>
                      {editingId === item.id ? (
                        <>
                          <td>
                            <input
                              required
                              maxLength={200}
                              value={editedName}
                              onChange={(event) =>
                                setEditedName(event.target.value)}
                            />
                          </td>
                          <td>
                            <input
                              required
                              maxLength={50}
                              value={editedCode}
                              onChange={(event) =>
                                setEditedCode(event.target.value)}
                            />
                          </td>
                          <td>{item.organizationCount}</td>
                          <td>
                            {item.isActive ? 'Active' : 'Inactive'}
                          </td>
                          <td>
                            {new Date(item.createdAt).toLocaleDateString()}
                          </td>
                          <td>
                            <button
                              className="primary-button"
                              disabled={busy !== null}
                              onClick={() => void handleUpdate(item)}
                              type="button"
                            >
                              <Icon className="button-icon" name="save" />
                              <span>Save</span>
                            </button>
                            <button
                              className="secondary-button"
                              disabled={busy !== null}
                              onClick={() => setEditingId(null)}
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
                              className={
                                item.isActive ? 'status-green' : 'status-amber'
                              }
                            >
                              {item.isActive ? 'Active' : 'Inactive'}
                            </span>
                          </td>
                          <td>
                            {new Date(item.createdAt).toLocaleDateString()}
                          </td>
                          <td>
                            <button
                              className="secondary-button"
                              disabled={busy !== null}
                              onClick={() => startEditing(item)}
                              type="button"
                            >
                              <Icon className="button-icon" name="edit" />
                              <span>Edit</span>
                            </button>
                            {!isUncategorized && (
                              <button
                                className="secondary-button"
                                disabled={busy !== null}
                                onClick={() => void handleToggle(item)}
                                type="button"
                              >
                                {item.isActive ? 'Deactivate' : 'Activate'}
                              </button>
                            )}
                            {!isUncategorized && (
                              <button
                                className="secondary-button danger"
                                disabled={busy !== null}
                                onClick={() => void handleDelete(item)}
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
    </>
  )
}
