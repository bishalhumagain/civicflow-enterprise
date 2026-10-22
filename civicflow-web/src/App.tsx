import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { apiRequest } from './api'
import {
  AuthApiError,
  allowedPortals,
  canUsePortal,
  getCurrentUser,
  isSuperAdmin,
  login as loginRequest,
  logout as logoutRequest,
  type AuthUser,
  type SignInPortal,
} from './auth'
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
import { CitizenDashboard } from './components/CitizenDashboard'
import { LandingPage } from './pages/LandingPage'
import StaffLoginPage from './pages/StaffLoginPage'
import CitizenLoginPage from './pages/CitizenLoginPage'
import { CitizenRegisterPage } from './pages/CitizenRegisterPage'
import { VerifyEmailPage } from './pages/VerifyEmailPage'
import { ForgotPasswordPage } from './pages/ForgotPasswordPage'
import { ResetPasswordPage } from './pages/ResetPasswordPage'
import CitizenProfilePage from './pages/CitizenProfilePage'
import AccessTokensPage from './pages/AccessTokensPage'
import { useToast } from './components/use-toast'
import type { LoginCredentials } from './components/LoginForm'

const STAFF_LOGIN_PATH = '/admin'
const CITIZEN_LOGIN_PATH = '/citizen/login'
const CITIZEN_REGISTER_PATH = '/citizen/register'
const VERIFY_EMAIL_PATH = '/citizen/verify-email'
const FORGOT_PASSWORD_PATH = '/forgot-password'
const RESET_PASSWORD_PATH = '/reset-password'
const WORKSPACE_PATH = '/workspace'

type AppRoute =
  | 'landing'
  | 'staff-login'
  | 'citizen-login'
  | 'citizen-register'
  | 'verify-email'
  | 'forgot-password'
  | 'reset-password'
  | 'workspace'

function resolveRoute(pathname: string): AppRoute {
  const path = pathname.replace(/\/+$/, '').toLowerCase()

  if (path === STAFF_LOGIN_PATH) return 'staff-login'
  if (path === CITIZEN_LOGIN_PATH) return 'citizen-login'
  if (path === CITIZEN_REGISTER_PATH) return 'citizen-register'
  if (path === VERIFY_EMAIL_PATH) return 'verify-email'
  if (path === FORGOT_PASSWORD_PATH) return 'forgot-password'
  if (path === RESET_PASSWORD_PATH) return 'reset-password'
  if (path === WORKSPACE_PATH) return 'workspace'

  return 'landing'
}

function navigateTo(path: string) {
  if (window.location.pathname !== path) {
    window.history.pushState({}, document.title, path)
  }
}

const PORTAL_STORAGE_KEY = 'civicflow.portal'

/**
 * The portal the account last signed in through. It is only a UI preference
 * (never a credential), and it is always validated against the roles the API
 * reports before it is used.
 */
function readRememberedPortal(): SignInPortal | null {
  try {
    const stored = window.sessionStorage.getItem(PORTAL_STORAGE_KEY)
    return stored === 'citizen' || stored === 'organization' ? stored : null
  } catch {
    return null
  }
}

function rememberPortal(portal: SignInPortal): void {
  try {
    window.sessionStorage.setItem(PORTAL_STORAGE_KEY, portal)
  } catch {
    // Storage can be unavailable in private browsing modes. The portal is
    // then derived from the account roles on the next load.
  }
}

function forgetPortal(): void {
  try {
    window.sessionStorage.removeItem(PORTAL_STORAGE_KEY)
  } catch {
    // Ignore storage failures; the portal is derived from roles anyway.
  }
}

/**
 * Picks the portal a restored session belongs to. The remembered choice only
 * wins when the account's roles actually allow that portal.
 */
function resolveSessionPortal(user: AuthUser): SignInPortal {
  const portals = allowedPortals(user)
  const remembered = readRememberedPortal()

  if (remembered && portals.includes(remembered)) return remembered

  return portals.includes('citizen') ? 'citizen' : 'organization'
}

