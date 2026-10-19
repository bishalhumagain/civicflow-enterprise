import { useEffect, useMemo, useState } from 'react'
import { apiRequest } from './api'
import { completeLogin, logout, oidcIsConfigured, startLogin, type AuthSession } from './auth'
import './App.css'

type CaseStatus = 'Submitted' | 'Under review' | 'In progress' | 'Resolved'
type CaseRecord = { reference: string; title: string; status: CaseStatus; priority: 'High' | 'Normal' | 'Low'; updated: string; owner: string }

const cases: CaseRecord[] = [
  { reference: 'CF-8A31C', title: 'Damaged pedestrian crossing', status: 'In progress', priority: 'High', updated: '12 minutes ago', owner: 'Maya Chen' },
  { reference: 'CF-7F20B', title: 'Streetlight outage on Cedar Avenue', status: 'Under review', priority: 'Normal', updated: '42 minutes ago', owner: 'Unassigned' },
  { reference: 'CF-6D19A', title: 'Request for accessible bus stop signage', status: 'Submitted', priority: 'Normal', updated: '1 hour ago', owner: 'Unassigned' },
  { reference: 'CF-5B04E', title: 'Community garden water connection', status: 'Resolved', priority: 'Low', updated: 'Yesterday', owner: 'Jordan Lee' },
]

