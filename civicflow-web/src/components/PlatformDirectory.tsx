import { useState } from 'react'
import type { PlatformOverview, PlatformStaff, PlatformUser } from '../domain'
import { Icon } from './Icon'

/**
 * Platform-wide directory. Platform administrators use this to see every
 * account and every staff membership of the platform, including the phone
 * number collected during onboarding.
 */
export function PlatformDirectory({
  loading,
  overview,
  users,
  staff,
}: {
  loading: boolean
  overview: PlatformOverview | null
  users: PlatformUser[]
  staff: PlatformStaff[]
}) {
  const [tab, setTab] = useState<'users' | 'staff'>('users')

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PLATFORM DIRECTORY</p>
          <h1>People across the platform</h1>
          <p className="muted">
            Every account and every staff membership registered on the
            platform, with the contact details captured during onboarding.
          </p>
        </div>
      </div>

      {overview && (
        <div className="stat-grid">
          <div className="stat-card">
            <span className="stat-icon blue"><Icon className="app-icon" name="building" /></span>
            <div>
              <span>Accounts</span>
              <strong>{overview.userCount}</strong>
              <small>{overview.activeUserCount} active</small>
            </div>
          </div>
          <div className="stat-card">
            <span className="stat-icon purple"><Icon className="app-icon" name="directory" /></span>
            <div>
              <span>Staff memberships</span>
              <strong>{overview.staffCount}</strong>
              <small>{overview.activeStaffCount} active</small>
            </div>
          </div>
          <div className="stat-card">
            <span className="stat-icon green"><Icon className="app-icon" name="staff" /></span>
            <div>
              <span>Organizations</span>
              <strong>{overview.organizationCount}</strong>
              <small>{overview.activeOrganizationCount} active</small>
            </div>
          </div>
          <div className="stat-card">
            <span className="stat-icon amber"><Icon className="app-icon" name="mail" /></span>
            <div>
              <span>Invitations</span>
              <strong>{overview.invitationCount}</strong>
              <small>{overview.pendingInvitationCount} pending</small>
            </div>
          </div>
        </div>
      )}

      <section className="case-card admin-panel">
        <div className="admin-row" style={{ marginBottom: 12 }}>
          <button
            className={tab === 'users' ? 'primary-button' : 'secondary-button'}
            onClick={() => setTab('users')}
            type="button"
          >
            Accounts ({users.length})
          </button>
          <button
            className={tab === 'staff' ? 'primary-button' : 'secondary-button'}
            onClick={() => setTab('staff')}
            type="button"
          >
            Staff ({staff.length})
          </button>
        </div>

        {loading ? (
          <p className="muted empty-state">Loading the platform directory…</p>
        ) : tab === 'users' ? (
          users.length === 0 ? (
            <p className="muted empty-state">No accounts were returned.</p>
          ) : (
            <div aria-label="Platform accounts" className="table-wrap" role="region" tabIndex={0}>
              <table>
                <thead>
                  <tr>
                    <th>ACCOUNT</th>
                    <th>PHONE</th>
                    <th>PLATFORM ROLE</th>
                    <th>ROLES</th>
                    <th>ORGANIZATIONS</th>
                    <th>STATUS</th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((user) => (
                    <tr key={user.id}>
                      <td>
                        <strong>{user.fullName}</strong>
                        <span>{user.email}</span>
                      </td>
                      <td>{user.phoneNumber ?? '—'}</td>
                      <td><strong>{user.platformRole}</strong></td>
                      <td>{user.roles.join(', ') || '—'}</td>
                      <td>
                        {user.organizations.length === 0 ? (
                          '—'
                        ) : (
                          user.organizations.map((organization) => (
                            <span key={organization.membershipId}>
                              {organization.organizationName} ({organization.role})
                              {organization.isActive ? '' : ' · inactive'}
                            </span>
                          ))
                        )}
                      </td>
                      <td>
                        {user.isActive ? 'Active' : 'Inactive'}
                        {user.emailConfirmed ? '' : ' · email unconfirmed'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : staff.length === 0 ? (
          <p className="muted empty-state">No staff memberships were returned.</p>
        ) : (
          <div aria-label="Platform staff memberships" className="table-wrap" role="region" tabIndex={0}>
            <table>
              <thead>
                <tr>
                  <th>STAFF MEMBER</th>
                  <th>PHONE</th>
                  <th>ORGANIZATION</th>
                  <th>ROLE</th>
                  <th>EMAIL</th>
                  <th>STATUS</th>
                </tr>
              </thead>
              <tbody>
                {staff.map((member) => (
                  <tr key={member.membershipId}>
                    <td>
                      <strong>{member.fullName}</strong>
                      <span>{member.email}</span>
                    </td>
                    <td>{member.phoneNumber ?? '—'}</td>
                    <td>
                      <strong>{member.organizationName}</strong>
                      <span>{member.organizationCode}</span>
                    </td>
                    <td><strong>{member.role}</strong></td>
                    <td>{member.emailConfirmed ? 'Confirmed' : 'Unconfirmed'}</td>
                    <td>
                      {member.isActive ? 'Active' : 'Inactive'}
                      {member.accountIsActive ? '' : ' · account inactive'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
