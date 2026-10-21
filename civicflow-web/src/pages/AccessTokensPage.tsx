import { useEffect, useState } from 'react'
import {
  createPersonalAccessToken,
  listPersonalAccessTokens,
  revokePersonalAccessToken,
  type PersonalAccessToken,
} from '../auth'

const expiryOptions = [
  { value: '', label: 'Never expires' },
  { value: '30', label: '30 days' },
  { value: '90', label: '90 days' },
  { value: '365', label: '1 year' },
]

/**
 * Lets the signed-in user manage personal access tokens for non-browser
 * clients. The plaintext token is displayed exactly once, right after it is
 * created, and is never retrievable again.
 */
export default function AccessTokensPage() {
  const [tokens, setTokens] = useState<PersonalAccessToken[]>([])
  const [name, setName] = useState('')
  const [expiry, setExpiry] = useState('90')
  const [loading, setLoading] = useState(true)
  const [creating, setCreating] = useState(false)
  const [revokingId, setRevokingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [issuedToken, setIssuedToken] = useState<string | null>(null)
  const [reloadToken, setReloadToken] = useState(0)

  useEffect(() => {
    let cancelled = false

    const run = async () => {
      try {
        const data = await listPersonalAccessTokens()
        if (cancelled) return
        setTokens(data)
        setError(null)
      } catch (reason) {
        if (cancelled) return
        setError(
          reason instanceof Error
            ? reason.message
            : 'Unable to load access tokens.',
        )
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    void run()

    return () => {
      cancelled = true
    }
  }, [reloadToken])

  function reload() {
    setReloadToken((current) => current + 1)
  }

  async function handleCreate() {
    if (!name.trim()) {
      setError('A token name is required.')
      return
    }

    try {
      setCreating(true)
      setError(null)

      const created = await createPersonalAccessToken(
        name.trim(),
        expiry ? Number.parseInt(expiry, 10) : null,
      )

      setIssuedToken(created.token)
      setName('')
      reload()
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : 'Unable to create the access token.',
      )
    } finally {
      setCreating(false)
    }
  }

  async function handleRevoke(token: PersonalAccessToken) {
    try {
      setRevokingId(token.id)
      setError(null)
      await revokePersonalAccessToken(token.id)
      reload()
    } catch (reason) {
      setError(
        reason instanceof Error
          ? reason.message
          : 'Unable to revoke the access token.',
      )
    } finally {
      setRevokingId(null)
    }
  }

  return (
    <div className="case-card create-form" style={{ maxWidth: 820 }}>
      <h2>Personal access tokens</h2>
      <p className="muted">
        A personal access token lets a non-browser client (mobile app, CLI, or
        integration) call the CivicFlow API using
        {' '}
        <code>Authorization: Bearer &lt;token&gt;</code>. The token carries
        exactly the permissions of your account and can be revoked at any time.
      </p>

      {error && (
        <div className="auth-error" role="alert">
          {error}
        </div>
      )}

      {issuedToken && (
        <div
          className="case-card"
          style={{
            background: '#f0fdf4',
            border: '1px solid #bbf7d0',
            marginBottom: 16,
          }}
        >
          <strong>Copy this token now — it is shown only once.</strong>
          <pre
            style={{
              overflowX: 'auto',
              padding: 12,
              background: '#fff',
              border: '1px solid #d1d5db',
              borderRadius: 6,
              fontSize: 12,
            }}
          >
            {issuedToken}
          </pre>
          <button
            className="secondary-button"
            type="button"
            onClick={() => setIssuedToken(null)}
          >
            Done
          </button>
        </div>
      )}

      <div
        style={{
          display: 'flex',
          gap: 8,
          alignItems: 'flex-end',
          flexWrap: 'wrap',
          marginBottom: 20,
        }}
      >
        <label style={{ flex: '1 1 240px' }}>
          Token name
          <input
            type="text"
            maxLength={100}
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Reporting script"
            disabled={creating}
          />
        </label>
        <label style={{ flex: '0 1 180px' }}>
          Expires in
          <select
            value={expiry}
            onChange={(event) => setExpiry(event.target.value)}
            disabled={creating}
          >
            {expiryOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        </label>
        <button
          className="primary-button"
          type="button"
          onClick={handleCreate}
          disabled={creating}
        >
          {creating ? 'Creating…' : 'Create token'}
        </button>
      </div>

      {loading ? (
        <p className="muted">Loading access tokens…</p>
      ) : tokens.length === 0 ? (
        <p className="muted">No access tokens have been created yet.</p>
      ) : (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th style={{ textAlign: 'left', padding: 8 }}>Name</th>
              <th style={{ textAlign: 'left', padding: 8 }}>Token id</th>
              <th style={{ textAlign: 'left', padding: 8 }}>Created</th>
              <th style={{ textAlign: 'left', padding: 8 }}>Status</th>
              <th style={{ textAlign: 'right', padding: 8 }} />
            </tr>
          </thead>
          <tbody>
            {tokens.map((token) => (
              <tr key={token.id} style={{ borderTop: '1px solid #e5e7eb' }}>
                <td style={{ padding: 8 }}>{token.name}</td>
                <td style={{ padding: 8 }}>
                  <code>{token.publicId}</code>
                </td>
                <td style={{ padding: 8 }}>
                  {new Date(token.createdAt).toLocaleDateString()}
                </td>
                <td style={{ padding: 8 }}>
                  {token.revokedAt
                    ? 'Revoked'
                    : token.isActive
                      ? 'Active'
                      : 'Expired'}
                </td>
                <td style={{ padding: 8, textAlign: 'right' }}>
                  {!token.revokedAt && (
                    <button
                      className="secondary-button"
                      type="button"
                      onClick={() => handleRevoke(token)}
                      disabled={revokingId === token.id}
                    >
                      {revokingId === token.id ? 'Revoking…' : 'Revoke'}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