function App() {
  const { showToast } = useToast()
  const [currentUser, setCurrentUser] = useState<AuthUser | null>(null)
  const [sessionChecked, setSessionChecked] = useState(false)
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
  const [viewSelection, setViewSelection] = useState<WorkspaceView>('citizen-dashboard')
  const [activePortal, setActivePortal] = useState<SignInPortal | null>(null)
  const [casePage, setCasePage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [adminLoading, setAdminLoading] = useState(false)
  const [route, setRoute] = useState<AppRoute>(() => resolveRoute(window.location.pathname))
  const [loginLoading, setLoginLoading] = useState(false)
  const [loginError, setLoginError] = useState<string | null>(null)
  const [loginErrorCode, setLoginErrorCode] = useState<string | null>(null)

  const selectedCase = cases.find((item) => item.id === selectedCaseId) ?? null

  // The API reports the roles the session actually carries. Every dashboard,
  // navigation entry, and data request below is derived from them so the UI
  // can never offer a surface the API would refuse.
  const citizenAccess = canUsePortal(currentUser, 'citizen')
  const organizationAccess = canUsePortal(currentUser, 'organization')
  const superAdminAccess = isSuperAdmin(currentUser)

  const availableViews = useMemo<WorkspaceView[]>(() => {
    if (!currentUser) return []

    const views: WorkspaceView[] = []

    if (citizenAccess) {
      views.push('citizen-dashboard', 'citizen', 'citizen-profile')
    }

    if (organizationAccess) {
      views.push('cases', 'staff')

      if (superAdminAccess) views.push('organizations')
    }

    views.push('access-tokens')

    return views
  }, [currentUser, citizenAccess, organizationAccess, superAdminAccess])

  // The view is derived from the roles the API reports, so a citizen can
  // never land on an organization-only surface, even if a stale selection
  // survives a sign-out or a role change.
  const activeView: WorkspaceView = availableViews.includes(viewSelection)
    ? viewSelection
    : (availableViews[0] ?? 'access-tokens')

  const navigationItems = useMemo(() => {
    if (!currentUser) return []

    const items: { view: WorkspaceView; label: string; icon: string }[] = []

    if (citizenAccess) {
      items.push({ view: 'citizen-dashboard', label: 'Dashboard', icon: '⌂' })
      items.push({ view: 'citizen', label: 'Public intake', icon: '✎' })
      items.push({ view: 'citizen-profile', label: 'My profile', icon: '☺' })
    }

    if (organizationAccess) {
      items.push({ view: 'cases', label: 'Cases', icon: '▣' })
      items.push({ view: 'staff', label: 'Staff & team', icon: '♙' })

      if (superAdminAccess) {
        items.push({ view: 'organizations', label: 'Organizations', icon: '⌂' })
      }
    }

    items.push({ view: 'access-tokens', label: 'Access tokens', icon: '⌗' })

    return items
  }, [currentUser, citizenAccess, organizationAccess, superAdminAccess])

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
    let mounted = true

    void getCurrentUser()
        .then((user) => {
          if (!mounted) return

          setCurrentUser(user)

          if (user) {
            const portal = resolveSessionPortal(user)
            setActivePortal(portal)
            setViewSelection(portal === 'citizen' ? 'citizen-dashboard' : 'cases')
          }
        })
        .catch(() => {
          if (!mounted) return
          setCurrentUser(null)
        })
        .finally(() => {
          if (mounted) setSessionChecked(true)
        })

    return () => {
      mounted = false
    }
  }, [])

  // The active view always has to stay inside the set the current roles allow,
  // so a citizen can never land on an organization-only surface.
  useEffect(() => {
    if (!currentUser || !organizationAccess || !organizationId) return

    let mounted = true

    async function loadWorkspace() {
      setLoading(true)
      setError(null)

      try {
        const [caseResult, staffMembers] = await Promise.all([
          requestJson<CasePage>(`/api/cases?page=${casePage}&pageSize=20`),
          requestJson<StaffMember[]>('/api/staff'),
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
  }, [currentUser, organizationAccess, casePage])

  useEffect(() => {
    if (!currentUser || !organizationAccess || !selectedCase || selectedCase.id.startsWith('demo-')) return

    let mounted = true
    const caseId = selectedCase.id

    void requestJson<HistoryEntry[]>(`/api/cases/${caseId}/history`)
        .then((result) => {
          if (mounted) {
            setCaseHistoryData({ caseId, items: result })
          }
        })
        .catch((reason: unknown) => {
          if (mounted) setError(reason instanceof Error ? reason.message : 'Case history could not be loaded.')
        })

    void requestJson<AssignmentEntry[]>(`/api/cases/${caseId}/assignment-history`)
        .then((result) => {
          if (mounted) {
            setAssignmentHistoryData({ caseId, items: result })
          }
        })
        .catch((reason: unknown) => {
          if (mounted) setError(reason instanceof Error ? reason.message : 'Assignment history could not be loaded.')
        })

    void requestJson<CaseDocument[]>(`/api/cases/${caseId}/documents`)
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
  }, [currentUser, organizationAccess, selectedCase])

  useEffect(() => {
    if (!currentUser || !superAdminAccess || activeView !== 'organizations') return

    let mounted = true

    async function loadOrganizations() {
      setAdminLoading(true)

      try {
        const loaded = await requestJson<Organization[]>('/api/organizations')

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
  }, [currentUser, superAdminAccess, activeView])

  useEffect(() => {
    if (!currentUser || !superAdminAccess || activeView !== 'organizations' || !selectedOrganizationId) return

    let mounted = true
    const currentOrganizationId = selectedOrganizationId

    void requestJson<Membership[]>(
        `/api/organizations/${currentOrganizationId}/memberships`,
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
  }, [currentUser, superAdminAccess, activeView, selectedOrganizationId])

  const goToRoute = (path: string, target: AppRoute) => {
    setLoginError(null)
    setError(null)
    navigateTo(path)
    setRoute(target)
  }

  const handleCitizenSignIn = () => goToRoute(CITIZEN_LOGIN_PATH, 'citizen-login')
  const handleCitizenRegister = () => goToRoute(CITIZEN_REGISTER_PATH, 'citizen-register')
  const handleOrganizationSignIn = () => goToRoute(STAFF_LOGIN_PATH, 'staff-login')
  const handleForgotPassword = () => goToRoute(FORGOT_PASSWORD_PATH, 'forgot-password')
  const handleVerifyEmail = () => goToRoute(VERIFY_EMAIL_PATH, 'verify-email')

  const handleBackToLanding = () => {
    setLoginError(null)
    navigateTo('/')
    setRoute('landing')
  }

  const completeSignIn = (user: AuthUser, portal: SignInPortal) => {
    setCurrentUser(user)
    setActivePortal(portal)
    setViewSelection(portal === 'citizen' ? 'citizen-dashboard' : 'cases')
    setLoginError(null)
    setLoginErrorCode(null)
    setError(null)
    rememberPortal(portal)
    navigateTo(WORKSPACE_PATH)
    setRoute('workspace')
    showToast('Signed in successfully.', 'success')
  }

  const handleLoginSubmit = async (
    { email, password }: LoginCredentials,
    portal: SignInPortal,
  ) => {
    setLoginLoading(true)
    setLoginError(null)
    setLoginErrorCode(null)
    setError(null)

    try {
      const user = await loginRequest(email, password, portal)
      completeSignIn(user, portal)
    } catch (reason) {
      const message =
          reason instanceof Error ? reason.message : 'Sign-in failed. Check your email and password.'
      setLoginError(message)
      setLoginErrorCode(
        reason instanceof AuthApiError ? reason.code : null,
      )
      showToast(message, 'error')
    } finally {
      setLoginLoading(false)
    }
  }

  const handleCitizenLoginSubmit = (credentials: LoginCredentials) =>
    handleLoginSubmit(credentials, 'citizen')

  const handleOrganizationLoginSubmit = (credentials: LoginCredentials) =>
    handleLoginSubmit(credentials, 'organization')

  const handleSignOut = () => {
    void logoutRequest()
    setCurrentUser(null)
    setCases([])
    setStaff([])
    setOrganizations([])
    setActivePortal(null)
    forgetPortal()
    navigateTo('/')
    setRoute('landing')
    setViewSelection('citizen-dashboard')
    showToast('Signed out.', 'info')
  }

  async function submitCase(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (!currentUser || !organizationId || !newTitle.trim() || !newDescription.trim()) return

    setActionLoading(true)
    setError(null)

    try {
      const created = await requestJson<CaseRecord>('/api/cases', {
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
    if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

    const caseId = selectedCase.id

    setActionLoading(true)
    setError(null)

    try {
      const updated = await requestJson<{ id: string; status: CaseStatus; updatedAt: string }>(
          `/api/cases/${caseId}/status`,
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
    if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-') || !staffMemberId) return

    setActionLoading(true)
    setError(null)

    try {
      await requestJson(`/api/cases/${selectedCase.id}/assignment`, {
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
    if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

    const caseId = selectedCase.id

    setActionLoading(true)
    setError(null)

    const formData = new FormData()
    formData.append('file', file)

    try {
      const uploaded = await requestJson<CaseDocument>(
          `/api/cases/${caseId}/documents`,
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
    if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

    setActionLoading(true)
    setError(null)

    try {
      const response = await apiRequest(
          `/api/cases/${selectedCase.id}/documents/${document.id}`,
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
    if (!currentUser || !organizationId) return

    setAdminLoading(true)
    setError(null)

    try {
      const created = await requestJson<StaffMember>('/api/staff', {
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
    if (!currentUser || !organizationId) return
    if (!window.confirm(`${member.isActive ? 'Deactivate' : 'Reactivate'} ${member.fullName}?`)) return

    setAdminLoading(true)
    setError(null)

    try {
      const result = await requestJson<StaffMember>(
          `/api/staff/${member.id}/${member.isActive ? 'deactivate' : 'reactivate'}`,
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
    if (!currentUser) return

    setAdminLoading(true)
    setError(null)

    try {
      const created = await requestJson<Organization>('/api/organizations', {
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
    if (!currentUser) return
    if (!window.confirm(`${organization.isActive ? 'Deactivate' : 'Reactivate'} ${organization.name}?`)) return

    setAdminLoading(true)
    setError(null)

    try {
      const result = await requestJson<Organization>(
          `/api/organizations/${organization.id}/${organization.isActive ? 'deactivate' : 'reactivate'}`,
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
    if (!currentUser || !selectedOrganizationId) return

    const currentOrganizationId = selectedOrganizationId

    setAdminLoading(true)
    setError(null)

    try {
      const created = await requestJson<Membership>(
          `/api/organizations/${currentOrganizationId}/memberships`,
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
    if (!currentUser || !selectedOrganizationId) return

    const currentOrganizationId = selectedOrganizationId

    setAdminLoading(true)
    setError(null)

    try {
      const path = action === 'role'
          ? `/api/organizations/${currentOrganizationId}/memberships/${membership.id}/role`
          : `/api/organizations/${currentOrganizationId}/memberships/${membership.id}/${membership.isActive ? 'deactivate' : 'reactivate'}`

      const result = await requestJson<Membership>(path, {
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

  if (!sessionChecked) {
    return (
      <div className="landing-page">
        <main className="landing-main">
          <p className="muted">Restoring session…</p>
        </main>
      </div>
    )
  }

  if (!currentUser && route === 'staff-login') {
    return (
      <StaffLoginPage
        onSubmit={handleOrganizationLoginSubmit}
        onBack={handleBackToLanding}
        onForgotPassword={handleForgotPassword}
        loading={loginLoading}
        error={loginError}
        errorCode={loginErrorCode}
      />
    )
  }

  if (!currentUser && route === 'citizen-login') {
    return (
      <CitizenLoginPage
        onSubmit={handleCitizenLoginSubmit}
        onBack={handleBackToLanding}
        onRegister={handleCitizenRegister}
        onForgotPassword={handleForgotPassword}
        loading={loginLoading}
        error={loginError}
        errorCode={loginErrorCode}
      />
    )
  }

  if (!currentUser && route === 'citizen-register') {
    return (
      <CitizenRegisterPage
        onBack={handleBackToLanding}
        onSignIn={handleCitizenSignIn}
        onVerifiedPending={() => handleVerifyEmail()}
      />
    )
  }

  if (!currentUser && route === 'verify-email') {
    return (
      <VerifyEmailPage
        onBack={handleBackToLanding}
        onSignIn={handleCitizenSignIn}
      />
    )
  }

  if (!currentUser && route === 'forgot-password') {
    return (
      <ForgotPasswordPage
        onBack={handleBackToLanding}
        onSignIn={handleCitizenSignIn}
      />
    )
  }

  if (!currentUser && route === 'reset-password') {
    return (
      <ResetPasswordPage
        onBack={handleBackToLanding}
        onSignIn={handleCitizenSignIn}
      />
    )
  }

  if (!currentUser) {
    return (
      <LandingPage
        onCitizenSignIn={handleCitizenSignIn}
        onCitizenRegister={handleCitizenRegister}
        onOrganizationSignIn={handleOrganizationSignIn}
        loading={loginLoading}
      />
    )
  }

  // The organization portal is only reachable for staff and super
  // administrator accounts. A citizen that navigates to it is told why and
  // is returned to the citizen workspace instead of the staff views.
  if (route === 'staff-login' && !organizationAccess) {
    return (
      <div className="landing-page">
        <header className="landing-header">
          <div className="landing-brand">
            <span className="landing-brand-mark">C</span>
            <span className="landing-brand-name">CivicFlow</span>
          </div>
        </header>
        <main className="landing-main">
          <section style={{ maxWidth: 520, margin: '0 auto', width: '100%' }}>
            <h2 style={{ marginTop: 0 }}>Organization workspace unavailable</h2>
            <p className="muted">
              This account is signed in as a citizen account. Citizen accounts
              only have access to the citizen portal, so the organization
              workspace, case management, and staff administration stay closed.
            </p>
            <div className="landing-card-actions">
              <button
                className="landing-button primary"
                onClick={() => {
                  setViewSelection('citizen-dashboard')
                  navigateTo(WORKSPACE_PATH)
                  setRoute('workspace')
                }}
                type="button"
              >
                Go to my dashboard
              </button>
              <button className="landing-button" onClick={handleSignOut} type="button">
                Sign out
              </button>
            </div>
          </section>
        </main>
        <footer className="landing-footer">
          <p>CivicFlow Enterprise · Secure organization-scoped case management</p>
        </footer>
      </div>
    )
  }

  const caseEmptyMessage = !organizationId
      ? 'Configure an organization ID to load cases.'
      : cases.length === 0
          ? 'No cases are available in this organization.'
          : 'No cases match the current filter.'

  const dataSourceLabel = loading
      ? 'Loading from API'
      : !organizationId
          ? 'Organization configuration required'
          : 'Live organization data'

  const workspaceLabel = activePortal === 'citizen'
      ? 'Citizen portal'
      : superAdminAccess
          ? 'Platform administration'
          : 'Organization workspace'

  const workspaceCaption = activePortal === 'citizen'
      ? currentUser.email
      : organizationId || 'Configure an organization ID'

  const viewLabels: Record<WorkspaceView, string> = {
    'citizen-dashboard': 'Dashboard',
    citizen: 'Public intake',
    'citizen-profile': 'My profile',
    cases: 'Cases',
    staff: 'Staff & team',
    organizations: 'Organizations',
    'access-tokens': 'Access tokens',
  }

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
            <strong>{workspaceLabel}</strong>
            <small>{workspaceCaption}</small>
          </span>
          </div>

          <nav className="navigation" aria-label="Primary navigation">
            {navigationItems.map((item) => (
              <button
                className={activeView === item.view ? 'active' : ''}
                key={item.view}
                onClick={() => setViewSelection(item.view)}
                type="button"
              >
                {item.icon} <span>{item.label}</span>
                {item.view === 'cases' && <b>{cases.length}</b>}
              </button>
            ))}
          </nav>

          <div className="sidebar-footer">
            <button className="link-button" onClick={handleBackToLanding} type="button">⚙ <span>Back to landing</span></button>
            <div className="profile">
              <span className="avatar">CF</span>
              <span>
              <strong>{currentUser ? currentUser.fullName : 'Not signed in'}</strong>
              <small>{currentUser ? currentUser.email : 'Sign in to continue'}</small>
            </span>
            </div>
          </div>
        </aside>

        <main className="main-content">
          <header className="topbar">
            <div className="breadcrumb">
              <span>{workspaceLabel}</span>
              <span>/</span>
              <strong>{viewLabels[activeView]}</strong>
            </div>

            <div className="topbar-actions">
              <button className="auth-button" onClick={handleSignOut} type="button">
                Sign out
              </button>
            </div>
          </header>

          <section className="content">
            {error && <div className="auth-error" role="alert">{error}</div>}

            {activeView === 'citizen-dashboard' ? (
              <CitizenDashboard
                fullName={currentUser.fullName}
                email={currentUser.email}
                emailConfirmed={currentUser.emailConfirmed}
                onOpenIntake={() => setViewSelection('citizen')}
                onOpenProfile={() => setViewSelection('citizen-profile')}
                onOpenAccessTokens={() => setViewSelection('access-tokens')}
              />
            ) : activeView === 'citizen' ? (
              <CitizenIntake />
            ) : activeView === 'citizen-profile' ? (
              <CitizenProfilePage onSignIn={handleCitizenSignIn} />
            ) : activeView === 'access-tokens' ? (
              <AccessTokensPage />
            ) : activeView === 'staff' && organizationAccess ? (
                <StaffDirectory
                    staff={staff}
                    authenticated={Boolean(currentUser)}
                    loading={adminLoading}
                    onCreate={createStaffMember}
                    onToggle={setStaffActive}
                />
            ) : activeView === 'organizations' && superAdminAccess ? (
                <OrganizationAdministration
                    authenticated={Boolean(currentUser)}
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
            ) : organizationAccess ? (
                <>
                  <div className="page-heading">
                    <div>
                      <p className="eyebrow">OPERATIONS OVERVIEW</p>
                      <h1>Case management</h1>
                      <p className="muted">Review, assign, and move public-service cases forward.</p>
                    </div>
                    <button
                        className="primary-button"
                        disabled={!currentUser || !organizationId || actionLoading}
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
            ) : (
                <CitizenDashboard
                    fullName={currentUser.fullName}
                    email={currentUser.email}
                    emailConfirmed={currentUser.emailConfirmed}
                    onOpenIntake={() => setViewSelection('citizen')}
                    onOpenProfile={() => setViewSelection('citizen-profile')}
                    onOpenAccessTokens={() => setViewSelection('access-tokens')}
                />
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
