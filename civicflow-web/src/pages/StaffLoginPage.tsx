import { LoginForm } from '../components/LoginForm'

export type StaffLoginPageProps = {
  onSubmit: () => void
  onBack?: () => void
  loading?: boolean
  error?: string | null
}

export function StaffLoginPage({
  onSubmit,
  onBack,
  loading = false,
  error = null,
}: StaffLoginPageProps) {
  return (
    <div className="landing-page">
      <header className="landing-header">
        <div className="landing-brand">
          <span className="landing-brand-mark">C</span>
          <span className="landing-brand-name">CivicFlow</span>
        </div>
      </header>
      <main className="landing-main">
        <section style={{ maxWidth: 480, margin: '0 auto', width: '100%' }}>
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
            <h2 style={{ margin: 0 }}>Organization &amp; Platform Sign In</h2>
            {onBack ? (
              <button className="secondary-button" type="button" onClick={onBack}>
                ← Back
              </button>
            ) : null}
          </div>
          <p className="muted" style={{ marginTop: 0 }}>
            Staff, Organization Administrators, and Super Admins sign in here. This page is served at a dedicated URL
            separate from the public landing page.
          </p>
          <LoginForm onSubmit={onSubmit} loading={loading} error={error} />
        </section>
      </main>
      <footer className="landing-footer">
        <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
      </footer>
    </div>
  )
}

export default StaffLoginPage
