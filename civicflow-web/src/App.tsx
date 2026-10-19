import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { apiRequest } from './api'
import { completeLogin, logout, oidcIsConfigured, startLogin, type AuthSession } from './auth'
import './App.css'

type CaseStatus = 'Submitted' | 'UnderReview' | 'Assigned' | 'InProgress' | 'AwaitingInformation' | 'Resolved' | 'Closed' | 'Rejected'
type CasePriority = 'High' | 'Normal' | 'Low'
type CaseRecord = {
  id: string
  referenceNumber: string
  title: string
  description: string
  status: CaseStatus
  priority: CasePriority
  createdAt: string
}
type StaffMember = { id: string; fullName: string; email: string; isActive: boolean }
type HistoryEntry = { id: string; previousStatus: CaseStatus; newStatus: CaseStatus; changedAt: string }
type CaseDocument = { id: string; originalFileName: string; contentType: string; sizeBytes: number; uploadedAt: string }
type CasePage = { items: CaseRecord[]; page: number; pageSize: number; totalCount: number; totalPages: number }
type AssignmentEntry = { id: string; previousStaffMemberId: string | null; newStaffMemberId: string | null; assignedAt: string; assignedByUserId: string | null }
type Organization = { id: string; name: string; code: string; isActive: boolean; createdAt: string; updatedAt: string }
type Membership = { id: string; userId: string; organizationId: string; role: string; isActive: boolean; createdAt: string; updatedAt: string }
type WorkspaceView = 'cases' | 'staff' | 'organizations' | 'citizen'

const organizationId = import.meta.env.VITE_CIVICFLOW_ORGANIZATION_ID as string | undefined
const demoCases: CaseRecord[] = [
  { id: 'demo-1', referenceNumber: 'CF-8A31C', title: 'Damaged pedestrian crossing', description: 'The crossing signal is not working.', status: 'InProgress', priority: 'High', createdAt: new Date().toISOString() },
  { id: 'demo-2', referenceNumber: 'CF-7F20B', title: 'Streetlight outage on Cedar Avenue', description: 'The streetlight has been out for three nights.', status: 'UnderReview', priority: 'Normal', createdAt: new Date().toISOString() },
  { id: 'demo-3', referenceNumber: 'CF-6D19A', title: 'Accessible bus stop signage', description: 'Request for accessible signage at the bus stop.', status: 'Submitted', priority: 'Normal', createdAt: new Date().toISOString() },
]

const statusLabels: Record<CaseStatus, string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  Assigned: 'Assigned',
  InProgress: 'In progress',
  AwaitingInformation: 'Awaiting information',
  Resolved: 'Resolved',
  Closed: 'Closed',
  Rejected: 'Rejected',
}

const statusOptions: CaseStatus[] = ['Submitted', 'UnderReview', 'Assigned', 'InProgress', 'AwaitingInformation', 'Resolved', 'Closed', 'Rejected']

