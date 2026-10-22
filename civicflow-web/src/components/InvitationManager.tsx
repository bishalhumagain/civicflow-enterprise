import { useState, type FormEvent } from 'react'
import type { AccountInvitation } from '../domain'
import {
  canResend,
  canRevoke,
  invitationRoles,
  invitationStatus,
} from './invitation-utils'

/**
 * Invitation form and pending-invitation list shared by the staff directory
 * and the platform organization administration screen. Invitations never
 * contain a password: the invitee chooses one from the emailed single-use
 * link.
 */
export function InvitationManager({
  invitations,
  loading,
  disabled = false,
  onInvite,
  onResend,
  onRevoke,
}: {
  invitations: AccountInvitation[]
  loading: boolean
  disabled?: boolean
  onInvite: (fullName: string, email: string, role: string) => Promise<boolean>
  onResend: (invitationId: string) => Promise<void>
  onRevoke: (invitationId: string) => Promise<void>
}) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState('CaseWorker')

  async function handleInvite(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    const invited = await onInvite(fullName.trim(), email.trim(), role)

    if (invited) {
      setFullName('')
      setEmail('')
      setRole('CaseWorker')
    }
  }

  return (
    <>
      <form className="case-card inline-form" onSubmit={handleInvite}>
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
        <select
          aria-label="Role"
          value={role}
          onChange={(event) => setRole(event.target.value)}
        >
          {invitationRoles.map((option) => (
            <option key={option} value={option}>{option}</option>
          ))}
        </select>
        <button
          className="primary-button"
          disabled={loading || disabled}
          type="submit"
        >
          {loading ? 'Sending…' : 'Send invitation'}
        </button>
      </form>

      <small className="muted" style={{ display: 'block', margin: '0 0 16px 4px' }}>
        No password is created or emailed by CivicFlow. The invitee sets their
        own password from the link, and the link stops working once used.
      </small>

      <section className="case-card admin-panel">
        <h2>Invitations</h2>

        {invitations.length === 0 ? (
          <p className="muted empty-state">
            No invitations have been sent for this organization yet.
          </p>
        ) : (
          <div className="membership-list">
            {invitations.map((invitation) => (
              <div className="membership-row" key={invitation.id}>
                <div style={{ minWidth: 220 }}>
                  <strong>{invitation.fullName}</strong>
                  <span style={{ display: 'block' }}>{invitation.email}</span>
                  <small className="muted">
                    {invitation.role} · expires{' '}
                    {new Date(invitation.expiresAt).toLocaleString()}
                  </small>
                </div>
                <span>{invitationStatus(invitation)}</span>
                <div style={{ display: 'flex', gap: 8 }}>
                  {canResend(invitation) && (
                    <button
                      className="secondary-button"
                      disabled={loading}
                      onClick={() => void onResend(invitation.id)}
                      type="button"
                    >
                      Resend
                    </button>
                  )}
                  {canRevoke(invitation) && (
                    <button
                      className="secondary-button"
                      disabled={loading}
                      onClick={() => void onRevoke(invitation.id)}
                      type="button"
                    >
                      Revoke
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>
    </>
  )
}
