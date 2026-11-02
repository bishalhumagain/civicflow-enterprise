import type { AuthUser } from '../auth'
import { StatCard } from './StatCard'

export function PlatformAdminProfile({ user }: { user: AuthUser }) {
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PLATFORM ADMINISTRATION</p>
          <h1>My profile</h1>
          <p className="muted">Your platform administrator account and access level.</p>
        </div>
      </div>
      <div className="stat-grid profile-stat-grid">
        <StatCard
          icon="profile"
          label="Account"
          value={user.platformRole === 'SuperAdmin' ? 'Super Admin' : 'Platform Admin'}
          detail="Platform-level access"
          tone="blue"
        />
        <StatCard
          icon="mail"
          label="Email"
          value={user.emailConfirmed ? 'Verified' : 'Pending'}
          detail={user.email}
          tone={user.emailConfirmed ? 'green' : 'amber'}
        />
      </div>
      <section className="case-card profile-summary platform-profile-summary">
        <h2>Account details</h2>
        <dl>
          <div><dt>Name</dt><dd>{user.fullName}</dd></div>
          <div><dt>Email</dt><dd>{user.email}</dd></div>
          <div><dt>Platform role</dt><dd>{user.platformRole}</dd></div>
          <div><dt>Organization membership</dt><dd>Not required for platform access</dd></div>
        </dl>
      </section>
    </>
  )
}
