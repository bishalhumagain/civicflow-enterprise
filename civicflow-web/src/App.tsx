import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { apiRequest } from './api'
import { completeLogin, logout, oidcIsConfigured, startLogin, startRegistration, type AuthSession } from './auth'
import './App.css'

import {
  organizationId,
  statusLabels,
  statusOptions,
  type AssignmentEntry,
  type AssignmentHistoryData,
  type CaseDocument,
  type CaseDocumentsData,
  type CaseHistoryData,
  type CasePage,
  type CaseRecord,
  type CaseStatus,
  type HistoryEntry,
  type Membership,
  type MembershipsData,
  type Organization,
  type StaffMember,
  type WorkspaceView,
} from './domain'
import { requestJson } from './api-client'
import { StatusPill } from './components/StatusPill'
import { StatCard } from './components/StatCard'
import { CaseDetails } from './components/CaseDetails'
import { StaffDirectory } from './components/StaffDirectory'
import { OrganizationAdministration } from './components/OrganizationAdministration'
import { CitizenIntake } from './components/CitizenIntake'
import { LandingPage } from './pages/LandingPage'
import StaffLoginPage from './pages/StaffLoginPage'
import CitizenProfilePage from './pages/CitizenProfilePage'
import { useToast } from './components/use-toast'

const STAFF_LOGIN_PATH = '/admin'

type AppRoute = 'landing' | 'staff-login' | 'citizen-login' | 'citizen-register'

function resolveRoute(pathname: string): AppRoute {
  const path = pathname.replace(/\/+$/, '').toLowerCase()

  if (path === STAFF_LOGIN_PATH) return 'staff-login'
  if (path === '/citizen/login') return 'citizen-login'
  if (path === '/citizen/register') return 'citizen-register'

  return 'landing'
}

function navigateTo(path: string) {
  if (window.location.pathname !== path) {
    window.history.pushState({}, document.title, path)
  }
}

