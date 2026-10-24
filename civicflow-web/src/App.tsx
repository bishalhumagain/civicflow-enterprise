import {useEffect, useMemo, useState, type FormEvent} from 'react'
import {apiRequest} from './api'
import {
    AuthApiError,
    allowedPortals,
    canUsePortal,
    getCurrentUser,
    login as loginRequest,
    logout as logoutRequest,
    type AuthUser,
    type SignInPortal,
} from './auth'
import './App.css'
import { Icon, type IconName } from './components/Icon'

import {
    statusLabels,
    statusOptions,
    type AccountInvitation,
    type ApplicationError,
    type AssignmentEntry,
    type AssignmentHistoryData,
    type AuditEvent,
    type CaseDocument,
    type CaseDocumentsData,
    type CaseHistoryData,
    type CasePage,
    type CaseRecord,
    type CaseStatistics,
    type CaseStatus,
    type HistoryEntry,
    type Membership,
    type MembershipsData,
    type Organization,
    type OrganizationWorkspaceOption,
    type PlatformOverview,
    type PlatformCitizen,
    type PlatformStaff,
    type StaffMember,
    type WorkspaceView,
} from './domain'
import {requestJson, setRequestOrganizationId} from './api-client'
import {StatusPill} from './components/StatusPill'
import {StatCard} from './components/StatCard'
import {CaseDetails} from './components/CaseDetails'
import {StaffDirectory} from './components/StaffDirectory'
import {OrganizationAdministration} from './components/OrganizationAdministration'
import {PlatformDirectory} from './components/PlatformDirectory'
import {AuditLog} from './components/AuditLog'
import {emptyAuditFilters, type AuditFilters} from './components/audit-utils'
import {CitizenIntake} from './components/CitizenIntake'
import {CitizenDashboard} from './components/CitizenDashboard'
import {PlatformDashboard} from './components/PlatformDashboard'
import {OrganizationDashboard} from './components/OrganizationDashboard'
import {PlatformErrors} from './components/PlatformErrors'
import {NotificationCenter} from './components/NotificationCenter'
import {BackgroundJobsPage} from './components/BackgroundJobsPage'
import {LandingPage} from './pages/LandingPage'
import StaffLoginPage from './pages/StaffLoginPage'
import PlatformAdminLoginPage from './pages/PlatformAdminLoginPage'
import SetupPasswordPage from './pages/SetupPasswordPage'
import CitizenLoginPage from './pages/CitizenLoginPage'
import {CitizenRegisterPage} from './pages/CitizenRegisterPage'
import {VerifyEmailPage} from './pages/VerifyEmailPage'
import {ForgotPasswordPage} from './pages/ForgotPasswordPage'
import {ResetPasswordPage} from './pages/ResetPasswordPage'
import ProfilePage from './pages/ProfilePage'
import {useToast} from './components/use-toast'
import type {LoginCredentials} from './components/LoginForm'

const STAFF_LOGIN_PATH = '/admin'
const PLATFORM_ADMIN_LOGIN_PATH = '/super-platform-admin'
const SETUP_PASSWORD_PATH = '/setup-password'
const CITIZEN_LOGIN_PATH = '/citizen/login'
const CITIZEN_REGISTER_PATH = '/citizen/register'
const VERIFY_EMAIL_PATH = '/citizen/verify-email'
const FORGOT_PASSWORD_PATH = '/forgot-password'
const RESET_PASSWORD_PATH = '/reset-password'
const WORKSPACE_PATH = '/workspace'

type AppRoute =
    | 'landing'
    | 'staff-login'
    | 'platform-admin-login'
    | 'setup-password'
    | 'citizen-login'
    | 'citizen-register'
    | 'verify-email'
    | 'forgot-password'
    | 'reset-password'
    | 'workspace'

function resolveRoute(pathname: string): AppRoute {
    const path = pathname.replace(/\/+$/, '').toLowerCase()

    if (path === STAFF_LOGIN_PATH) return 'staff-login'
    if (path === PLATFORM_ADMIN_LOGIN_PATH) return 'platform-admin-login'
    if (path === SETUP_PASSWORD_PATH) return 'setup-password'
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
        return stored === 'citizen' ||
        stored === 'organization' ||
        stored === 'platform-admin'
            ? stored
            : null
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

    if (portals.includes('platform-admin')) return 'platform-admin'
    if (portals.includes('citizen')) return 'citizen'
    return 'organization'
}