function App() {
  const [authSession, setAuthSession] = useState<AuthSession | null>(null)
  const [cases, setCases] = useState<CaseRecord[]>(demoCases)
  const [selectedCaseId, setSelectedCaseId] = useState<string | null>(demoCases[0].id)
  const [history, setHistory] = useState<HistoryEntry[]>([])
  const [documents, setDocuments] = useState<CaseDocument[]>([])
  const [assignmentHistory, setAssignmentHistory] = useState<AssignmentEntry[]>([])
  const [staff, setStaff] = useState<StaffMember[]>([])
  const [organizations, setOrganizations] = useState<Organization[]>([])
  const [memberships, setMemberships] = useState<Membership[]>([])
  const [selectedOrganizationId, setSelectedOrganizationId] = useState(organizationId ?? '')
  const [selectedStatus, setSelectedStatus] = useState<'All' | CaseStatus>('All')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(false)
  const [actionLoading, setActionLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showCreateForm, setShowCreateForm] = useState(false)
  const [newTitle, setNewTitle] = useState('')
  const [newDescription, setNewDescription] = useState('')
  const [activeView, setActiveView] = useState<WorkspaceView>('cases')
  const [casePage, setCasePage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [adminLoading, setAdminLoading] = useState(false)

  const selectedCase = cases.find((item) => item.id === selectedCaseId) ?? null
  const visibleCases = useMemo(() => cases.filter((item) => {
    const matchesStatus = selectedStatus === 'All' || item.status === selectedStatus
    const query = search.trim().toLowerCase()
    return matchesStatus && (!query || `${item.referenceNumber} ${item.title}`.toLowerCase().includes(query))
  }), [cases, search, selectedStatus])

  useEffect(() => {
    void completeLogin()
      .then(setAuthSession)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'OIDC login failed.'))
  }, [])

  useEffect(() => {
    if (!authSession || !organizationId) return
    let mounted = true
    const session = authSession
    async function loadWorkspace() {
      setLoading(true)
      setError(null)
      try {
        const [caseResult, staffMembers] = await Promise.all([
          requestJson<CasePage>(`/api/cases?page=${casePage}&pageSize=20`, session),
          requestJson<StaffMember[]>('/api/staff', session),
        ])
        if (!mounted) return
        setCases(caseResult.items)
        setTotalPages(caseResult.totalPages)
        setStaff(staffMembers)
        setSelectedCaseId(caseResult.items[0]?.id ?? null)
      } catch (reason) {
        if (mounted) setError(reason instanceof Error ? reason.message : 'The CivicFlow API could not be reached.')
      } finally {
        if (mounted) setLoading(false)
      }
    }
    void loadWorkspace()
    return () => { mounted = false }
  }, [authSession, casePage])

  useEffect(() => {
    if (!authSession || !selectedCase || selectedCase.id.startsWith('demo-')) return
    void requestJson<HistoryEntry[]>(`/api/cases/${selectedCase.id}/history`, authSession)
      .then(setHistory)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Case history could not be loaded.'))
    void requestJson<AssignmentEntry[]>(`/api/cases/${selectedCase.id}/assignment-history`, authSession)
      .then(setAssignmentHistory)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Assignment history could not be loaded.'))
    void requestJson<CaseDocument[]>(`/api/cases/${selectedCase.id}/documents`, authSession)
      .then(setDocuments)
      .catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Case documents could not be loaded.'))
  }, [authSession, selectedCase])

  useEffect(() => {
    if (!authSession || activeView !== 'organizations') return
    let mounted = true
    const session = authSession
    async function loadOrganizations() {
      setAdminLoading(true)
      try {
        const loaded = await requestJson<Organization[]>('/api/organizations', session)
        if (!mounted) return
        setOrganizations(loaded)
        setSelectedOrganizationId((current) => current || loaded[0]?.id || '')
      } catch (reason) {
        if (mounted) setError(reason instanceof Error ? reason.message : 'Organizations could not be loaded. Your role may not permit platform administration.')
      } finally {
        if (mounted) setAdminLoading(false)
      }
    }
    void loadOrganizations()
    return () => { mounted = false }
  }, [authSession, activeView])

  useEffect(() => {
    if (!authSession || activeView !== 'organizations' || !selectedOrganizationId) return
    let mounted = true
    void requestJson<Membership[]>(`/api/organizations/${selectedOrganizationId}/memberships`, authSession)
      .then((loaded) => { if (mounted) setMemberships(loaded) })
      .catch((reason: unknown) => { if (mounted) setError(reason instanceof Error ? reason.message : 'Memberships could not be loaded.') })
    return () => { mounted = false }
  }, [authSession, activeView, selectedOrganizationId])

  const handleLogin = () => void startLogin().catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'OIDC login failed.'))

  async function submitCase(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!authSession || !newTitle.trim() || !newDescription.trim()) return
    setActionLoading(true)
    setError(null)
    try {
      const created = await requestJson<CaseRecord>('/api/cases', authSession, {
        method: 'POST',
        body: JSON.stringify({ title: newTitle, description: newDescription }),
      })
      setCases((current) => [created, ...current])
      setSelectedCaseId(created.id)
      setNewTitle('')
      setNewDescription('')
      setShowCreateForm(false)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Case creation failed. Your role may not permit this action.')
    } finally {
      setActionLoading(false)
    }
  }

  async function changeStatus(status: CaseStatus) {
    if (!authSession || !selectedCase || selectedCase.id.startsWith('demo-')) return
    setActionLoading(true)
    try {
      const updated = await requestJson<{ id: string; status: CaseStatus; updatedAt: string }>(`/api/cases/${selectedCase.id}/status`, authSession, {
        method: 'PATCH',
        body: JSON.stringify({ newStatus: status }),
      })
      setCases((current) => current.map((item) => item.id === updated.id ? { ...item, status: updated.status } : item))
      setHistory(await requestJson<HistoryEntry[]>(`/api/cases/${selectedCase.id}/history`, authSession))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Status update failed. Your role may not permit this action.')
    } finally {
      setActionLoading(false)
    }
  }

  async function assignCase(staffMemberId: string) {
    if (!authSession || !selectedCase || selectedCase.id.startsWith('demo-') || !staffMemberId) return
    setActionLoading(true)
    try {
      await requestJson(`/api/cases/${selectedCase.id}/assignment`, authSession, {
        method: 'PATCH',
        body: JSON.stringify({ staffMemberId }),
      })
      setError(null)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Assignment failed. Your role may not permit this action.')
    } finally {
      setActionLoading(false)
    }
  }

  async function uploadDocument(file: File) {
    if (!authSession || !selectedCase || selectedCase.id.startsWith('demo-')) return
    setActionLoading(true)
    const formData = new FormData()
    formData.append('file', file)
    try {
      const uploaded = await requestJson<CaseDocument>(`/api/cases/${selectedCase.id}/documents`, authSession, { method: 'POST', body: formData })
      setDocuments((current) => [...current, uploaded])
      setError(null)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Document upload failed. Check the file type and size.')
    } finally {
      setActionLoading(false)
    }
  }

  async function downloadDocument(document: CaseDocument) {
    if (!authSession || !selectedCase || selectedCase.id.startsWith('demo-')) return
    setActionLoading(true)
    try {
      const response = await apiRequest(`/api/cases/${selectedCase.id}/documents/${document.id}`, authSession, organizationId ?? '')
      if (!response.ok) throw new Error(`Document download failed with HTTP ${response.status}.`)
      const url = URL.createObjectURL(await response.blob())
      const anchor = window.document.createElement('a')
      anchor.href = url
      anchor.download = document.originalFileName
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Document download failed.')
    } finally {
      setActionLoading(false)
    }
  }

  async function createStaffMember(fullName: string, email: string) {
    if (!authSession) return
    setAdminLoading(true)
    try {
      const created = await requestJson<StaffMember>('/api/staff', authSession, { method: 'POST', body: JSON.stringify({ fullName, email }) })
      setStaff((current) => [...current, created])
      setError(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Staff creation failed.') }
    finally { setAdminLoading(false) }
  }

  async function setStaffActive(member: StaffMember) {
    if (!authSession || !window.confirm(`${member.isActive ? 'Deactivate' : 'Reactivate'} ${member.fullName}?`)) return
    setAdminLoading(true)
    try {
      const result = await requestJson<StaffMember>(`/api/staff/${member.id}/${member.isActive ? 'deactivate' : 'reactivate'}`, authSession, { method: 'PATCH' })
      setStaff((current) => current.map((item) => item.id === result.id ? result : item))
      setError(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Staff status change failed.') }
    finally { setAdminLoading(false) }
  }

  async function createOrganization(name: string, code: string) {
    if (!authSession) return
    setAdminLoading(true)
    try {
      const created = await requestJson<Organization>('/api/organizations', authSession, { method: 'POST', body: JSON.stringify({ name, code }) })
      setOrganizations((current) => [...current, created])
      setSelectedOrganizationId(created.id)
      setError(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Organization creation failed.') }
    finally { setAdminLoading(false) }
  }

  async function updateOrganizationStatus(organization: Organization) {
    if (!authSession || !window.confirm(`${organization.isActive ? 'Deactivate' : 'Reactivate'} ${organization.name}?`)) return
    setAdminLoading(true)
    try {
      const result = await requestJson<Organization>(`/api/organizations/${organization.id}/${organization.isActive ? 'deactivate' : 'reactivate'}`, authSession, { method: 'PATCH' })
      setOrganizations((current) => current.map((item) => item.id === result.id ? result : item))
      setError(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Organization status change failed.') }
    finally { setAdminLoading(false) }
  }

  async function createMembership(userId: string, role: string) {
    if (!authSession || !selectedOrganizationId) return
    setAdminLoading(true)
    try {
      const created = await requestJson<Membership>(`/api/organizations/${selectedOrganizationId}/memberships`, authSession, { method: 'POST', body: JSON.stringify({ userId, role }) })
      setMemberships((current) => [...current, created])
      setError(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Membership creation failed.') }
    finally { setAdminLoading(false) }
  }

  async function updateMembership(membership: Membership, action: 'role' | 'active', role?: string) {
    if (!authSession || !selectedOrganizationId) return
    setAdminLoading(true)
    try {
      const path = action === 'role' ? `/api/organizations/${selectedOrganizationId}/memberships/${membership.id}/role` : `/api/organizations/${selectedOrganizationId}/memberships/${membership.id}/${membership.isActive ? 'deactivate' : 'reactivate'}`
      const result = await requestJson<Membership>(path, authSession, { method: action === 'role' ? 'PUT' : 'PATCH', ...(action === 'role' ? { body: JSON.stringify({ role }) } : {}) })
      setMemberships((current) => current.map((item) => item.id === result.id ? result : item))
      setError(null)
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Membership update failed.') }
    finally { setAdminLoading(false) }
  }

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><span className="brand-mark">C</span><span>CivicFlow</span></div>
      <div className="workspace-label">WORKSPACE</div>
      <div className="organization-switcher"><span className="organization-icon">CF</span><span><strong>Organization workspace</strong><small>{organizationId ?? 'Configure an organization ID'}</small></span></div>
      <nav className="navigation" aria-label="Primary navigation"><button className={activeView === 'cases' ? 'active' : ''} onClick={() => setActiveView('cases')} type="button">▣ <span>Cases</span><b>{cases.length}</b></button><button className={activeView === 'staff' ? 'active' : ''} onClick={() => setActiveView('staff')} type="button">♙ <span>Staff &amp; team</span></button><button className={activeView === 'organizations' ? 'active' : ''} onClick={() => setActiveView('organizations')} type="button">⌂ <span>Organizations</span></button><button className={activeView === 'citizen' ? 'active' : ''} onClick={() => setActiveView('citizen')} type="button">⌂ <span>Public intake</span></button></nav>
      <div className="sidebar-footer"><a href="#settings">⚙ <span>Settings</span></a><div className="profile"><span className="avatar">CF</span><span><strong>{authSession ? 'Authenticated staff' : 'Local reviewer'}</strong><small>{authSession ? 'API session active' : 'Demo mode'}</small></span></div></div>
    </aside>
    <main className="main-content">
      <header className="topbar"><div className="breadcrumb"><span>Workspace</span><span>/</span><strong>{activeView === 'cases' ? 'Cases' : activeView === 'staff' ? 'Staff & team' : activeView === 'organizations' ? 'Organizations' : 'Public intake'}</strong></div><div className="topbar-actions">{oidcIsConfigured && authSession ? <button className="auth-button" onClick={() => logout(authSession)} type="button">Sign out</button> : <span className="demo-badge">{oidcIsConfigured ? 'NOT SIGNED IN' : 'DEMO MODE'}</span>}{oidcIsConfigured && !authSession && <button className="auth-button" onClick={handleLogin} type="button">Sign in</button>}</div></header>
      <section className="content">
        {error && <div className="auth-error" role="alert">{error}</div>}
        {activeView === 'citizen' ? <CitizenLanding /> : activeView === 'staff' ? <StaffDirectory staff={staff} authenticated={Boolean(authSession)} loading={adminLoading} onCreate={createStaffMember} onToggle={setStaffActive} /> : activeView === 'organizations' ? <OrganizationAdministration authenticated={Boolean(authSession)} loading={adminLoading} organizations={organizations} memberships={memberships} selectedOrganizationId={selectedOrganizationId} onSelectOrganization={setSelectedOrganizationId} onCreateOrganization={createOrganization} onToggleOrganization={updateOrganizationStatus} onCreateMembership={createMembership} onUpdateMembership={updateMembership} /> : <>
        <div className="page-heading"><div><p className="eyebrow">OPERATIONS OVERVIEW</p><h1>Case management</h1><p className="muted">Review, assign, and move public-service cases forward.</p></div><button className="primary-button" disabled={!authSession || actionLoading} onClick={() => setShowCreateForm(true)} type="button">＋ New case</button></div>
        {showCreateForm && <form className="case-card create-form" onSubmit={submitCase}><h2>Create case</h2><label>Title<input required minLength={3} maxLength={200} value={newTitle} onChange={(event) => setNewTitle(event.target.value)} /></label><label>Description<textarea required minLength={10} maxLength={5000} value={newDescription} onChange={(event) => setNewDescription(event.target.value)} /></label><div><button className="primary-button" disabled={actionLoading} type="submit">{actionLoading ? 'Saving…' : 'Create case'}</button><button className="secondary-button" onClick={() => setShowCreateForm(false)} type="button">Cancel</button></div></form>}
        <div className="stat-grid"><StatCard label="Visible cases" value={String(visibleCases.length)} detail={loading ? 'Loading from API' : authSession ? 'Live organization data' : 'Local demo data'} tone="blue" /><StatCard label="Needs review" value={String(cases.filter((item) => item.status === 'Submitted' || item.status === 'UnderReview').length)} detail="Submitted or under review" tone="amber" /><StatCard label="In progress" value={String(cases.filter((item) => item.status === 'InProgress').length)} detail="Active workflow" tone="purple" /><StatCard label="Resolved" value={String(cases.filter((item) => item.status === 'Resolved' || item.status === 'Closed').length)} detail="Resolved or closed" tone="green" /></div>
        <div className="section-heading"><div><h2>Cases</h2><span className="muted">Showing {visibleCases.length} of {cases.length}</span></div></div>
        <div className="case-layout">
          <section className="case-card"><div className="case-toolbar"><div className="search">⌕ <input aria-label="Search cases" placeholder="Search cases…" value={search} onChange={(event) => setSearch(event.target.value)} /></div><select aria-label="Filter by status" value={selectedStatus} onChange={(event) => setSelectedStatus(event.target.value as 'All' | CaseStatus)}><option value="All">All statuses</option>{statusOptions.map((status) => <option key={status} value={status}>{statusLabels[status]}</option>)}</select></div>{loading ? <p className="muted empty-state">Loading cases…</p> : visibleCases.length === 0 ? <p className="muted empty-state">No cases match the current filter.</p> : <div className="table-wrap"><table><thead><tr><th>CASE</th><th>STATUS</th><th>PRIORITY</th><th>UPDATED</th></tr></thead><tbody>{visibleCases.map((item) => <tr className={selectedCase?.id === item.id ? 'selected-row' : ''} key={item.id} onClick={() => setSelectedCaseId(item.id)}><td><strong>{item.referenceNumber}</strong><span>{item.title}</span></td><td><StatusPill status={item.status} /></td><td><span className={`priority ${item.priority.toLowerCase()}`}><i />{item.priority}</span></td><td>{new Date(item.createdAt).toLocaleString()}</td></tr>)}</tbody></table></div>}<div className="pagination"><span>Page {casePage} of {totalPages}</span><span><button disabled={casePage <= 1 || loading} onClick={() => setCasePage((page) => Math.max(1, page - 1))} type="button">Previous</button><button disabled={casePage >= totalPages || loading} onClick={() => setCasePage((page) => Math.min(totalPages, page + 1))} type="button">Next</button></span></div></section>
          <CaseDetails caseRecord={selectedCase} history={history} assignmentHistory={assignmentHistory} documents={documents} staff={staff} actionLoading={actionLoading} onStatusChange={changeStatus} onAssignmentChange={assignCase} onUpload={uploadDocument} onDownload={downloadDocument} />
        </div>
        </>}
        <p className="footer-note">CivicFlow Enterprise · Secure organization-scoped case management · <a href="https://github.com/bishalhumagain/civicflow-enterprise">View project</a></p>
      </section>
    </main>
  </div>
}

function CaseDetails({ caseRecord, history, assignmentHistory, documents, staff, actionLoading, onStatusChange, onAssignmentChange, onUpload, onDownload }: { caseRecord: CaseRecord | null; history: HistoryEntry[]; assignmentHistory: AssignmentEntry[]; documents: CaseDocument[]; staff: StaffMember[]; actionLoading: boolean; onStatusChange: (status: CaseStatus) => Promise<void>; onAssignmentChange: (staffMemberId: string) => Promise<void>; onUpload: (file: File) => Promise<void>; onDownload: (document: CaseDocument) => Promise<void> }) {
  if (!caseRecord) return <aside className="detail-card"><p className="muted empty-state">Select a case to view details.</p></aside>
  return <aside className="detail-card"><div className="detail-header"><span>CASE DETAILS</span></div><div className="detail-title"><span className="case-reference">{caseRecord.referenceNumber}</span><h2>{caseRecord.title}</h2><StatusPill status={caseRecord.status} /></div><p className="case-description">{caseRecord.description}</p><div className="detail-section"><h3>Workflow</h3><label>Status<select disabled={actionLoading || caseRecord.id.startsWith('demo-')} value={caseRecord.status} onChange={(event) => void onStatusChange(event.target.value as CaseStatus)}>{statusOptions.map((status) => <option key={status} value={status}>{statusLabels[status]}</option>)}</select></label><label>Assign to<select disabled={actionLoading || caseRecord.id.startsWith('demo-')} defaultValue="" onChange={(event) => void onAssignmentChange(event.target.value)}><option value="">Select staff member</option>{staff.filter((member) => member.isActive).map((member) => <option key={member.id} value={member.id}>{member.fullName}</option>)}</select></label></div><div className="detail-section"><h3>Documents</h3>{caseRecord.id.startsWith('demo-') ? <p className="muted">Documents are available for live cases.</p> : <><label className="upload-control">Attach supporting document<input disabled={actionLoading} type="file" accept=".pdf,.jpg,.jpeg,.png,.doc,.docx" onChange={(event) => { const file = event.target.files?.[0]; if (file) void onUpload(file); event.currentTarget.value = '' }} /></label>{documents.length === 0 ? <p className="muted">No documents attached.</p> : <ul>{documents.map((document) => <li key={document.id}><button className="document-link" disabled={actionLoading} onClick={() => void onDownload(document)} type="button">{document.originalFileName}</button> <small>{Math.ceil(document.sizeBytes / 1024)} KB</small></li>)}</ul>}</>}</div><div className="detail-section timeline"><h3>Status history</h3>{caseRecord.id.startsWith('demo-') || history.length === 0 ? <p className="muted">No recorded status changes yet.</p> : history.map((item) => <p key={item.id}><span className="timeline-dot" /><span><strong>{statusLabels[item.previousStatus]} → {statusLabels[item.newStatus]}</strong><small>{new Date(item.changedAt).toLocaleString()}</small></span></p>)}</div><div className="detail-section timeline"><h3>Assignment history</h3>{caseRecord.id.startsWith('demo-') || assignmentHistory.length === 0 ? <p className="muted">No recorded assignments yet.</p> : assignmentHistory.map((item) => <p key={item.id}><span className="timeline-dot" /><span><strong>{item.previousStaffMemberId ? 'Reassigned case' : 'Case assigned'}</strong><small>{new Date(item.assignedAt).toLocaleString()}</small></span></p>)}</div></aside>
}

function StaffDirectory({ staff, authenticated, loading, onCreate, onToggle }: { staff: StaffMember[]; authenticated: boolean; loading: boolean; onCreate: (fullName: string, email: string) => Promise<void>; onToggle: (member: StaffMember) => Promise<void> }) {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  return <><div className="page-heading"><div><p className="eyebrow">ORGANIZATION DIRECTORY</p><h1>Staff &amp; team</h1><p className="muted">Active staff available for case assignment in the selected organization.</p></div></div>{authenticated && <form className="case-card inline-form" onSubmit={(event) => { event.preventDefault(); void onCreate(fullName, email); setFullName(''); setEmail('') }}><input required minLength={1} maxLength={150} placeholder="Full name" value={fullName} onChange={(event) => setFullName(event.target.value)} /><input required type="email" maxLength={254} placeholder="Email address" value={email} onChange={(event) => setEmail(event.target.value)} /><button className="primary-button" disabled={loading} type="submit">Add staff</button></form>}<section className="case-card directory-card">{!authenticated ? <p className="muted empty-state">Sign in with a staff identity to load the organization directory.</p> : staff.length === 0 ? <p className="muted empty-state">No active staff members were returned, or this role cannot access staff administration.</p> : <div className="directory-grid">{staff.map((member) => <article className="directory-member" key={member.id}><span className="avatar">{member.fullName.slice(0, 2).toUpperCase()}</span><div><strong>{member.fullName}</strong><span>{member.email}</span><small>{member.isActive ? 'Active' : 'Inactive'}</small></div><button className="secondary-button" disabled={loading} onClick={() => void onToggle(member)} type="button">{member.isActive ? 'Deactivate' : 'Reactivate'}</button></article>)}</div>}</section></>
}

function OrganizationAdministration({ authenticated, loading, organizations, memberships, selectedOrganizationId, onSelectOrganization, onCreateOrganization, onToggleOrganization, onCreateMembership, onUpdateMembership }: { authenticated: boolean; loading: boolean; organizations: Organization[]; memberships: Membership[]; selectedOrganizationId: string; onSelectOrganization: (id: string) => void; onCreateOrganization: (name: string, code: string) => Promise<void>; onToggleOrganization: (organization: Organization) => Promise<void>; onCreateMembership: (userId: string, role: string) => Promise<void>; onUpdateMembership: (membership: Membership, action: 'role' | 'active', role?: string) => Promise<void> }) {
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [userId, setUserId] = useState('')
  const [role, setRole] = useState('Viewer')
  const selected = organizations.find((item) => item.id === selectedOrganizationId)
  return <><div className="page-heading"><div><p className="eyebrow">ACCESS ADMINISTRATION</p><h1>Organizations &amp; memberships</h1><p className="muted">Platform administrators can manage organizations; organization administrators can manage memberships in their own organization.</p></div></div>{!authenticated ? <section className="case-card empty-state"><p className="muted">Sign in to access authorized administration features.</p></section> : <><section className="case-card admin-panel"><h2>Organizations</h2>{organizations.length === 0 ? <p className="muted">No platform organizations loaded. If you are an OrganizationAdmin, select the configured organization below to manage its memberships.</p> : <div className="admin-row"><select aria-label="Selected organization" value={selectedOrganizationId} onChange={(event) => onSelectOrganization(event.target.value)}>{organizations.map((item) => <option key={item.id} value={item.id}>{item.name} ({item.code})</option>)}</select>{selected && <button className="secondary-button" disabled={loading} onClick={() => void onToggleOrganization(selected)} type="button">{selected.isActive ? 'Deactivate' : 'Reactivate'}</button>}</div>}<form className="inline-form" onSubmit={(event) => { event.preventDefault(); void onCreateOrganization(name, code); setName(''); setCode('') }}><input required placeholder="Organization name" value={name} onChange={(event) => setName(event.target.value)} /><input required placeholder="Code" value={code} onChange={(event) => setCode(event.target.value)} /><button className="primary-button" disabled={loading} type="submit">Create organization</button></form></section><section className="case-card admin-panel"><h2>Memberships {selectedOrganizationId && <small>{selectedOrganizationId}</small>}</h2><form className="inline-form" onSubmit={(event) => { event.preventDefault(); void onCreateMembership(userId, role); setUserId('') }}><input required placeholder="Application user ID" value={userId} onChange={(event) => setUserId(event.target.value)} /><select value={role} onChange={(event) => setRole(event.target.value)}><option>Viewer</option><option>CaseWorker</option><option>OrganizationAdmin</option></select><button className="primary-button" disabled={loading || !selectedOrganizationId} type="submit">Add membership</button></form>{memberships.length === 0 ? <p className="muted empty-state">No memberships returned, or this identity is not authorized for the selected organization.</p> : <div className="membership-list">{memberships.map((membership) => <div className="membership-row" key={membership.id}><code>{membership.userId}</code><select value={membership.role} disabled={loading} onChange={(event) => void onUpdateMembership(membership, 'role', event.target.value)}><option>Viewer</option><option>CaseWorker</option><option>OrganizationAdmin</option></select><span>{membership.isActive ? 'Active' : 'Inactive'}</span><button className="secondary-button" disabled={loading} onClick={() => void onUpdateMembership(membership, 'active')} type="button">{membership.isActive ? 'Deactivate' : 'Reactivate'}</button></div>)}</div>}</section></>}</>
}

function CitizenLanding() {
  const [submitted, setSubmitted] = useState(false)
  return <><div className="page-heading"><div><p className="eyebrow">PUBLIC SERVICE PORTAL</p><h1>Request public assistance</h1><p className="muted">Describe a local service issue and provide enough detail for the responsible team.</p></div></div><section className="case-card citizen-card"><div><h2>Citizen intake foundation</h2><p>This public-facing form is prepared for the future citizen submission contract. It does not submit to the staff API and cannot expose internal case data.</p></div><form onSubmit={(event) => { event.preventDefault(); setSubmitted(true) }}><label>Issue title<input required minLength={3} maxLength={200} placeholder="What do you need help with?" /></label><label>Details<textarea required minLength={10} maxLength={5000} placeholder="Include location, dates, and relevant context." /></label><button className="primary-button" type="submit">{submitted ? 'Submission unavailable' : 'Preview request'}</button>{submitted && <p className="auth-error" role="status">Citizen intake is not enabled yet. A dedicated citizen API, organization-resolution policy, and document authorization must be implemented before requests can be submitted.</p>}</form></section></>
}

async function requestJson<T>(path: string, session: AuthSession, init?: RequestInit): Promise<T> {
  const response = await apiRequest(path, session, organizationId ?? '', init)
  if (response.status === 401) throw new Error('Your session is missing or has expired. Sign in again.')
  if (response.status === 403) throw new Error('You are not authorized for this organization or operation.')
  if (!response.ok) throw new Error(`API request failed with HTTP ${response.status}.`)
  if (response.status === 204) return undefined as T
  return await response.json() as T
}

function StatCard({ label, value, detail, tone }: { label: string; value: string; detail: string; tone: string }) { return <article className="stat-card"><div className={`stat-icon ${tone}`}>◈</div><div><span>{label}</span><strong>{value}</strong><small>{detail}</small></div></article> }
function StatusPill({ status }: { status: CaseStatus }) { return <span className={`status-pill ${status.toLowerCase()}`}><i />{statusLabels[status]}</span> }

export default App
