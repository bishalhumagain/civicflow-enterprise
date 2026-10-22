import { StatCard } from './StatCard'

export type CitizenDashboardProps = {
  fullName: string
  email: string
  emailConfirmed: boolean
  onOpenIntake: () => void
  onOpenProfile: () => void
  onOpenAccessTokens: () => void
}

/**
 * Dashboard shown to accounts that hold the citizen role. It only exposes
 * citizen self-service; case management and organization administration stay
 * behind the organization portal.
 */
export function CitizenDashboard({
  fullName,
  email,
  emailConfirmed,
  onOpenIntake,
  onOpenProfile,
  onOpenAccessTokens,
}: CitizenDashboardProps) {
  const firstName = fullName.trim().split(/\s+/)[0] || fullName

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">CITIZEN PORTAL</p>
          <h1>Welcome back, {firstName}</h1>
          <p className="muted">
            Submit service requests, keep your profile current, and manage the
            access tokens issued to your account.
          </p>
        </div>
      </div>

      <div className="stat-grid citizen-stat-grid">
        <StatCard
          label="Account status"
          value={emailConfirmed ? 'Verified' : 'Pending'}
          detail={emailConfirmed ? 'Email address confirmed' : 'Confirm your email address'}
          tone={emailConfirmed ? 'green' : 'amber'}
        />
        <StatCard
          label="Signed in as"
          value={email}
          detail="Citizen account"
          tone="blue"
        />
        <StatCard
          label="Portal access"
          value="Citizen"
          detail="Public self-service only"
          tone="purple"
        />
        <StatCard
          label="Case handling"
          value="Staff"
          detail="Handled by the responsible team"
          tone="amber"
        />
      </div>

      <div className="section-heading">
        <div>
          <h2>What you can do</h2>
          <span className="muted">Citizen self-service</span>
        </div>
      </div>

      <div className="directory-grid">
        <article className="case-card citizen-action-card">
          <h3>Request public assistance</h3>
          <p className="muted">
            Describe a local service issue and send it to the responsible team.
          </p>
          <button className="primary-button" onClick={onOpenIntake} type="button">
            Open public intake
          </button>
        </article>

        <article className="case-card citizen-action-card">
          <h3>My profile</h3>
          <p className="muted">
            Review the contact details and identifiers attached to your account.
          </p>
          <button className="primary-button" onClick={onOpenProfile} type="button">
            Open my profile
          </button>
        </article>

        <article className="case-card citizen-action-card">
          <h3>Access tokens</h3>
          <p className="muted">
            Issue and revoke personal access tokens for your own account.
          </p>
          <button className="primary-button" onClick={onOpenAccessTokens} type="button">
            Manage access tokens
          </button>
        </article>
      </div>
    </>
  )
}