function App() {
  const [authSession, setAuthSession] = useState<AuthSession | null>(null)
  const [authError, setAuthError] = useState<string | null>(null)
  const [caseRecords, setCaseRecords] = useState(cases)
  const [selectedStatus, setSelectedStatus] = useState<'All' | CaseStatus>('All')
  const [selectedCase, setSelectedCase] = useState(cases[0])
  const visibleCases = useMemo(() => selectedStatus === 'All' ? caseRecords : caseRecords.filter((item) => item.status === selectedStatus), [caseRecords, selectedStatus])

  useEffect(() => {
    void completeLogin().then(setAuthSession).catch((error: unknown) => setAuthError(error instanceof Error ? error.message : 'OIDC login failed.'))
  }, [])

  useEffect(() => {
    const organizationId = import.meta.env.VITE_CIVICFLOW_ORGANIZATION_ID as string | undefined
    if (!authSession || !organizationId) return

    let mounted = true
    void apiRequest('/api/cases?page=1&pageSize=20', authSession, organizationId)
      .then(async (response) => {
        if (!response.ok) throw new Error(`Case API request failed with HTTP ${response.status}.`)
        const payload = await response.json() as { items: Array<{ referenceNumber: string; title: string; status: string; priority: string; updatedAt: string }> }
        const liveCases: CaseRecord[] = payload.items.map((item) => ({
          reference: item.referenceNumber,
          title: item.title,
          status: item.status === 'UnderReview' ? 'Under review' : item.status === 'InProgress' ? 'In progress' : item.status as CaseStatus,
          priority: item.priority as CaseRecord['priority'],
          updated: new Date(item.updatedAt).toLocaleString(),
          owner: 'Organization case queue',
        }))
        if (mounted && liveCases.length > 0) {
          setCaseRecords(liveCases)
          setSelectedCase(liveCases[0])
        }
      })
      .catch((error: unknown) => {
        if (mounted) setAuthError(error instanceof Error ? error.message : 'Case API request failed.')
      })

    return () => { mounted = false }
  }, [authSession])

  const handleLogin = () => void startLogin().catch((error: unknown) => setAuthError(error instanceof Error ? error.message : 'OIDC login failed.'))

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><span className="brand-mark">C</span><span>CivicFlow</span></div>
      <div className="workspace-label">WORKSPACE</div>
      <button className="organization-switcher" type="button"><span className="organization-icon">NV</span><span><strong>North Valley Services</strong><small>Organization workspace</small></span><span className="chevron">⌄</span></button>
      <nav className="navigation" aria-label="Primary navigation"><a className="active" href="#cases">▣ <span>Cases</span><b>24</b></a><a href="#team">♙ <span>Staff &amp; team</span></a><a href="#organizations">⌂ <span>Organizations</span></a><a href="#reports">◒ <span>Reports</span></a></nav>
      <div className="sidebar-footer"><a href="#settings">⚙ <span>Settings</span></a><div className="profile"><span className="avatar">AR</span><span><strong>Alex Rivera</strong><small>Organization admin</small></span><span>•••</span></div></div>
    </aside>
    <main className="main-content">
      <header className="topbar"><div className="breadcrumb"><span>Workspace</span><span>/</span><strong>Cases</strong></div><div className="topbar-actions">{oidcIsConfigured && authSession ? <button className="auth-button" onClick={() => logout(authSession)} type="button">Sign out</button> : <span className="demo-badge">DEMO MODE</span>}{oidcIsConfigured && !authSession && <button className="auth-button" onClick={handleLogin} type="button">Sign in</button>}<button className="icon-button" aria-label="Notifications" type="button">♧</button><button className="help-button" type="button">?</button></div></header>
      <section className="content">
        {authError && <div className="auth-error" role="alert">{authError}</div>}
        <div className="page-heading"><div><p className="eyebrow">OPERATIONS OVERVIEW</p><h1>Case management</h1><p className="muted">Review, assign, and move public-service cases forward.</p></div><button className="primary-button" type="button">＋ New case</button></div>
        <div className="stat-grid"><StatCard label="Open cases" value="24" detail="↑ 8% this month" tone="blue" /><StatCard label="Needs review" value="07" detail="3 high priority" tone="amber" /><StatCard label="In progress" value="11" detail="Across 4 teams" tone="purple" /><StatCard label="Resolved this month" value="38" detail="↑ 14% vs last month" tone="green" /></div>
        <div className="section-heading"><div><h2>Recent cases</h2><span className="muted">Showing {visibleCases.length} of 24 cases</span></div><button className="secondary-button" type="button">Export view ↓</button></div>
        <div className="case-layout">
          <section className="case-card"><div className="case-toolbar"><div className="search">⌕ <input aria-label="Search cases" placeholder="Search cases..." /></div><select aria-label="Filter by status" value={selectedStatus} onChange={(event) => setSelectedStatus(event.target.value as 'All' | CaseStatus)}><option>All</option><option>Submitted</option><option>Under review</option><option>In progress</option><option>Resolved</option></select><button className="filter-button" type="button">☷ Filter</button></div><div className="table-wrap"><table><thead><tr><th>CASE</th><th>STATUS</th><th>PRIORITY</th><th>ASSIGNED TO</th><th>UPDATED</th><th aria-label="Actions" /></tr></thead><tbody>{visibleCases.map((item) => <tr className={selectedCase.reference === item.reference ? 'selected-row' : ''} key={item.reference} onClick={() => setSelectedCase(item)}><td><strong>{item.reference}</strong><span>{item.title}</span></td><td><StatusPill status={item.status} /></td><td><span className={`priority ${item.priority.toLowerCase()}`}><i />{item.priority}</span></td><td>{item.owner}</td><td>{item.updated}</td><td className="row-action">•••</td></tr>)}</tbody></table></div><div className="pagination"><span>1–{visibleCases.length} of 24 results</span><div><button type="button">‹</button><button className="current" type="button">1</button><button type="button">2</button><button type="button">3</button><button type="button">›</button></div></div></section>
          <aside className="detail-card"><div className="detail-header"><span>CASE DETAILS</span><button aria-label="More case actions" type="button">•••</button></div><div className="detail-title"><span className="case-reference">{selectedCase.reference}</span><h2>{selectedCase.title}</h2><StatusPill status={selectedCase.status} /></div><div className="detail-section"><h3>Assignment</h3><div className="assignee"><span className="avatar small">{selectedCase.owner === 'Unassigned' ? '?' : 'MC'}</span><span><strong>{selectedCase.owner}</strong><small>{selectedCase.owner === 'Unassigned' ? 'Ready for assignment' : 'Case worker'}</small></span><button type="button">Change</button></div></div><div className="detail-section"><h3>Case information</h3><dl><div><dt>Priority</dt><dd><span className={`priority ${selectedCase.priority.toLowerCase()}`}><i />{selectedCase.priority}</span></dd></div><div><dt>Last updated</dt><dd>{selectedCase.updated}</dd></div></dl></div><div className="detail-section timeline"><h3>Activity</h3><p><span className="timeline-dot" /><span><strong>Case status updated</strong><small>{selectedCase.updated}</small></span></p><p><span className="timeline-dot muted-dot" /><span><strong>Case submitted</strong><small>Yesterday at 9:14 AM</small></span></p></div><button className="full-button" type="button">Open case details →</button></aside>
        </div>
        <p className="footer-note">CivicFlow Enterprise · Secure organization-scoped case management · <a href="https://github.com/bishalhumagain/civicflow-enterprise">View project</a></p>
      </section>
    </main>
  </div>
}

function StatCard({ label, value, detail, tone }: { label: string; value: string; detail: string; tone: string }) { return <article className="stat-card"><div className={`stat-icon ${tone}`}>◈</div><div><span>{label}</span><strong>{value}</strong><small className={detail.startsWith('↑') ? 'positive' : ''}>{detail}</small></div></article> }
function StatusPill({ status }: { status: CaseStatus }) { return <span className={`status-pill ${status.toLowerCase().replace(' ', '-')}`}><i />{status}</span> }

export default App