function App() {
  const { showToast } = useToast()
  const [authSession, setAuthSession] = useState<AuthSession | null>(null)
  const [cases, setCases] = useState<CaseRecord[]>([])
  const [selectedCaseId, setSelectedCaseId] = useState<string | null>(null)

  const [caseHistoryData, setCaseHistoryData] = useState<CaseHistoryData | null>(null)
  const [assignmentHistoryData, setAssignmentHistoryData] = useState<AssignmentHistoryData | null>(null)
  const [caseDocumentsData, setCaseDocumentsData] = useState<CaseDocumentsData | null>(null)

  const [staff, setStaff] = useState<StaffMember[]>([])
  const [organizations, setOrganizations] = useState<Organization[]>([])
  const [membershipsData, setMembershipsData] = useState<MembershipsData | null>(null)
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
  const [route, setRoute] = useState<AppRoute>(() => resolveRoute(window.location.pathname))
  const [loginLoading, setLoginLoading] = useState(false)
  const [loginError, setLoginError] = useState<string | null>(null)

  const selectedCase = cases.find((item) => item.id === selectedCaseId) ?? null

  const history = caseHistoryData && caseHistoryData.caseId === selectedCase?.id
      ? caseHistoryData.items
      : []

  const assignmentHistory = assignmentHistoryData && assignmentHistoryData.caseId === selectedCase?.id
      ? assignmentHistoryData.items
      : []

  const documents = caseDocumentsData && caseDocumentsData.caseId === selectedCase?.id
      ? caseDocumentsData.items
      : []
  const memberships = membershipsData?.organizationId === selectedOrganizationId
  && activeView === 'organizations'
      ? membershipsData.items
      : []

  const visibleCases = useMemo(() => cases.filter((item) => {
    const matchesStatus = selectedStatus === 'All' || item.status === selectedStatus
    const query = search.trim().toLowerCase()

    return matchesStatus && (!query || `${item.referenceNumber} ${item.title}`.toLowerCase().includes(query))
  }), [cases, search, selectedStatus])

  useEffect(() => {
    const handlePopState = () => setRoute(resolveRoute(window.location.pathname))
    window.addEventListener('popstate', handlePopState)

    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  useEffect(() => {
    void completeLogin()
        .then((session) => {
          setAuthSession(session)
          if (session) {
            showToast('Signed in successfully.', 'success')
          }
        })
        .catch((reason: unknown) => {
          const message = reason instanceof Error ? reason.message : 'OIDC login failed.'
          setError(message)
          showToast(message, 'error')
        })
  }, [showToast])

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
        setTotalPages(Math.max(1, caseResult.totalPages))
        setStaff(staffMembers)
        setSelectedCaseId((current) => {
          if (current && caseResult.items.some((item) => item.id === current)) {
            return current
          }

          return caseResult.items[0]?.id ?? null
        })
      } catch (reason) {
        if (mounted) {
          setError(reason instanceof Error ? reason.message : 'The CivicFlow API could not be reached.')
        }
      } finally {
        if (mounted) setLoading(false)
      }
    }

    void loadWorkspace()

    return () => {
      mounted = false
    }
  }, [authSession, casePage])

  useEffect(() => {
    if (!authSession || !selectedCase || selectedCase.id.startsWith('demo-')) return

    let mounted = true
    const session = authSession
    const caseId = selectedCase.id

    void requestJson<HistoryEntry[]>(`/api/cases/${caseId}/history`, session)
        .then((result) => {
          if (mounted) {
            setCaseHistoryData({ caseId, items: result })
          }
        })
        .catch((reason: unknown) => {
          if (mounted) setError(reason instanceof Error ? reason.message : 'Case history could not be loaded.')
        })

    void requestJson<AssignmentEntry[]>(`/api/cases/${caseId}/assignment-history`, session)
        .then((result) => {
          if (mounted) {
            setAssignmentHistoryData({ caseId, items: result })
          }
        })
        .catch((reason: unknown) => {
          if (mounted) setError(reason instanceof Error ? reason.message : 'Assignment history could not be loaded.')
        })

    void requestJson<CaseDocument[]>(`/api/cases/${caseId}/documents`, session)
        .then((result) => {
          if (mounted) {
            setCaseDocumentsData({ caseId, items: result })
          }
        })
        .catch((reason: unknown) => {
          if (mounted) setError(reason instanceof Error ? reason.message : 'Case documents could not be loaded.')
        })

    return () => {
      mounted = false
    }
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
        if (mounted) {
          setError(reason instanceof Error ? reason.message : 'Organizations could not be loaded. Your role may not permit platform administration.')
        }
      } finally {
        if (mounted) setAdminLoading(false)
      }
    }

    void loadOrganizations()

    return () => {
      mounted = false
    }
  }, [authSession, activeView])

  useEffect(() => {
    if (!authSession || activeView !== 'organizations' || !selectedOrganizationId) return

    let mounted = true
    const session = authSession
    const currentOrganizationId = selectedOrganizationId

    void requestJson<Membership[]>(
        `/api/organizations/${currentOrganizationId}/memberships`,
        session,
    )
        .then((loaded) => {
          if (mounted) {
            setMembershipsData({
              organizationId: currentOrganizationId,
              items: loaded,
            })
          }
        })
        .catch((reason: unknown) => {
          if (mounted) {
            setError(reason instanceof Error ? reason.message : 'Memberships could not be loaded.')
          }
        })

    return () => {
      mounted = false
    }
  }, [authSession, activeView, selectedOrganizationId])

  const beginOidcLogin = (options: { returnTo?: string; register?: boolean; toast: string }) => {
    setLoginError(null)
    setError(null)

    if (!oidcIsConfigured) {
      const message = 'Secure authentication is not configured. Use environment variables for OIDC/Keycloak.'
      setLoginError(message)
      setError(message)
      showToast(message, 'warning')
      return
    }

    setLoginLoading(true)
    showToast(options.toast, 'info')

    const run = options.register
        ? startRegistration({ returnTo: options.returnTo })
        : startLogin({ returnTo: options.returnTo })

    void run
        .catch((reason: unknown) => {
          const message = reason instanceof Error ? reason.message : 'OIDC login failed.'
          setLoginError(message)
          setError(message)
          showToast(message, 'error')
        })
        .finally(() => setLoginLoading(false))
  }

  const handleCitizenSignIn = () =>
      beginOidcLogin({
        returnTo: window.location.origin,
        toast: 'Redirecting to citizen sign-in...',
      })

  const handleCitizenRegister = () =>
      beginOidcLogin({
        returnTo: window.location.origin,
        register: true,
        toast: 'Redirecting to citizen registration...',
      })

  const handleOrganizationSignIn = () => {
    setLoginError(null)
    navigateTo(STAFF_LOGIN_PATH)
    setRoute('staff-login')
  }

  const handleBackToLanding = () => {
    setLoginError(null)
    navigateTo('/')
    setRoute('landing')
  }

  const handleLoginSubmit = () =>
      beginOidcLogin({
        toast: 'Redirecting to secure sign-in...',
      })

  const handleSignOut = () => {
    logout(authSession)
    setAuthSession(null)
    navigateTo('/')
    setRoute('landing')
    setActiveView('cases')
    showToast('Signed out.', 'info')
  }

  async function submitCase(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!authSession || !organizationId || !newTitle.trim() || !newDescription.trim()) return

    setActionLoading(true)
    setError(null)

    try {
      const created = await requestJson<CaseRecord>('/api/cases', authSession, {
        method: 'POST',
        body: JSON.stringify({
          title: newTitle.trim(),
          description: newDescription.trim(),
        }),
      })

      setCases((current) => [created, ...current])
      setSelectedCaseId(created.id)
      setNewTitle('')
      setNewDescription('')
      setShowCreateForm(false)
      showToast('Case created.', 'success')
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : 'Case creation failed. Your role may not permit this action.'
      setError(message)
      showToast(message, 'error')
    } finally {
      setActionLoading(false)
    }
  }

  async function changeStatus(status: CaseStatus) {
    if (!authSession || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

    const caseId = selectedCase.id

    setActionLoading(true)
    setError(null)

    try {
      const updated = await requestJson<{ id: string; status: CaseStatus; updatedAt: string }>(
          `/api/cases/${caseId}/status`,
          authSession,
          {
            method: 'PATCH',
            body: JSON.stringify({ newStatus: status }),
          },
      )

      setCases((current) => current.map((item) => (
          item.id === updated.id ? { ...item, status: updated.status } : item
      )))

      const loadedHistory = await requestJson<HistoryEntry[]>(
          `/api/cases/${caseId}/history`,
          authSession,
      )

      setCaseHistoryData({ caseId, items: loadedHistory })
      showToast('Status updated.', 'success')
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : 'Status update failed. Your role may not permit this action.'
      setError(message)
      showToast(message, 'error')
    } finally {
      setActionLoading(false)
    }
  }

  async function assignCase(staffMemberId: string) {
    if (!authSession || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-') || !staffMemberId) return

    setActionLoading(true)
    setError(null)

    try {
      await requestJson(`/api/cases/${selectedCase.id}/assignment`, authSession, {
        method: 'PATCH',
        body: JSON.stringify({ staffMemberId }),
      })
      showToast('Assignment updated.', 'success')
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : 'Assignment failed. Your role may not permit this action.'
      setError(message)
      showToast(message, 'error')
    } finally {
      setActionLoading(false)
    }
  }

  async function uploadDocument(file: File) {
    if (!authSession || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

    const caseId = selectedCase.id

    setActionLoading(true)
    setError(null)

    const formData = new FormData()
    formData.append('file', file)

    try {
      const uploaded = await requestJson<CaseDocument>(
          `/api/cases/${caseId}/documents`,
          authSession,
          { method: 'POST', body: formData },
      )

      setCaseDocumentsData((current) => ({
        caseId,
        items: current?.caseId === caseId
            ? [...current.items, uploaded]
            : [uploaded],
      }))
      showToast('Document uploaded.', 'success')
    } catch (reason) {
      const message = reason instanceof Error ? reason.message : 'Document upload failed. Check the file type and size.'
      setError(message)
      showToast(message, 'error')
    } finally {
      setActionLoading(false)
    }
  }

  async function downloadDocument(document: CaseDocument) {
    if (!authSession || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

    setActionLoading(true)
    setError(null)

    try {
      const response = await apiRequest(
          `/api/cases/${selectedCase.id}/documents/${document.id}`,
          authSession,
          organizationId,
      )

      if (response.status === 401) throw new Error('Your session is missing or has expired. Sign in again.')
      if (response.status === 403) throw new Error('You are not authorized to download this document.')
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
    if (!authSession || !organizationId) return

    setAdminLoading(true)
    setError(null)

    try {
      const created = await requestJson<StaffMember>('/api/staff', authSession, {
        method: 'POST',
        body: JSON.stringify({ fullName, email }),
      })

      setStaff((current) => [...current, created])
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Staff creation failed.')
    } finally {
      setAdminLoading(false)
    }
  }

  async function setStaffActive(member: StaffMember) {
    if (!authSession || !organizationId) return
    if (!window.confirm(`${member.isActive ? 'Deactivate' : 'Reactivate'} ${member.fullName}?`)) return

    setAdminLoading(true)
    setError(null)

    try {
      const result = await requestJson<StaffMember>(
          `/api/staff/${member.id}/${member.isActive ? 'deactivate' : 'reactivate'}`,
          authSession,
          { method: 'PATCH' },
      )

      setStaff((current) => current.map((item) => item.id === result.id ? result : item))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Staff status change failed.')
    } finally {
      setAdminLoading(false)
    }
  }

  async function createOrganization(name: string, code: string) {
    if (!authSession) return

    setAdminLoading(true)
    setError(null)

    try {
      const created = await requestJson<Organization>('/api/organizations', authSession, {
        method: 'POST',
        body: JSON.stringify({ name, code }),
      })

      setOrganizations((current) => [...current, created])
      setSelectedOrganizationId(created.id)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Organization creation failed.')
    } finally {
      setAdminLoading(false)
    }
  }

  async function updateOrganizationStatus(organization: Organization) {
    if (!authSession) return
    if (!window.confirm(`${organization.isActive ? 'Deactivate' : 'Reactivate'} ${organization.name}?`)) return

    setAdminLoading(true)
    setError(null)

    try {
      const result = await requestJson<Organization>(
          `/api/organizations/${organization.id}/${organization.isActive ? 'deactivate' : 'reactivate'}`,
          authSession,
          { method: 'PATCH' },
      )

      setOrganizations((current) => current.map((item) => item.id === result.id ? result : item))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Organization status change failed.')
    } finally {
      setAdminLoading(false)
    }
  }

  async function createMembership(userId: string, role: string) {
    if (!authSession || !selectedOrganizationId) return

    const currentOrganizationId = selectedOrganizationId

    setAdminLoading(true)
    setError(null)

    try {
      const created = await requestJson<Membership>(
          `/api/organizations/${currentOrganizationId}/memberships`,
          authSession,
          {
            method: 'POST',
            body: JSON.stringify({ userId, role }),
          },
      )

      setMembershipsData((current: MembershipsData | null) => ({
        organizationId: currentOrganizationId,
        items: current?.organizationId === currentOrganizationId
            ? [...current.items, created]
            : [created],
      }))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Membership creation failed.')
    } finally {
      setAdminLoading(false)
    }
  }

  async function updateMembership(membership: Membership, action: 'role' | 'active', role?: string) {
    if (!authSession || !selectedOrganizationId) return

    const currentOrganizationId = selectedOrganizationId

    setAdminLoading(true)
    setError(null)

    try {
      const path = action === 'role'
          ? `/api/organizations/${currentOrganizationId}/memberships/${membership.id}/role`
          : `/api/organizations/${currentOrganizationId}/memberships/${membership.id}/${membership.isActive ? 'deactivate' : 'reactivate'}`

      const result = await requestJson<Membership>(path, authSession, {
        method: action === 'role' ? 'PUT' : 'PATCH',
        ...(action === 'role' && role ? { body: JSON.stringify({ role }) } : {}),
      })

      setMembershipsData((current: MembershipsData | null) => ({
        organizationId: currentOrganizationId,
        items: current?.organizationId === currentOrganizationId
            ? current.items.map((item: Membership) => item.id === result.id ? result : item)
            : [result],
      }))
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Membership update failed.')
    } finally {
      setAdminLoading(false)
    }
  }

  if (route === 'staff-login' && !authSession) {
    return (
      <StaffLoginPage
        onSubmit={handleLoginSubmit}
        onBack={handleBackToLanding}
        loading={loginLoading}
        error={loginError}
      />
    )
  }

  if (!authSession) {
    return (
      <LandingPage
        onCitizenSignIn={handleCitizenSignIn}
        onCitizenRegister={handleCitizenRegister}
        onOrganizationSignIn={handleOrganizationSignIn}
        loading={loginLoading}
      />
    )
  }

  const caseEmptyMessage = !organizationId
      ? 'Configure an organization ID to load cases.'
      : !oidcIsConfigured
          ? 'Configure the OpenID Connect settings to sign in and load organization cases.'
          : !authSession
              ? 'Sign in to load organization cases.'
              : cases.length === 0
                  ? 'No cases are available in this organization.'
                  : 'No cases match the current filter.'

  const dataSourceLabel = loading
      ? 'Loading from API'
      : !organizationId
          ? 'Organization configuration required'
          : !oidcIsConfigured
              ? 'Authentication configuration required'
              : authSession
                  ? 'Live organization data'
                  : 'Sign in to load cases'

  return (
      <div className="app-shell">
        <aside className="sidebar">
          <div className="brand">
            <span className="brand-mark">C</span>
            <span>CivicFlow</span>
          </div>

          <div className="workspace-label">WORKSPACE</div>

          <div className="organization-switcher">
            <span className="organization-icon">CF</span>
            <span>
            <strong>Organization workspace</strong>
            <small>{organizationId || 'Configure an organization ID'}</small>
          </span>
          </div>

          <nav className="navigation" aria-label="Primary navigation">
            <button className={activeView === 'cases' ? 'active' : ''} onClick={() => setActiveView('cases')} type="button">
              ▣ <span>Cases</span><b>{cases.length}</b>
            </button>
            <button className={activeView === 'staff' ? 'active' : ''} onClick={() => setActiveView('staff')} type="button">
              ♙ <span>Staff &amp; team</span>
            </button>
            <button className={activeView === 'organizations' ? 'active' : ''} onClick={() => setActiveView('organizations')} type="button">
              ⌂ <span>Organizations</span>
            </button>
            <button className={activeView === 'citizen' ? 'active' : ''} onClick={() => setActiveView('citizen')} type="button">
              ⌂ <span>Public intake</span>
            </button>
            <button className={activeView === 'citizen-profile' ? 'active' : ''} onClick={() => setActiveView('citizen-profile')} type="button">
              ☺ <span>Citizen profile</span>
            </button>
          </nav>

          <div className="sidebar-footer">
            <button className="link-button" onClick={handleBackToLanding} type="button">⚙ <span>Back to landing</span></button>
            <div className="profile">
              <span className="avatar">CF</span>
              <span>
              <strong>{authSession ? 'Authenticated staff' : 'Not signed in'}</strong>
              <small>{authSession ? 'API session active' : oidcIsConfigured ? 'Authentication required' : 'Authentication not configured'}</small>
            </span>
            </div>
          </div>
        </aside>

        <main className="main-content">
          <header className="topbar">
            <div className="breadcrumb">
              <span>Workspace</span>
              <span>/</span>
              <strong>
                {activeView === 'cases'
                    ? 'Cases'
                    : activeView === 'staff'
                        ? 'Staff & team'
                        : activeView === 'organizations'
                            ? 'Organizations'
                            : 'Public intake'}
              </strong>
            </div>

            <div className="topbar-actions">
              {oidcIsConfigured && authSession ? (
                  <button className="auth-button" onClick={handleSignOut} type="button">
                    Sign out
                  </button>
              ) : (
                  <span className="demo-badge">{oidcIsConfigured ? 'NOT SIGNED IN' : 'AUTH NOT CONFIGURED'}</span>
              )}

              {oidcIsConfigured && !authSession && (
                  <button className="auth-button" onClick={handleLoginSubmit} type="button">
                    Sign in
                  </button>
              )}
            </div>
          </header>

          <section className="content">
            {error && <div className="auth-error" role="alert">{error}</div>}

            {activeView === 'citizen' ? (
                <CitizenIntake />
            ) : activeView === 'citizen-profile' ? (
                <CitizenProfilePage />
            ) : activeView === 'staff' ? (
                <StaffDirectory
                    staff={staff}
                    authenticated={Boolean(authSession)}
                    loading={adminLoading}
                    onCreate={createStaffMember}
                    onToggle={setStaffActive}
                />
            ) : activeView === 'organizations' ? (
                <OrganizationAdministration
                    authenticated={Boolean(authSession)}
                    loading={adminLoading}
                    organizations={organizations}
                    memberships={memberships}
                    selectedOrganizationId={selectedOrganizationId}
                    onSelectOrganization={setSelectedOrganizationId}
                    onCreateOrganization={createOrganization}
                    onToggleOrganization={updateOrganizationStatus}
                    onCreateMembership={createMembership}
                    onUpdateMembership={updateMembership}
                />
            ) : (
                <>
                  <div className="page-heading">
                    <div>
                      <p className="eyebrow">OPERATIONS OVERVIEW</p>
                      <h1>Case management</h1>
                      <p className="muted">Review, assign, and move public-service cases forward.</p>
                    </div>
                    <button
                        className="primary-button"
                        disabled={!authSession || !organizationId || actionLoading}
                        onClick={() => setShowCreateForm(true)}
                        type="button"
                    >
                      ＋ New case
                    </button>
                  </div>

                  {showCreateForm && (
                      <form className="case-card create-form" onSubmit={submitCase}>
                        <h2>Create case</h2>
                        <label>
                          Title
                          <input
                              required
                              minLength={3}
                              maxLength={200}
                              value={newTitle}
                              onChange={(event) => setNewTitle(event.target.value)}
                          />
                        </label>
                        <label>
                          Description
                          <textarea
                              required
                              minLength={10}
                              maxLength={5000}
                              value={newDescription}
                              onChange={(event) => setNewDescription(event.target.value)}
                          />
                        </label>
                        <div>
                          <button className="primary-button" disabled={actionLoading} type="submit">
                            {actionLoading ? 'Saving…' : 'Create case'}
                          </button>
                          <button className="secondary-button" onClick={() => setShowCreateForm(false)} type="button">
                            Cancel
                          </button>
                        </div>
                      </form>
                  )}

                  <div className="stat-grid">
                    <StatCard
                        label="Visible cases"
                        value={String(visibleCases.length)}
                        detail={dataSourceLabel}
                        tone="blue"
                    />
                    <StatCard
                        label="Needs review"
                        value={String(cases.filter((item) => item.status === 'Submitted' || item.status === 'UnderReview').length)}
                        detail="Submitted or under review"
                        tone="amber"
                    />
                    <StatCard
                        label="In progress"
                        value={String(cases.filter((item) => item.status === 'InProgress').length)}
                        detail="Active workflow"
                        tone="purple"
                    />
                    <StatCard
                        label="Resolved"
                        value={String(cases.filter((item) => item.status === 'Resolved' || item.status === 'Closed').length)}
                        detail="Resolved or closed"
                        tone="green"
                    />
                  </div>

                  <div className="section-heading">
                    <div>
                      <h2>Cases</h2>
                      <span className="muted">Showing {visibleCases.length} of {cases.length}</span>
                    </div>
                  </div>

                  <div className="case-layout">
                    <section className="case-card">
                      <div className="case-toolbar">
                        <div className="search">
                          ⌕
                          <input
                              aria-label="Search cases"
                              placeholder="Search cases…"
                              value={search}
                              onChange={(event) => setSearch(event.target.value)}
                          />
                        </div>

                        <select
                            aria-label="Filter by status"
                            value={selectedStatus}
                            onChange={(event) => setSelectedStatus(event.target.value as 'All' | CaseStatus)}
                        >
                          <option value="All">All statuses</option>
                          {statusOptions.map((status) => (
                              <option key={status} value={status}>{statusLabels[status]}</option>
                          ))}
                        </select>
                      </div>

                      {loading ? (
                          <p className="muted empty-state">Loading cases…</p>
                      ) : visibleCases.length === 0 ? (
                          <p className="muted empty-state">{caseEmptyMessage}</p>
                      ) : (
                          <div className="table-wrap">
                            <table>
                              <thead>
                              <tr>
                                <th>CASE</th>
                                <th>STATUS</th>
                                <th>PRIORITY</th>
                                <th>UPDATED</th>
                              </tr>
                              </thead>
                              <tbody>
                              {visibleCases.map((item) => (
                                  <tr
                                      className={selectedCase?.id === item.id ? 'selected-row' : ''}
                                      key={item.id}
                                      onClick={() => setSelectedCaseId(item.id)}
                                  >
                                    <td>
                                      <strong>{item.referenceNumber}</strong>
                                      <span>{item.title}</span>
                                    </td>
                                    <td><StatusPill status={item.status} /></td>
                                    <td>
                                <span className={`priority ${item.priority.toLowerCase()}`}>
                                  <i />{item.priority}
                                </span>
                                    </td>
                                    <td>{new Date(item.createdAt).toLocaleString()}</td>
                                  </tr>
                              ))}
                              </tbody>
                            </table>
                          </div>
                      )}

                      <div className="pagination">
                        <span>Page {casePage} of {totalPages}</span>
                        <span>
                      <button
                          disabled={casePage <= 1 || loading}
                          onClick={() => setCasePage((page) => Math.max(1, page - 1))}
                          type="button"
                      >
                        Previous
                      </button>
                      <button
                          disabled={casePage >= totalPages || loading}
                          onClick={() => setCasePage((page) => Math.min(totalPages, page + 1))}
                          type="button"
                      >
                        Next
                      </button>
                    </span>
                      </div>
                    </section>

                    <CaseDetails
                        caseRecord={selectedCase}
                        history={history}
                        assignmentHistory={assignmentHistory}
                        documents={documents}
                        staff={staff}
                        actionLoading={actionLoading}
                        onStatusChange={changeStatus}
                        onAssignmentChange={assignCase}
                        onUpload={uploadDocument}
                        onDownload={downloadDocument}
                    />
                  </div>
                </>
            )}

            <p className="footer-note">
              CivicFlow Enterprise · Secure organization-scoped case management ·{' '}
              <a href="https://github.com/bishalhumagain/civicflow-enterprise">View project</a>
            </p>
          </section>
        </main>
      </div>
  )
}

export default App