function App() {
    const {showToast} = useToast()
    const [currentUser, setCurrentUser] = useState<AuthUser | null>(null)
    const [sessionChecked, setSessionChecked] = useState(false)
    const [cases, setCases] = useState<CaseRecord[]>([])
    const [selectedCaseId, setSelectedCaseId] = useState<string | null>(null)

    const [caseHistoryData, setCaseHistoryData] = useState<CaseHistoryData | null>(null)
    const [assignmentHistoryData, setAssignmentHistoryData] = useState<AssignmentHistoryData | null>(null)
    const [caseDocumentsData, setCaseDocumentsData] = useState<CaseDocumentsData | null>(null)

    const [staff, setStaff] = useState<StaffMember[]>([])
    const [organizations, setOrganizations] = useState<Organization[]>([])
    const [workspaceOrganizations, setWorkspaceOrganizations] = useState<OrganizationWorkspaceOption[]>([])
    const [organizationId, setOrganizationId] = useState<string | null>(null)
    const [membershipsData, setMembershipsData] = useState<MembershipsData | null>(null)
    const [workspaceMemberships, setWorkspaceMemberships] = useState<Membership[]>([])
    const [invitationsData, setInvitationsData] = useState<{
        organizationId: string
        items: AccountInvitation[]
    } | null>(null)
    const [selectedOrganizationId, setSelectedOrganizationId] = useState('')
    const [selectedStatus, setSelectedStatus] = useState<'All' | CaseStatus>('All')
    const [search, setSearch] = useState('')
    const [loading, setLoading] = useState(false)
    const [retryVersion, setRetryVersion] = useState(0)
    const [organizationRetryVersion, setOrganizationRetryVersion] = useState(0)
    const [actionLoading, setActionLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [showCreateForm, setShowCreateForm] = useState(false)
    const [newTitle, setNewTitle] = useState('')
    const [newDescription, setNewDescription] = useState('')
    const [viewSelection, setViewSelection] = useState<WorkspaceView>('citizen-dashboard')
    const [activePortal, setActivePortal] = useState<SignInPortal | null>(null)
    const [casePage, setCasePage] = useState(1)
    const [totalPages, setTotalPages] = useState(1)
    const [caseTotalCount, setCaseTotalCount] = useState(0)
    const [adminLoading, setAdminLoading] = useState(false)
    const [route, setRoute] = useState<AppRoute>(() => resolveRoute(window.location.pathname))
    const [loginLoading, setLoginLoading] = useState(false)
    const [loginError, setLoginError] = useState<string | null>(null)
    const [loginErrorCode, setLoginErrorCode] = useState<string | null>(null)

    const [platformCitizens, setPlatformCitizens] = useState<PlatformCitizen[]>([])
    const [platformStaff, setPlatformStaff] = useState<PlatformStaff[]>([])
    const [platformOverview, setPlatformOverview] = useState<PlatformOverview | null>(null)
    const [directoryLoading, setDirectoryLoading] = useState(false)

    const [platformStatistics, setPlatformStatistics] = useState<CaseStatistics | null>(null)
    const [organizationStatistics, setOrganizationStatistics] = useState<CaseStatistics | null>(null)
    const [dashboardLoading, setDashboardLoading] = useState(false)

    const [applicationErrors, setApplicationErrors] = useState<ApplicationError[]>([])
    const [errorsLoading, setErrorsLoading] = useState(false)

    const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([])
    const [auditFilters, setAuditFilters] = useState<AuditFilters>(emptyAuditFilters)
    const [auditPage, setAuditPage] = useState(1)
    const [auditTotalCount, setAuditTotalCount] = useState(0)
    const [auditTotalPages, setAuditTotalPages] = useState(1)
    const [auditLoading, setAuditLoading] = useState(false)

    const selectedCase = cases.find((item) => item.id === selectedCaseId) ?? null
    const activeOrganization = workspaceOrganizations.find(
        (item) => item.id === organizationId,
    ) ?? null

    // The API reports the roles the session actually carries. Every dashboard,
    // navigation entry, and data request below is derived from them so the UI
    // can never offer a surface the API would refuse.
    const citizenAccess = canUsePortal(currentUser, 'citizen')
    const organizationAccess = canUsePortal(currentUser, 'organization')
    const platformAdminAccess = canUsePortal(currentUser, 'platform-admin')

    const availableViews = useMemo<WorkspaceView[]>(() => {
        if (!currentUser) return []

        const views: WorkspaceView[] = []

        if (citizenAccess) {
            views.push('citizen-dashboard', 'citizen')
        }

        if (organizationAccess) {
            views.push('cases', 'staff', 'activity')
        }

        // Shared self-service profile for citizens and organization staff.
        // Platform administrators are excluded — the API refuses their identity.
        if (citizenAccess || organizationAccess) {
            views.push('profile')
        }

        if (platformAdminAccess) {
            views.push('platform-dashboard', 'organizations', 'platform-directory', 'platform-audit', 'platform-errors', 'platform-jobs')
        }

        if (organizationAccess) {
            views.push('organization-dashboard')
        }

        return views
    }, [currentUser, citizenAccess, organizationAccess, platformAdminAccess])

    // The view is derived from the roles the API reports, so a citizen can
    // never land on an organization-only surface, even if a stale selection
    // survives a sign-out or a role change.
    const activeView: WorkspaceView = availableViews.includes(viewSelection)
        ? viewSelection
        : (availableViews[0] ?? 'citizen-dashboard')

    const navigationItems = useMemo(() => {
        if (!currentUser) return []

        const items: { view: WorkspaceView; label: string; icon: IconName }[] = []

        if (citizenAccess) {
            items.push({view: 'citizen-dashboard', label: 'Dashboard', icon: 'dashboard'})
            items.push({view: 'citizen', label: 'Public intake', icon: 'intake'})
        }

        if (organizationAccess) {
            items.push({view: 'organization-dashboard', label: 'Dashboard', icon: 'dashboard'})
            items.push({view: 'cases', label: 'Cases', icon: 'cases'})
            items.push({view: 'staff', label: 'Staff & team', icon: 'staff'})
            items.push({view: 'activity', label: 'Activity log', icon: 'activity'})
        }

        if (citizenAccess || organizationAccess) {
            items.push({view: 'profile', label: 'My profile', icon: 'profile'})
        }

        if (platformAdminAccess) {
            items.push({view: 'platform-dashboard', label: 'Dashboard', icon: 'dashboard'})
            items.push({view: 'organizations', label: 'Organizations', icon: 'building'})
            items.push({view: 'platform-directory', label: 'Directory', icon: 'directory'})
            items.push({view: 'platform-audit', label: 'Audit log', icon: 'activity'})
            items.push({view: 'platform-errors', label: 'Errors', icon: 'alert'})
            items.push({view: 'platform-jobs', label: 'Background jobs', icon: 'jobs'})
        }

        return items
    }, [currentUser, citizenAccess, organizationAccess, platformAdminAccess])

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
    const organizationInvitations =
        invitationsData && invitationsData.organizationId === selectedOrganizationId
            ? invitationsData.items
            : []
    const staffInvitations =
        invitationsData && invitationsData.organizationId === organizationId
            ? invitationsData.items
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
        setRequestOrganizationId(null)

        if (!currentUser || !organizationAccess) return

        let mounted = true

        async function loadWorkspaceOrganizations() {
            setLoading(true)
            setError(null)

            try {
                const memberships = await requestJson<OrganizationWorkspaceOption[]>(
                    '/api/organizations/me',
                    undefined,
                    '',
                )

                if (!mounted) return

                setWorkspaceOrganizations(memberships)
                const firstMembership = memberships[0]
                if (firstMembership) {
                    setRequestOrganizationId(firstMembership.id)
                    setOrganizationId(firstMembership.id)
                } else {
                    setError('This account has no active organization membership. Contact an administrator.')
                }
            } catch (reason) {
                if (mounted) {
                    setError(reason instanceof Error
                        ? reason.message
                        : 'Your organization memberships could not be loaded. Retry by refreshing the workspace.')
                }
            } finally {
                if (mounted) setLoading(false)
            }
        }

        void loadWorkspaceOrganizations()

        return () => {
            mounted = false
        }
    }, [currentUser, organizationAccess, organizationRetryVersion])

    function selectWorkspaceOrganization(nextOrganizationId: string) {
        if (!workspaceOrganizations.some((item) => item.id === nextOrganizationId)) return

        setRequestOrganizationId(nextOrganizationId)
        setOrganizationId(nextOrganizationId)
        setCases([])
        setStaff([])
        setWorkspaceMemberships([])
        setSelectedCaseId(null)
        setCasePage(1)
        setInvitationsData(null)
        setError(null)
    }

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
                setCaseTotalCount(caseResult.totalCount)
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
    }, [currentUser, organizationAccess, organizationId, casePage, retryVersion])

    useEffect(() => {
        if (!currentUser || !organizationAccess || !selectedCase || selectedCase.id.startsWith('demo-')) return

        let mounted = true
        const caseId = selectedCase.id

        void requestJson<HistoryEntry[]>(`/api/cases/${caseId}/history`)
            .then((result) => {
                if (mounted) {
                    setCaseHistoryData({caseId, items: result})
                }
            })
            .catch((reason: unknown) => {
                if (mounted) setError(reason instanceof Error ? reason.message : 'Case history could not be loaded.')
            })

        void requestJson<AssignmentEntry[]>(`/api/cases/${caseId}/assignment-history`)
            .then((result) => {
                if (mounted) {
                    setAssignmentHistoryData({caseId, items: result})
                }
            })
            .catch((reason: unknown) => {
                if (mounted) setError(reason instanceof Error ? reason.message : 'Assignment history could not be loaded.')
            })

        void requestJson<CaseDocument[]>(`/api/cases/${caseId}/documents`)
            .then((result) => {
                if (mounted) {
                    setCaseDocumentsData({caseId, items: result})
                }
            })
            .catch((reason: unknown) => {
                if (mounted) setError(reason instanceof Error ? reason.message : 'Case documents could not be loaded.')
            })

        return () => {
            mounted = false
        }
    }, [currentUser, organizationAccess, organizationId, selectedCase, retryVersion])

    useEffect(() => {
        if (!currentUser || !platformAdminAccess || activeView !== 'organizations') return

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
    }, [currentUser, platformAdminAccess, activeView, retryVersion])

    useEffect(() => {
        if (!currentUser || !platformAdminAccess || activeView !== 'organizations' || !selectedOrganizationId) return

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
    }, [currentUser, platformAdminAccess, activeView, selectedOrganizationId, retryVersion])

    useEffect(() => {
        if (!currentUser || !platformAdminAccess || activeView !== 'platform-directory') return

        let mounted = true

        async function loadDirectory() {
            setDirectoryLoading(true)

            try {
                const [citizens, staff, overview, loadedOrganizations] = await Promise.all([
                    requestJson<PlatformCitizen[]>('/api/platform/citizens'),
                    requestJson<PlatformStaff[]>('/api/platform/staff'),
                    requestJson<PlatformOverview>('/api/platform/overview'),
                    requestJson<Organization[]>('/api/organizations'),
                ])

                if (!mounted) return

                setPlatformCitizens(citizens)
                setPlatformStaff(staff)
                setPlatformOverview(overview)
                setOrganizations(loadedOrganizations)
            } catch (reason) {
                if (mounted) {
                    setError(
                        reason instanceof Error
                            ? reason.message
                            : 'The platform directory could not be loaded. Your role may not permit platform administration.',
                    )
                }
            } finally {
                if (mounted) setDirectoryLoading(false)
            }
        }

        void loadDirectory()

        return () => {
            mounted = false
        }
    }, [currentUser, platformAdminAccess, activeView, retryVersion])

    // Load platform dashboard statistics
    useEffect(() => {
        if (!currentUser || !platformAdminAccess || activeView !== 'platform-dashboard') return

        let mounted = true

        async function loadPlatformStatistics() {
            setDashboardLoading(true)
            setPlatformStatistics(null)
            setError(null)

            try {
                const [statistics, overview] = await Promise.all([
                    requestJson<CaseStatistics>('/api/dashboard/platform'),
                    requestJson<PlatformOverview>('/api/platform/overview'),
                ])

                if (!mounted) return

                setPlatformStatistics(statistics)
                setPlatformOverview(overview)
            } catch (reason) {
                if (mounted) {
                    setError(
                        reason instanceof Error
                            ? reason.message
                            : 'Platform statistics could not be loaded.',
                    )
                }
            } finally {
                if (mounted) setDashboardLoading(false)
            }
        }

        void loadPlatformStatistics()

        return () => {
            mounted = false
        }
    }, [currentUser, platformAdminAccess, activeView, retryVersion])

    // Load organization dashboard statistics
    useEffect(() => {
        if (!currentUser || !organizationAccess || activeView !== 'organization-dashboard' || !organizationId) return

        let mounted = true

        async function loadOrganizationStatistics() {
            setDashboardLoading(true)
            setOrganizationStatistics(null)
            setError(null)

            try {
                const statistics = await requestJson<CaseStatistics>(`/api/dashboard/organization?organizationId=${organizationId}`)

                if (!mounted) return

                setOrganizationStatistics(statistics)
            } catch (reason) {
                if (mounted) {
                    setError(
                        reason instanceof Error
                            ? reason.message
                            : 'Organization statistics could not be loaded.',
                    )
                }
            } finally {
                if (mounted) setDashboardLoading(false)
            }
        }

        void loadOrganizationStatistics()

        return () => {
            mounted = false
        }
    }, [currentUser, organizationAccess, activeView, organizationId, retryVersion])

    // Load application errors
    useEffect(() => {
        if (!currentUser || !platformAdminAccess || activeView !== 'platform-errors') return

        let mounted = true

        async function loadErrors() {
            setErrorsLoading(true)

            try {
                const errors = await requestJson<ApplicationError[]>('/api/errors')

                if (!mounted) return

                setApplicationErrors(errors)
            } catch (reason) {
                if (mounted) {
                    setError(
                        reason instanceof Error
                            ? reason.message
                            : 'Application errors could not be loaded.',
                    )
                }
            } finally {
                if (mounted) setErrorsLoading(false)
            }
        }

        void loadErrors()

        return () => {
            mounted = false
        }
    }, [currentUser, platformAdminAccess, activeView, retryVersion])

    // Organization activity is actor-scoped for staff; the API enforces access.
    const auditScope = activeView === 'platform-audit'
        ? 'platform'
        : activeView === 'activity'
            ? 'organization'
            : null

    const auditOrganizationId = organizationId ?? ''

    useEffect(() => {
        if (!currentUser || !auditScope) return
        if (auditScope === 'organization' && !auditOrganizationId) return

        let mounted = true

        async function loadAuditEvents() {
            setAuditLoading(true)

            const query = new URLSearchParams({
                page: String(auditPage),
                pageSize: '25',
            })

            if (auditFilters.category) query.set('category', auditFilters.category)
            if (auditFilters.action) query.set('action', auditFilters.action)
            if (auditFilters.search) query.set('search', auditFilters.search)
            if (auditFilters.from) query.set('from', `${auditFilters.from}T00:00:00Z`)
            if (auditFilters.to) query.set('to', `${auditFilters.to}T23:59:59Z`)

            const path = auditScope === 'platform'
                ? `/api/platform/audit?${query.toString()}`
                : `/api/organizations/${auditOrganizationId}/audit?${query.toString()}`

            try {
                const page = await requestJson<{
                    items: AuditEvent[]
                    totalCount: number
                    totalPages: number
                }>(path)

                if (!mounted) return

                setAuditEvents(page.items)
                setAuditTotalCount(page.totalCount)
                setAuditTotalPages(page.totalPages)
            } catch (reason) {
                if (mounted) {
                    setError(
                        reason instanceof Error
                            ? reason.message
                            : 'The audit trail could not be loaded. Your role may not permit this view.',
                    )
                }
            } finally {
                if (mounted) setAuditLoading(false)
            }
        }

        void loadAuditEvents()

        return () => {
            mounted = false
        }
    }, [
        currentUser,
        auditScope,
        auditOrganizationId,
        auditPage,
        auditFilters,
        retryVersion,
    ])

    const invitationsOrganizationId =
        activeView === 'organizations'
            ? selectedOrganizationId
            : activeView === 'staff' &&
            activeOrganization?.role === 'OrganizationAdmin'
                ? organizationId ?? ''
                : ''

    useEffect(() => {
        if (!currentUser || !invitationsOrganizationId) return
        if (activeView === 'organizations' && !platformAdminAccess) return
        if (activeView === 'staff' && !organizationAccess) return

        let mounted = true
        const targetOrganizationId = invitationsOrganizationId

        void requestJson<AccountInvitation[]>(
            `/api/organizations/${targetOrganizationId}/invitations`,
            undefined,
            targetOrganizationId,
        )
            .then((loaded) => {
                if (mounted) {
                    setInvitationsData({
                        organizationId: targetOrganizationId,
                        items: loaded,
                    })
                }
            })
            .catch((reason: unknown) => {
                if (mounted) {
                    setError(
                        reason instanceof Error
                            ? reason.message
                            : 'Invitations could not be loaded.',
                    )
                }
            })

        return () => {
            mounted = false
        }
    }, [
        currentUser,
        activeView,
        platformAdminAccess,
        organizationAccess,
        invitationsOrganizationId,
        retryVersion,
    ])

    useEffect(() => {
        if (!currentUser ||
            !organizationAccess ||
            activeView !== 'staff' ||
            !organizationId ||
            activeOrganization?.role !== 'OrganizationAdmin') {
            return
        }

        let mounted = true
        const targetOrganizationId = organizationId

        void requestJson<Membership[]>(
            `/api/organizations/${targetOrganizationId}/memberships`,
            undefined,
            targetOrganizationId,
        )
            .then((loaded) => {
                if (mounted) setWorkspaceMemberships(loaded)
            })
            .catch((reason: unknown) => {
                if (mounted) {
                    setError(reason instanceof Error
                        ? reason.message
                        : 'Organization memberships could not be loaded.')
                }
            })

        return () => {
            mounted = false
        }
    }, [
        currentUser,
        organizationAccess,
        activeView,
        organizationId,
        activeOrganization?.role,
        retryVersion,
    ])

    async function updateWorkspaceMembership(
        membership: Membership,
        role?: string,
    ) {
        if (!organizationId || activeOrganization?.role !== 'OrganizationAdmin') return

        const path = role
            ? `/api/organizations/${organizationId}/memberships/${membership.id}/role`
            : `/api/organizations/${organizationId}/memberships/${membership.id}/${membership.isActive ? 'deactivate' : 'reactivate'}`

        setAdminLoading(true)
        setError(null)

        try {
            const updated = await requestJson<Membership>(
                path,
                role
                    ? {method: 'PUT', body: JSON.stringify({role})}
                    : {method: 'PATCH'},
                organizationId,
            )

            setWorkspaceMemberships((current) =>
                current.map((item) => item.id === updated.id ? updated : item))
            setRetryVersion((version) => version + 1)
            showToast('Membership updated.', 'success')
        } catch (reason) {
            const message = reason instanceof Error
                ? reason.message
                : 'The membership could not be updated.'
            setError(message)
            showToast(message, 'error')
        } finally {
            setAdminLoading(false)
        }
    }

    async function inviteStaff(
        organizationIdForRequest: string,
        fullName: string,
        email: string,
        phoneNumber: string,
        role: string,
    ): Promise<boolean> {
        if (!currentUser || !organizationIdForRequest) return false

        setAdminLoading(true)
        setError(null)

        try {
            const created = await requestJson<AccountInvitation>(
                `/api/organizations/${organizationIdForRequest}/invitations`,
                {
                    method: 'POST',
                    body: JSON.stringify({
                        fullName,
                        email,
                        phoneNumber: phoneNumber || null,
                        role,
                    }),
                },
                organizationIdForRequest,
            )

            setInvitationsData((current) => ({
                organizationId: organizationIdForRequest,
                items: current?.organizationId === organizationIdForRequest
                    ? [created, ...current.items]
                    : [created],
            }))
            showToast('Invitation sent.', 'success')
            return true
        } catch (reason) {
            const message = reason instanceof Error
                ? reason.message
                : 'The invitation could not be sent.'
            setError(message)
            showToast(message, 'error')
            return false
        } finally {
            setAdminLoading(false)
        }
    }

    async function resendInvitation(
        organizationIdForRequest: string,
        invitationId: string,
    ) {
        if (!currentUser || !organizationIdForRequest) return

        setAdminLoading(true)
        setError(null)

        try {
            const updated = await requestJson<AccountInvitation>(
                `/api/organizations/${organizationIdForRequest}/invitations/${invitationId}/resend`,
                {method: 'POST'},
                organizationIdForRequest,
            )

            setInvitationsData((current) => ({
                organizationId: organizationIdForRequest,
                items: current?.organizationId === organizationIdForRequest
                    ? current.items.map((item) => item.id === updated.id ? updated : item)
                    : [updated],
            }))
            showToast('Invitation resent.', 'success')
        } catch (reason) {
            const message = reason instanceof Error
                ? reason.message
                : 'The invitation could not be resent.'
            setError(message)
            showToast(message, 'error')
        } finally {
            setAdminLoading(false)
        }
    }

    async function revokeInvitation(
        organizationIdForRequest: string,
        invitationId: string,
    ) {
        if (!currentUser || !organizationIdForRequest) return

        setAdminLoading(true)
        setError(null)

        try {
            await requestJson<void>(
                `/api/organizations/${organizationIdForRequest}/invitations/${invitationId}`,
                {method: 'DELETE'},
                organizationIdForRequest,
            )

            setInvitationsData((current) => ({
                organizationId: organizationIdForRequest,
                items: current?.organizationId === organizationIdForRequest
                    ? current.items.filter((item) => item.id !== invitationId)
                    : [],
            }))
            showToast('Invitation revoked.', 'info')
        } catch (reason) {
            const message = reason instanceof Error
                ? reason.message
                : 'The invitation could not be revoked.'
            setError(message)
            showToast(message, 'error')
        } finally {
            setAdminLoading(false)
        }
    }

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
        setViewSelection(
            portal === 'citizen'
                ? 'citizen-dashboard'
                : portal === 'platform-admin'
                    ? 'organizations'
                    : 'cases',
        )
        setLoginError(null)
        setLoginErrorCode(null)
        setError(null)
        rememberPortal(portal)
        navigateTo(WORKSPACE_PATH)
        setRoute('workspace')
        showToast('Signed in successfully.', 'success')
    }

    const handleLoginSubmit = async (
        {email, password}: LoginCredentials,
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

    const handlePlatformAdminLoginSubmit = (credentials: LoginCredentials) =>
        handleLoginSubmit(credentials, 'platform-admin')

    const handleSignOut = () => {
        void logoutRequest()
        setRequestOrganizationId(null)
        setCurrentUser(null)
        setOrganizationId(null)
        setWorkspaceOrganizations([])
        setWorkspaceMemberships([])
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
            setRetryVersion((version) => version + 1)
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
                    body: JSON.stringify({newStatus: status}),
                },
            )

            setCases((current) => current.map((item) => (
                item.id === updated.id ? {...item, status: updated.status} : item
            )))

            const loadedHistory = await requestJson<HistoryEntry[]>(
                `/api/cases/${caseId}/history`,
            )

            setCaseHistoryData({caseId, items: loadedHistory})
            setRetryVersion((version) => version + 1)
            showToast('Status updated.', 'success')
        } catch (reason) {
            const message = reason instanceof Error ? reason.message : 'Status update failed. Your role may not permit this action.'
            setError(message)
            showToast(message, 'error')
        } finally {
            setActionLoading(false)
        }
    }

    async function updateCaseDetails(title: string, description: string): Promise<boolean> {
        if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return false

        const caseId = selectedCase.id
        setActionLoading(true)
        setError(null)

        try {
            const updated = await requestJson<CaseRecord>(`/api/cases/${caseId}`, {
                method: 'PUT',
                body: JSON.stringify({title, description}),
            })
            setCases((current) => current.map((item) => (
                item.id === caseId
                    ? {...item, title: updated.title, description: updated.description}
                    : item
            )))
            setRetryVersion((version) => version + 1)
            showToast('Case details saved.', 'success')
            return true
        } catch (reason) {
            const message = reason instanceof Error
                ? reason.message
                : 'Case details could not be saved.'
            setError(message)
            showToast(message, 'error')
            return false
        } finally {
            setActionLoading(false)
        }
    }

    async function deleteCase() {
        if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

        const caseId = selectedCase.id
        setActionLoading(true)
        setError(null)

        try {
            await requestJson<void>(`/api/cases/${caseId}`, {
                method: 'DELETE',
                body: JSON.stringify({confirmed: true}),
            })
            setCases((current) => current.filter((item) => item.id !== caseId))
            setSelectedCaseId(null)
            setRetryVersion((version) => version + 1)
            showToast('Case deleted.', 'success')
        } catch (reason) {
            const message = reason instanceof Error
                ? reason.message
                : 'Case could not be deleted.'
            setError(message)
            showToast(message, 'error')
        } finally {
            setActionLoading(false)
        }
    }

    async function assignCase(
        staffMemberId: string,
        role: 'CaseWorker' | 'Viewer',
    ) {
        if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-') || !staffMemberId) return

        setActionLoading(true)
        setError(null)

        try {
            await requestJson(`/api/cases/${selectedCase.id}/assignment`, {
                method: 'PATCH',
                body: JSON.stringify({staffMemberId, role}),
            })
            setRetryVersion((version) => version + 1)
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
                {method: 'POST', body: formData},
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

    async function setStaffActive(member: StaffMember) {
        if (!currentUser || !organizationId) return
        if (!window.confirm(`${member.isActive ? 'Deactivate' : 'Reactivate'} ${member.fullName}?`)) return

        setAdminLoading(true)
        setError(null)

        try {
            const result = await requestJson<StaffMember>(
                `/api/staff/${member.id}/${member.isActive ? 'deactivate' : 'reactivate'}`,
                {method: 'PATCH'},
            )

            setStaff((current) => current.map((item) => item.id === result.id ? result : item))
            setRetryVersion((version) => version + 1)
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : 'Staff status change failed.')
        } finally {
            setAdminLoading(false)
        }
    }

    async function updateStaffPhone(member: StaffMember, phoneNumber: string) {
        if (!currentUser || !organizationId) return

        setAdminLoading(true)
        setError(null)

        try {
            const updated = await requestJson<StaffMember>(
                `/api/staff/${member.id}/phone`,
                {
                    method: 'PATCH',
                    body: JSON.stringify({phoneNumber: phoneNumber || null}),
                },
            )

            setStaff((current) => current.map((item) => item.id === updated.id ? updated : item))
            setRetryVersion((version) => version + 1)
            showToast('Phone number updated.', 'success')
        } catch (reason) {
            const message = reason instanceof Error ? reason.message : 'Phone number update failed.'
            setError(message)
            showToast(message, 'error')
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
                ...(action === 'role' && role ? {body: JSON.stringify({role})} : {}),
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

    if (route === 'setup-password') {
        return (
            <SetupPasswordPage
                onBack={handleBackToLanding}
                onSignIn={handleOrganizationSignIn}
            />
        )
    }

    if (!currentUser && route === 'platform-admin-login') {
        return (
            <PlatformAdminLoginPage
                onSubmit={handlePlatformAdminLoginSubmit}
                onBack={handleBackToLanding}
                onForgotPassword={handleForgotPassword}
                loading={loginLoading}
                error={loginError}
                errorCode={loginErrorCode}
            />
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

    // The organization workspace is only reachable for staff accounts. Citizen
    // and platform-administrator accounts that navigate to it are told why and
    // are returned to their own portal.
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
                    <section style={{maxWidth: 520, margin: '0 auto', width: '100%'}}>
                        <h2 style={{marginTop: 0}}>Organization workspace unavailable</h2>
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

    // The platform administration portal is only reachable for super
    // administrator accounts. Staff and citizen accounts that navigate to it
    // are told why and are returned to their own portal.
    if (route === 'platform-admin-login' && !platformAdminAccess) {
        return (
            <div className="landing-page">
                <header className="landing-header">
                    <div className="landing-brand">
                        <span className="landing-brand-mark">C</span>
                        <span className="landing-brand-name">CivicFlow</span>
                    </div>
                </header>
                <main className="landing-main">
                    <section style={{maxWidth: 520, margin: '0 auto', width: '100%'}}>
                        <h2 style={{marginTop: 0}}>Platform administration unavailable</h2>
                        <p className="muted">
                            This account is not a platform administrator. Platform
                            administration is limited to super administrator accounts; staff
                            use the organization portal and citizens use the citizen portal.
                        </p>
                        <div className="landing-card-actions">
                            <button
                                className="landing-button primary"
                                onClick={() => {
                                    navigateTo(WORKSPACE_PATH)
                                    setRoute('workspace')
                                }}
                                type="button"
                            >
                                Go to my workspace
                            </button>
                            <button className="landing-button" onClick={handleSignOut} type="button">
                                Sign out
                            </button>
                        </div>
                    </section>
                </main>
                <footer className="landing-footer">
                    <p>CivicFlow Enterprise · Platform administration</p>
                </footer>
            </div>
        )
    }

    const caseEmptyMessage = !organizationId
        ? 'Loading your organization workspace.'
        : cases.length === 0
            ? 'No cases are available in this organization.'
            : 'No cases match the current filter.'

    const dataSourceLabel = loading
        ? 'Loading from API'
        : !organizationId
            ? 'Resolving organization membership'
            : 'Live organization data'

    const workspaceLabel = activePortal === 'citizen'
        ? 'Citizen portal'
        : activePortal === 'platform-admin'
            ? 'Platform administration'
            : 'Organization workspace'

    const workspaceCaption = activePortal === 'citizen'
        ? currentUser.email
        : activeOrganization?.name ?? 'Organization membership required'

    const viewLabels: Record<WorkspaceView, string> = {
        'citizen-dashboard': 'Dashboard',
        citizen: 'Public intake',
        profile: 'My profile',
        cases: 'Cases',
        staff: 'Staff & team',
        activity: 'Activity log',
        'organization-dashboard': 'Dashboard',
        organizations: 'Organizations',
        'platform-dashboard': 'Dashboard',
        'platform-directory': 'Directory',
        'platform-audit': 'Audit log',
        'platform-errors': 'Errors',
        'platform-jobs': 'Background jobs',
    }

    return (
        <div className="app-shell">
            <a className="skip-link" href="#main-content">Skip to main content</a>
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
                {organizationAccess && workspaceOrganizations.length > 1 && (
                    <label className="organization-select-label">
                        Organization
                        <select
                            aria-label="Active organization"
                            value={organizationId ?? ''}
                            onChange={(event) => selectWorkspaceOrganization(event.target.value)}
                        >
                            {workspaceOrganizations.map((item) => (
                                <option key={item.id} value={item.id}>
                                    {item.name} ({item.role})
                                </option>
                            ))}
                        </select>
                    </label>
                )}

                <nav className="navigation" aria-label="Primary navigation">
                    {navigationItems.map((item) => (
                        <button
                            aria-current={activeView === item.view ? 'page' : undefined}
                            data-tooltip={item.label}
                            title={item.label}
                            className={activeView === item.view ? 'active' : ''}
                            key={item.view}
                            onClick={() => setViewSelection(item.view)}
                            type="button"
                        >
                            <Icon aria-hidden="true" className="nav-icon" name={item.icon} />
                            <span>{item.label}</span>
                            {item.view === 'cases' && caseTotalCount > 0 && <b>{caseTotalCount}</b>}
                        </button>
                    ))}
                </nav>

                <div className="sidebar-footer">
                    <button className="link-button" onClick={handleBackToLanding}
                            type="button"><Icon name="home"/> <span>Back to landing</span></button>
                    <div className="profile">
                        <span className="avatar">CF</span>
                        <span>
              <strong>{currentUser ? currentUser.fullName : 'Not signed in'}</strong>
              <small>{currentUser ? currentUser.email : 'Sign in to continue'}</small>
            </span>
                    </div>
                </div>
            </aside>

            <main className="main-content" id="main-content">
                <header className="topbar">
                    <div className="breadcrumb">
                        <span>{workspaceLabel}</span>
                        <span>/</span>
                        <strong>{viewLabels[activeView]}</strong>
                    </div>

                    <div className="topbar-actions">
                        <NotificationCenter
                            onOpenCase={organizationAccess
                                ? (caseId) => {
                                    setViewSelection('cases')
                                    setSelectedCaseId(caseId)
                                }
                                : undefined}
                        />
                        <button className="auth-button" onClick={handleSignOut} type="button">
                            Sign out
                        </button>
                    </div>
                </header>

                <section className="content">
                    {activeView === 'platform-jobs' && platformAdminAccess ? (
                        <BackgroundJobsPage/>
                    ) : (
                        <>
                            {error && (
                                <div className="auth-error" role="alert">
                                    {error}
                                    <button
                                        className="secondary-button"
                                        onClick={() => {
                                            setRetryVersion((version) => version + 1)
                                            if (!organizationId) {
                                                setOrganizationRetryVersion((version) => version + 1)
                                            }
                                        }}
                                        type="button"
                                    >
                                        Retry
                                    </button>
                                </div>
                            )}

                            {activeView === 'citizen-dashboard' ? (
                                <CitizenDashboard
                                    fullName={currentUser.fullName}
                                    email={currentUser.email}
                                    emailConfirmed={currentUser.emailConfirmed}
                                    onOpenIntake={() => setViewSelection('citizen')}
                                    onOpenProfile={() => setViewSelection('profile')}
                                />
                            ) : activeView === 'citizen' ? (
                                <CitizenIntake/>
                            ) : activeView === 'profile' ? (
                                <ProfilePage/>
                            ) : activeView === 'staff' && organizationAccess ? (
                                <StaffDirectory
                                    staff={staff}
                                    invitations={staffInvitations}
                                    memberships={workspaceMemberships}
                                    canManage={activeOrganization?.role === 'OrganizationAdmin'}
                                    authenticated={Boolean(currentUser)}
                                    loading={adminLoading}
                                    onInvite={(fullName, email, phoneNumber, role) =>
                                        inviteStaff(organizationId ?? '', fullName, email, phoneNumber, role)}
                                    onResend={(invitationId) =>
                                        resendInvitation(organizationId ?? '', invitationId)}
                                    onRevoke={(invitationId) =>
                                        revokeInvitation(organizationId ?? '', invitationId)}
                                    onToggle={setStaffActive}
                                    onUpdatePhone={updateStaffPhone}
                                    onUpdateMembership={(membership, role) =>
                                        updateWorkspaceMembership(membership, role)}
                                    onToggleMembership={(membership) =>
                                        updateWorkspaceMembership(membership)}
                                />
                            ) : activeView === 'activity' && organizationAccess ? (
                                <AuditLog
                                    title={
                                        activeOrganization?.role === 'OrganizationAdmin'
                                            ? 'Organization activity'
                                            : 'My activity'
                                    }
                                    description={
                                        activeOrganization?.role === 'OrganizationAdmin'
                                            ? 'Audited staff, case and workflow events recorded across your organization.'
                                            : 'Your own audited actions recorded in this organization.'
                                    }
                                    loading={auditLoading}
                                    events={auditEvents}
                                    filters={auditFilters}
                                    totalCount={auditTotalCount}
                                    totalPages={auditTotalPages}
                                    page={auditPage}
                                    onApplyFilters={(filters) => {
                                        setAuditFilters(filters)
                                        setAuditPage(1)
                                    }}
                                    onPageChange={setAuditPage}
                                    emptyMessage={
                                        activeOrganization?.role === 'OrganizationAdmin'
                                            ? 'No activity has been recorded for this organization yet.'
                                            : 'You have no recorded activity in this organization yet.'
                                    }
                                />
                            ) : activeView === 'platform-dashboard' && platformAdminAccess ? (
                                <PlatformDashboard
                                    statistics={platformStatistics}
                                    overview={platformOverview}
                                    loading={dashboardLoading}
                                />
                            ) : activeView === 'organization-dashboard' && organizationAccess ? (
                                <OrganizationDashboard
                                    statistics={organizationStatistics}
                                    loading={dashboardLoading}
                                    organizationName={activeOrganization?.name ?? 'Organization'}
                                />
                            ) : activeView === 'platform-directory' && platformAdminAccess ? (
                                <PlatformDirectory
                                    loading={directoryLoading}
                                    overview={platformOverview}
                                    citizens={platformCitizens}
                                    staff={platformStaff}
                                    organizations={organizations}
                                    refreshData={() => setRetryVersion((version) => version + 1)}
                                />
                            ) : activeView === 'platform-errors' && platformAdminAccess ? (
                                <PlatformErrors
                                    errors={applicationErrors}
                                    loading={errorsLoading}
                                    onMarkAsHandled={async (errorId) => {
                                        try {
                                            await requestJson(`/api/errors/${errorId}/mark-handled`, {method: 'PATCH'})
                                            setApplicationErrors(applicationErrors.map(e => e.id === errorId ? {
                                                ...e,
                                                isHandled: true
                                            } : e))
                                        } catch (reason) {
                                            setError(reason instanceof Error ? reason.message : 'Failed to mark error as handled')
                                        }
                                    }}
                                />
                            ) : activeView === 'platform-audit' && platformAdminAccess ? (
                                <AuditLog
                                    title="Platform audit trail"
                                    description="Every audited change recorded across the platform, including organization, staff, invitation, access-token and authentication events."
                                    loading={auditLoading}
                                    events={auditEvents}
                                    filters={auditFilters}
                                    totalCount={auditTotalCount}
                                    totalPages={auditTotalPages}
                                    page={auditPage}
                                    onApplyFilters={(filters) => {
                                        setAuditFilters(filters)
                                        setAuditPage(1)
                                    }}
                                    onPageChange={setAuditPage}
                                    emptyMessage="No audit events match the current filters."
                                />
                            ) : activeView === 'organizations' && platformAdminAccess ? (
                                <OrganizationAdministration
                                    authenticated={Boolean(currentUser)}
                                    loading={adminLoading}
                                    organizations={organizations}
                                    memberships={memberships}
                                    invitations={organizationInvitations}
                                    selectedOrganizationId={selectedOrganizationId}
                                    onSelectOrganization={setSelectedOrganizationId}
                                    refreshData={() => setRetryVersion((version) => version + 1)}
                                    onUpdateMembership={updateMembership}
                                    onInvite={(fullName, email, phoneNumber, role) =>
                                        inviteStaff(selectedOrganizationId, fullName, email, phoneNumber, role)}
                                    onResendInvitation={(invitationId) =>
                                        resendInvitation(selectedOrganizationId, invitationId)}
                                    onRevokeInvitation={(invitationId) =>
                                        revokeInvitation(selectedOrganizationId, invitationId)}
                                />
                            ) : organizationAccess ? (
                                <>
                                    <div className="page-heading">
                                        <div>
                                            <p className="eyebrow">OPERATIONS OVERVIEW</p>
                                            <h1>Case management</h1>
                                            <p className="muted">Review, assign, and move public-service cases
                                                forward.</p>
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
                                                <button className="primary-button" disabled={actionLoading}
                                                        type="submit">
                                                    {actionLoading ? 'Saving…' : 'Create case'}
                                                </button>
                                                <button className="secondary-button"
                                                        onClick={() => setShowCreateForm(false)} type="button">
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
                                            <span
                                                className="muted">Showing {visibleCases.length} of {cases.length}</span>
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
                                                        <option key={status}
                                                                value={status}>{statusLabels[status]}</option>
                                                    ))}
                                                </select>
                                            </div>

                                            {loading ? (
                                                <p className="muted empty-state">Loading cases…</p>
                                            ) : visibleCases.length === 0 ? (
                                                <p className="muted empty-state">{caseEmptyMessage}</p>
                                            ) : (
                                                <div aria-label="Cases list" className="table-wrap" role="region"
                                                     tabIndex={0}>
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
                                                            >
                                                                <td>
                                                                    <button
                                                                        aria-pressed={selectedCase?.id === item.id}
                                                                        className="case-row-button"
                                                                        onClick={() => setSelectedCaseId(item.id)}
                                                                        type="button"
                                                                    >
                                                                        <strong>{item.referenceNumber}</strong>
                                                                        <span>{item.title}</span>
                                                                    </button>
                                                                </td>
                                                                <td><StatusPill status={item.status}/></td>
                                                                <td>
                                <span className={`priority ${item.priority.toLowerCase()}`}>
                                  <i/>{item.priority}
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
                                            canAssignCases={activeOrganization?.role === 'OrganizationAdmin'}
                                            canEditCase={
                                                activeOrganization?.role === 'OrganizationAdmin' ||
                                                (activeOrganization?.role === 'CaseWorker' &&
                                                    !!selectedCase &&
                                                    selectedCase.assignedRole === 'CaseWorker' &&
                                                    staff.some((member) =>
                                                        member.id === selectedCase.assignedToStaffMemberId &&
                                                        member.email.toLowerCase() === currentUser.email.toLowerCase(),
                                                    ))
                                            }
                                            canDeleteCase={activeOrganization?.role === 'OrganizationAdmin'}
                                            actionLoading={actionLoading}
                                            onStatusChange={changeStatus}
                                            onUpdate={updateCaseDetails}
                                            onDelete={deleteCase}
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
                                    onOpenProfile={() => setViewSelection('profile')}
                                />
                            )}
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
