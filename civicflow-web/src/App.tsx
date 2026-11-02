import {useEffect, useMemo, useState, type FormEvent} from 'react'
import {apiRequest} from './api'
import {
    AuthApiError,
    canUsePortal,
    getCurrentUser,
    login as loginRequest,
    logout as logoutRequest,
    type AuthUser,
    type SignInPortal,
} from './auth'
import './App.css'
import { formatDateTime, formatNumber } from './utils/format'
import {
    MAX_CASE_DESCRIPTION_LENGTH,
    MAX_CASE_TITLE_LENGTH,
    MIN_CASE_DESCRIPTION_LENGTH,
    MIN_CASE_TITLE_LENGTH,
    normalizeMultiline,
    normalizeWhitespace,
    validateCaseDescription,
    validateCaseTitle,
} from './utils/validation'
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
    type CasePriority,
    type CaseRecord,
    type CaseStatistics,
    type CaseStatus,
    type CaseUpdate,
    type CaseUpdateKind,
    type CaseUpdatesData,
    type HistoryEntry,
    type Membership,
    type MembershipsData,
    type Organization,
    type OrganizationWorkspaceOption,
    type PlatformOverview,
    type PlatformOrganizationCaseBreakdown,
    type PlatformCitizen,
    type PlatformUser,
    type PagedResponse,
    type StaffMember,
    type WorkspaceView,
} from './domain'
import {requestJson, setRequestOrganizationId} from './api-client'
import {StatusPill} from './components/StatusPill'
import {StatCard} from './components/StatCard'
import {CaseDetails} from './components/CaseDetails'
import {StaffDirectory} from './components/StaffDirectory'
import {OrganizationAdministration} from './components/OrganizationAdministration'
import {MyOrganization} from './components/MyOrganization'
import {PlatformAdminProfile} from './components/PlatformAdminProfile'
import {PlatformDirectory, type DirectoryQuery} from './components/PlatformDirectory'
import {AuditLog} from './components/AuditLog'
import {emptyAuditFilters, type AuditFilters} from './utils/audit'
import {CitizenIntake} from './components/CitizenIntake'
import {CitizenDashboard} from './components/CitizenDashboard'
import {PlatformDashboard} from './components/PlatformDashboard'
import {PlatformCases} from './components/PlatformCases'
import {OrganizationDashboard} from './components/OrganizationDashboard'
import {PlatformErrors} from './components/PlatformErrors'
import {NotificationCenter} from './components/NotificationCenter'
import {BackgroundJobsPage} from './components/BackgroundJobsPage'
import {AppFooter} from './components/PublicLayout'
import {AuthPage} from './components/AuthPage'
import {ProfileMenu} from './components/ProfileMenu'
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
import {useToast} from './components/Toast'
import {ConfirmationDialog} from './components/ConfirmationDialog'
import type {LoginCredentials} from './components/LoginForm'
import {useAppConfig} from './app-config'
import {
    FORGOT_PASSWORD_PATH,
    STAFF_LOGIN_PATH,
    VERIFY_EMAIL_PATH,
    WORKSPACE_PATH,
    CITIZEN_LOGIN_PATH,
    CITIZEN_REGISTER_PATH,
    forgetPortal,
    forgetWorkspaceView,
    navigateTo,
    readRememberedWorkspaceView,
    rememberPortal,
    rememberWorkspaceView,
    resolveRoute,
    resolveSessionPortal,
    type AppRoute,
} from './routing'

function App() {
    const {showToast} = useToast()
    const {applicationName} = useAppConfig()
    const [currentUser, setCurrentUser] = useState<AuthUser | null>(null)
    const [sessionChecked, setSessionChecked] = useState(false)
    const [cases, setCases] = useState<CaseRecord[]>([])
    const [selectedCaseId, setSelectedCaseId] = useState<string | null>(null)
    const [caseWorkspacePanel, setCaseWorkspacePanel] = useState<'list' | 'details'>('list')
    const [caseHasUnsavedChanges, setCaseHasUnsavedChanges] = useState(false)
    const [pendingUnsavedNavigation, setPendingUnsavedNavigation] = useState<{
        caseId?: string
        panel?: 'list' | 'details'
        view?: WorkspaceView
        signOut?: boolean
    } | null>(null)

    const [caseHistoryData, setCaseHistoryData] = useState<CaseHistoryData | null>(null)
    const [caseUpdatesData, setCaseUpdatesData] = useState<CaseUpdatesData | null>(null)
    const [assignmentHistoryData, setAssignmentHistoryData] = useState<AssignmentHistoryData | null>(null)
    const [caseDocumentsData, setCaseDocumentsData] = useState<CaseDocumentsData | null>(null)

    const [staff, setStaff] = useState<StaffMember[]>([])
    const [staffDirectoryPage, setStaffDirectoryPage] = useState<PagedResponse<StaffMember>>({ items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })
    const [staffDirectoryQuery, setStaffDirectoryQuery] = useState({ page: 1, pageSize: 10, search: '', isActive: null as boolean | null })
    const [organizations, setOrganizations] = useState<Organization[]>([])
    const [organizationsPageInfo, setOrganizationsPageInfo] = useState({ page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })
    const [organizationsQuery, setOrganizationsQuery] = useState({ page: 1, pageSize: 10, search: '', organizationTypeId: '', isActive: null as boolean | null })
    const [workspaceOrganizations, setWorkspaceOrganizations] = useState<OrganizationWorkspaceOption[]>([])
    const [organizationId, setOrganizationId] = useState<string | null>(null)
    const [membershipsData, setMembershipsData] = useState<MembershipsData | null>(null)
    const [membershipsPageInfo, setMembershipsPageInfo] = useState({ page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })
    const [membershipsQuery, setMembershipsQuery] = useState({ page: 1, pageSize: 10, search: '', isActive: null as boolean | null })
    const [invitationsData, setInvitationsData] = useState<{
        organizationId: string
        items: AccountInvitation[]
    } | null>(null)
    const [invitationsPageInfo, setInvitationsPageInfo] = useState({ page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })
    const [invitationsQuery, setInvitationsQuery] = useState({ page: 1, pageSize: 10, search: '' })
    const [selectedOrganizationId, setSelectedOrganizationId] = useState('')
    const [selectedStatus, setSelectedStatus] = useState<'All' | CaseStatus>('All')
    const [selectedPriority, setSelectedPriority] = useState<'All' | CasePriority>('All')
    const [search, setSearch] = useState('')
    const [caseSearchInput, setCaseSearchInput] = useState('')
    const [loading, setLoading] = useState(false)
    const [retryVersion, setRetryVersion] = useState(0)
    const [organizationRetryVersion, setOrganizationRetryVersion] = useState(0)
    const [actionLoading, setActionLoading] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [showCreateForm, setShowCreateForm] = useState(false)
    const [newTitle, setNewTitle] = useState('')
    const [newDescription, setNewDescription] = useState('')
    const [caseFormSubmitted, setCaseFormSubmitted] = useState(false)
    const [viewSelection, setViewSelection] = useState<WorkspaceView>(
        () => readRememberedWorkspaceView() ?? 'citizen-dashboard',
    )
    const [activePortal, setActivePortal] = useState<SignInPortal | null>(null)
    const [citizenCasesVersion, setCitizenCasesVersion] = useState(0)
    const [citizenInitialCaseId, setCitizenInitialCaseId] = useState<string | null>(null)
    const [casePage, setCasePage] = useState(1)
    const [totalPages, setTotalPages] = useState(1)
    const [caseTotalCount, setCaseTotalCount] = useState(0)
    const [adminLoading, setAdminLoading] = useState(false)
    const [route, setRoute] = useState<AppRoute>(() => resolveRoute(window.location.pathname))
    const [loginLoading, setLoginLoading] = useState(false)
    const [loginError, setLoginError] = useState<string | null>(null)
    const [loginErrorCode, setLoginErrorCode] = useState<string | null>(null)

    const [platformCitizens, setPlatformCitizens] = useState<PlatformCitizen[]>([])
    const [platformUsers, setPlatformUsers] = useState<PlatformUser[]>([])
    const [directoryResult, setDirectoryResult] = useState({ page: 1, pageSize: 10, totalCount: 0, totalPages: 0 })
    const [directoryQuery, setDirectoryQuery] = useState<DirectoryQuery>({ tab: 'citizens', page: 1, pageSize: 10, search: '', accountFilter: 'all', sortField: 'fullName', sortOrder: 'asc' })
    const [platformOverview, setPlatformOverview] = useState<PlatformOverview | null>(null)
    const [directoryLoading, setDirectoryLoading] = useState(false)

    const [platformStatistics, setPlatformStatistics] = useState<CaseStatistics | null>(null)
    const [platformOrganizationBreakdown, setPlatformOrganizationBreakdown] =
        useState<PlatformOrganizationCaseBreakdown[]>([])
    const [organizationStatistics, setOrganizationStatistics] = useState<CaseStatistics | null>(null)
    const [dashboardLoading, setDashboardLoading] = useState(false)

    const [applicationErrors, setApplicationErrors] = useState<ApplicationError[]>([])
    const [errorsLoading, setErrorsLoading] = useState(false)
    const [errorPage, setErrorPage] = useState(1)
    const [errorTotalCount, setErrorTotalCount] = useState(0)
    const [errorTotalPages, setErrorTotalPages] = useState(1)
    const [errorFilter, setErrorFilter] = useState<'all' | 'unhandled' | 'handled'>('all')

    const [auditEvents, setAuditEvents] = useState<AuditEvent[]>([])
    const [auditFilters, setAuditFilters] = useState<AuditFilters>(emptyAuditFilters)
    const [auditPage, setAuditPage] = useState(1)
    const [auditTotalCount, setAuditTotalCount] = useState(0)
    const [auditTotalPages, setAuditTotalPages] = useState(1)
    const [auditLoading, setAuditLoading] = useState(false)

    const selectedCase = cases.find((item) => item.id === selectedCaseId) ?? null
    function navigateToCase(caseId: string) {
        if (caseHasUnsavedChanges) {
            setPendingUnsavedNavigation({caseId, panel: 'details', view: 'cases'})
            return
        }

        setViewSelection('cases')
        setSelectedCaseId(caseId)
        setCaseWorkspacePanel('details')
    }

    function navigateToPanel(panel: 'list' | 'details') {
        if (caseHasUnsavedChanges && panel !== caseWorkspacePanel) {
            setPendingUnsavedNavigation({panel})
            return
        }

        setCaseWorkspacePanel(panel)
    }

    function navigateToWorkspaceView(view: WorkspaceView) {
        if (caseHasUnsavedChanges && view !== activeView) {
            setPendingUnsavedNavigation({view})
            return
        }

        setViewSelection(view)
    }

    function navigateToSignOut() {
        if (caseHasUnsavedChanges) {
            setPendingUnsavedNavigation({signOut: true})
            return
        }

        void handleSignOut()
    }
    const activeOrganization = workspaceOrganizations.find(
        (item) => item.id === organizationId,
    ) ?? null

    // The API reports the roles the session actually carries. Every dashboard,
    // navigation entry, and data request below is derived from them so the UI
    // can never offer a surface the API would refuse.
    const citizenAccess = canUsePortal(currentUser, 'citizen')
    const organizationAccess = canUsePortal(currentUser, 'organization')
    const platformAdminAccess = canUsePortal(currentUser, 'platform-admin')
    const organizationAdminAccess = workspaceOrganizations.some(
        (organization) => organization.role === 'OrganizationAdmin',
    )

    const availableViews = useMemo<WorkspaceView[]>(() => {
        if (!currentUser) return []

        const views: WorkspaceView[] = []

        if (citizenAccess) {
            views.push('citizen-dashboard', 'citizen-cases', 'citizen')
        }

        if (organizationAccess) {
            views.push('cases', 'staff', 'activity')

            if (!platformAdminAccess) {
                views.push('organizations')
            }
        }

        // Citizens and organization staff use the self-service profile; platform
        // administrators receive a platform-specific account summary.
        if (citizenAccess || organizationAccess || platformAdminAccess) {
            views.push('profile')
        }

        if (platformAdminAccess) {
            views.push('platform-dashboard', 'platform-cases', 'organizations', 'platform-directory', 'platform-audit', 'platform-errors', 'platform-jobs')
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

    useEffect(() => {
        if (currentUser) rememberWorkspaceView(activeView)
    }, [activeView, currentUser])

    const navigationItems = useMemo(() => {
        if (!currentUser) return []

        const items: { view: WorkspaceView; label: string; icon: IconName }[] = []

        if (citizenAccess) {
            items.push({view: 'citizen-dashboard', label: 'Dashboard', icon: 'dashboard'})
            items.push({view: 'citizen-cases', label: 'My cases', icon: 'cases'})
            items.push({view: 'citizen', label: 'Public intake', icon: 'intake'})
        }

        if (organizationAccess) {
            items.push({view: 'organization-dashboard', label: 'Dashboard', icon: 'dashboard'})
            items.push({view: 'cases', label: 'Cases', icon: 'cases'})
            items.push({view: 'staff', label: 'Team contacts', icon: 'staff'})
            items.push({view: 'activity', label: 'Activity log', icon: 'activity'})

            if (!platformAdminAccess) {
                items.push({view: 'organizations', label: 'My Organization', icon: 'building'})
            }
        }

        if (platformAdminAccess) {
            items.push({view: 'platform-dashboard', label: 'Dashboard', icon: 'dashboard'})
            items.push({view: 'platform-cases', label: 'All cases', icon: 'cases'})
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

    const caseUpdates = caseUpdatesData && caseUpdatesData.caseId === selectedCase?.id
        ? caseUpdatesData.items
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

    useEffect(() => {
        const timer = window.setTimeout(() => {
            setSearch(caseSearchInput.trim())
            setCasePage(1)
        }, 300)

        return () => window.clearTimeout(timer)
    }, [caseSearchInput])

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
        setSelectedCaseId(null)
        setCasePage(1)
        setTotalPages(1)
        setCaseTotalCount(0)
        setSelectedStatus('All')
        setSelectedPriority('All')
        setSearch('')
        setCaseSearchInput('')
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
                    setActivePortal(resolveSessionPortal(user))
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

            const caseQuery = new URLSearchParams({
                page: String(casePage),
                pageSize: '20',
            })

            if (selectedStatus !== 'All') caseQuery.set('status', selectedStatus)
            if (selectedPriority !== 'All') caseQuery.set('priority', selectedPriority)
            if (search) caseQuery.set('search', search)

            try {
                const [caseResult, staffMembers] = await Promise.all([
                    requestJson<CasePage>(`/api/cases?${caseQuery.toString()}`),
                    requestJson<PagedResponse<StaffMember>>('/api/staff?page=1&pageSize=100'),
                ])

                if (!mounted) return

                setCases(caseResult.items)
                setTotalPages(Math.max(1, caseResult.totalPages))
                setCaseTotalCount(caseResult.totalCount)
                setStaff(staffMembers.items)
                setSelectedCaseId((current) => {
                    if (current && caseResult.items.some((item) => item.id === current)) {
                        return current
                    }

                    return caseResult.items[0]?.id ?? null
                })
            } catch (reason) {
                if (mounted) {
                    setError(reason instanceof Error ? reason.message : 'CivicFlow could not be reached. Check your connection and try again.')
                }
            } finally {
                if (mounted) setLoading(false)
            }
        }

        void loadWorkspace()

        return () => {
            mounted = false
        }
    }, [currentUser, organizationAccess, organizationId, casePage, selectedStatus, selectedPriority, search, retryVersion])

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

        void requestJson<CaseUpdate[]>(`/api/cases/${caseId}/updates`)
            .then((result) => {
                if (mounted) {
                    setCaseUpdatesData({caseId, items: result})
                }
            })
            .catch((reason: unknown) => {
                if (mounted) setError(reason instanceof Error ? reason.message : 'Case updates could not be loaded.')
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
        if (!currentUser ||
            (!platformAdminAccess && !organizationAdminAccess) ||
            activeView !== 'organizations') return

        let mounted = true

        async function loadOrganizations() {
            setAdminLoading(true)

            try {
                const query = new URLSearchParams({ page: String(organizationsQuery.page), pageSize: String(organizationsQuery.pageSize) })
                if (organizationsQuery.search.trim()) query.set('search', organizationsQuery.search.trim())
                if (organizationsQuery.organizationTypeId) query.set('organizationTypeId', organizationsQuery.organizationTypeId)
                if (organizationsQuery.isActive !== null) query.set('isActive', String(organizationsQuery.isActive))
                const loadedPage = await requestJson<PagedResponse<Organization>>(`/api/organizations?${query}`)
                const loaded = loadedPage.items

                if (!mounted) return

                setOrganizations(loaded)
                setOrganizationsPageInfo({ page: loadedPage.page, pageSize: loadedPage.pageSize, totalCount: loadedPage.totalCount, totalPages: loadedPage.totalPages })
                setSelectedOrganizationId((current) =>
                    loaded.some((organization) => organization.id === current)
                        ? current
                        : (current || loaded[0]?.id || ''))
            } catch (reason) {
                if (mounted) {
                    setError(reason instanceof Error ? reason.message : 'Organizations could not be loaded for your account.')
                }
            } finally {
                if (mounted) setAdminLoading(false)
            }
        }

        void loadOrganizations()

        return () => {
            mounted = false
        }
    }, [currentUser, platformAdminAccess, organizationAdminAccess, activeView, retryVersion, organizationsQuery])

    useEffect(() => {
        if (!currentUser ||
            (!platformAdminAccess && !organizationAdminAccess) ||
            activeView !== 'organizations' ||
            !selectedOrganizationId) return

        let mounted = true
        const currentOrganizationId = selectedOrganizationId

        const query = new URLSearchParams({ page: String(membershipsQuery.page), pageSize: String(membershipsQuery.pageSize) })
        if (membershipsQuery.search.trim()) query.set('search', membershipsQuery.search.trim())
        if (membershipsQuery.isActive !== null) query.set('isActive', String(membershipsQuery.isActive))
        void requestJson<PagedResponse<Membership>>(
            `/api/organizations/${currentOrganizationId}/memberships?${query}`,
            undefined,
            currentOrganizationId,
        )
            .then((loaded) => {
                if (mounted) {
                    setMembershipsData({
                        organizationId: currentOrganizationId,
                        items: loaded.items,
                    })
                    setMembershipsPageInfo({ page: loaded.page, pageSize: loaded.pageSize, totalCount: loaded.totalCount, totalPages: loaded.totalPages })
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
    }, [currentUser, platformAdminAccess, organizationAdminAccess, activeView, selectedOrganizationId, retryVersion, membershipsQuery])

    useEffect(() => {
        if (!currentUser || (!organizationAccess && !organizationAdminAccess) ||
            (activeView !== 'staff' && activeView !== 'organizations')) return
        let mounted = true
        const query = new URLSearchParams({ page: String(staffDirectoryQuery.page), pageSize: String(staffDirectoryQuery.pageSize) })
        if (staffDirectoryQuery.search.trim()) query.set('search', staffDirectoryQuery.search.trim())
        if (staffDirectoryQuery.isActive !== null) query.set('isActive', String(staffDirectoryQuery.isActive))

        async function loadStaffDirectory() {
            setAdminLoading(true)
            try {
                const result = await requestJson<PagedResponse<StaffMember>>(`/api/staff?${query}`)
                if (mounted) setStaffDirectoryPage(result)
            } catch (reason) {
                if (mounted) setError(reason instanceof Error ? reason.message : 'Staff could not be loaded.')
            } finally {
                if (mounted) setAdminLoading(false)
            }
        }

        void loadStaffDirectory()
        return () => { mounted = false }
    }, [currentUser, organizationAccess, organizationAdminAccess, activeView, staffDirectoryQuery, retryVersion])

    useEffect(() => {
        if (!currentUser || !platformAdminAccess || activeView !== 'platform-directory') return

        let mounted = true

        async function loadDirectory() {
            setDirectoryLoading(true)

            try {
                const params = new URLSearchParams({ page: String(directoryQuery.page), pageSize: String(directoryQuery.pageSize) })
                if (directoryQuery.search.trim()) params.set('search', directoryQuery.search.trim())
                const sortField = directoryQuery.sortField
                const sortKey = sortField === 'fullName'
                    ? 'name'
                    : sortField === 'createdAt'
                        ? 'created'
                        : sortField === 'accountType'
                            ? 'accountType'
                            : sortField
                const sort = `${sortKey}_${directoryQuery.sortOrder}`
                const [directoryPage, overview] = await Promise.all([
                    requestJson<PagedResponse<PlatformCitizen | PlatformUser>>(
                        directoryQuery.tab === 'citizens'
                            ? `/api/platform/citizens?${params}${sort ? `&sort=${sort}` : ''}`
                            : `/api/platform/users?${params}${directoryQuery.tab === 'staff' ? '&isStaff=true' : directoryQuery.accountFilter === 'staff' ? '&isStaff=true' : directoryQuery.accountFilter === 'citizens' ? '&isCitizen=true' : ''}${sort ? `&sort=${sort}` : ''}`),
                    requestJson<PlatformOverview>('/api/platform/overview'),
                ])

                if (!mounted) return

                if (directoryQuery.tab === 'citizens') {
                    setPlatformCitizens(directoryPage.items as PlatformCitizen[])
                    setPlatformUsers([])
                } else {
                    setPlatformUsers(directoryPage.items as PlatformUser[])
                    setPlatformCitizens([])
                }
                setDirectoryResult({ page: directoryPage.page, pageSize: directoryPage.pageSize, totalCount: directoryPage.totalCount, totalPages: directoryPage.totalPages })
                setPlatformOverview(overview)
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
    }, [currentUser, platformAdminAccess, activeView, retryVersion, directoryQuery])

    // Load platform dashboard statistics
    useEffect(() => {
        if (!currentUser || !platformAdminAccess || activeView !== 'platform-dashboard') return

        let mounted = true

        async function loadPlatformStatistics() {
            setDashboardLoading(true)
            setPlatformStatistics(null)
            setPlatformOrganizationBreakdown([])
            setError(null)

            try {
                const [statistics, overview, organizationBreakdown] = await Promise.all([
                    requestJson<CaseStatistics>('/api/dashboard/platform'),
                    requestJson<PlatformOverview>('/api/platform/overview'),
                    requestJson<PlatformOrganizationCaseBreakdown[]>('/api/dashboard/platform/organizations'),
                ])

                if (!mounted) return

                setPlatformStatistics(statistics)
                setPlatformOverview(overview)
                setPlatformOrganizationBreakdown(organizationBreakdown)
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
        if (!currentUser || !organizationAccess || !organizationId) return
        if (activeView !== 'organization-dashboard' && activeView !== 'cases') return

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
                const query = new URLSearchParams({ page: String(errorPage), pageSize: '25' })
                if (errorFilter !== 'all') query.set('isHandled', String(errorFilter === 'handled'))
                const errors = await requestJson<PagedResponse<ApplicationError>>(`/api/errors?${query}`)

                if (!mounted) return

                setApplicationErrors(errors.items)
                setErrorTotalCount(errors.totalCount)
                setErrorTotalPages(Math.max(1, errors.totalPages))
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
    }, [currentUser, platformAdminAccess, activeView, retryVersion, errorPage, errorFilter])

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
        activeView === 'organizations' ? selectedOrganizationId : ''

    useEffect(() => {
        if (!currentUser || !invitationsOrganizationId) return

        let mounted = true
        const targetOrganizationId = invitationsOrganizationId

        const query = new URLSearchParams({ page: String(invitationsQuery.page), pageSize: String(invitationsQuery.pageSize) })
        if (invitationsQuery.search.trim()) query.set('search', invitationsQuery.search.trim())
        void requestJson<PagedResponse<AccountInvitation>>(
            `/api/organizations/${targetOrganizationId}/invitations?${query}`,
            undefined,
            targetOrganizationId,
        )
            .then((loaded) => {
                if (mounted) {
                    setInvitationsData({
                        organizationId: targetOrganizationId,
                        items: loaded.items,
                    })
                    setInvitationsPageInfo({ page: loaded.page, pageSize: loaded.pageSize, totalCount: loaded.totalCount, totalPages: loaded.totalPages })
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
        invitationsOrganizationId,
        invitationsQuery,
        retryVersion,
    ])

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

    const openCitizenCase = (caseId: string | null = null) => {
        setCitizenInitialCaseId(caseId)
        setCitizenCasesVersion((version) => version + 1)
        setViewSelection('citizen-cases')
    }

    const completeSignIn = (user: AuthUser, portal: SignInPortal) => {
        setCurrentUser(user)
        setActivePortal(portal)
        setViewSelection(
            portal === 'citizen'
                ? 'citizen-dashboard'
                : portal === 'platform-admin'
                    ? 'platform-dashboard'
                    : 'cases',
        )
        setLoginError(null)
        setLoginErrorCode(null)
        setError(null)
        setCitizenInitialCaseId(null)
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
        setCases([])
        setStaff([])
        setOrganizations([])
        setActivePortal(null)
        setCitizenInitialCaseId(null)
        forgetPortal()
        forgetWorkspaceView()
        navigateTo('/')
        setRoute('landing')
        setViewSelection('citizen-dashboard')
        showToast('Signed out.', 'info')
    }

    async function submitCase(event: FormEvent<HTMLFormElement>) {
        event.preventDefault()
        setCaseFormSubmitted(true)

        if (!currentUser || !organizationId) return

        const titleError = validateCaseTitle(newTitle)
        const descriptionError = validateCaseDescription(newDescription)

        if (titleError !== null || descriptionError !== null) {
            setError(titleError ?? descriptionError)
            return
        }

        setActionLoading(true)
        setError(null)

        try {
            const created = await requestJson<CaseRecord>('/api/cases', {
                method: 'POST',
                body: JSON.stringify({
                    title: normalizeWhitespace(newTitle),
                    description: normalizeMultiline(newDescription),
                }),
            })

            setCases((current) => [created, ...current])
            setSelectedCaseId(created.id)
            setNewTitle('')
            setNewDescription('')
            setCaseFormSubmitted(false)
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

    async function changeStatus(
        status: CaseStatus,
        explanation?: string,
        explanationVisibleToCitizen = false,
    ) {
        if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

        const caseId = selectedCase.id

        setActionLoading(true)
        setError(null)

        try {
            const updated = await requestJson<{ id: string; status: CaseStatus; updatedAt: string }>(
                `/api/cases/${caseId}/status`,
                {
                    method: 'PATCH',
                    body: JSON.stringify({
                        newStatus: status,
                        explanation: explanation?.trim() || null,
                        explanationVisibleToCitizen,
                    }),
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

    async function changePriority(priority: CasePriority) {
        if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

        const caseId = selectedCase.id

        setActionLoading(true)
        setError(null)

        try {
            const updated = await requestJson<{ id: string; priority: CasePriority; updatedAt: string }>(
                `/api/cases/${caseId}/priority`,
                {
                    method: 'PATCH',
                    body: JSON.stringify({ newPriority: priority }),
                },
            )

            setCases((current) => current.map((item) => (
                item.id === updated.id ? {...item, priority: updated.priority} : item
            )))

            setRetryVersion((version) => version + 1)
            showToast('Priority updated.', 'success')
        } catch (reason) {
            const message = reason instanceof Error ? reason.message : 'Priority update failed. Your role may not permit this action.'
            setError(message)
            showToast(message, 'error')
        } finally {
            setActionLoading(false)
        }
    }

    async function addCaseUpdate(
        kind: CaseUpdateKind,
        message: string,
    ): Promise<boolean> {
        if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return false

        const caseId = selectedCase.id
        setActionLoading(true)
        setError(null)

        try {
            const created = await requestJson<CaseUpdate>(
                `/api/cases/${caseId}/updates`,
                {
                    method: 'POST',
                    body: JSON.stringify({kind, message}),
                },
            )

            setCaseUpdatesData((current) => ({
                caseId,
                items: current?.caseId === caseId
                    ? [...current.items, created]
                    : [created],
            }))
            setRetryVersion((version) => version + 1)
            showToast(
                kind === 'InternalNote'
                    ? 'Internal note added.'
                    : kind === 'InformationRequest'
                        ? 'Information request sent.'
                        : 'Update published.',
                'success',
            )
            return true
        } catch (reason) {
            const messageText = reason instanceof Error
                ? reason.message
                : 'The case update could not be saved.'
            setError(messageText)
            showToast(messageText, 'error')
            return false
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

    async function uploadDocument(file: File, visibleToCitizen: boolean) {
        if (!currentUser || !organizationId || !selectedCase || selectedCase.id.startsWith('demo-')) return

        const caseId = selectedCase.id

        setActionLoading(true)
        setError(null)

        const formData = new FormData()
        formData.append('file', file)
        formData.append('visibleToCitizen', String(visibleToCitizen))

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

    async function loadCaseDocument(document: CaseDocument): Promise<Blob> {
        if (!currentUser || !organizationId) {
            throw new Error('Your session is missing or has expired. Sign in again.')
        }

        const response = await apiRequest(
            `/api/cases/${document.caseId}/documents/${document.id}`,
            organizationId,
        )

        if (response.status === 401) throw new Error('Your session is missing or has expired. Sign in again.')
        if (response.status === 403 || response.status === 404) {
            throw new Error('You are not authorized to preview this document.')
        }
        if (!response.ok) throw new Error(`Document preview failed with HTTP ${response.status}.`)

        return response.blob()
    }

    async function setStaffActive(member: StaffMember) {
        if (!currentUser || !organizationId) return

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
            }, currentOrganizationId)

            setMembershipsData((current) => ({
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
                onSignIn={handleOrganizationSignIn}
            />
        )
    }

    if (!currentUser && route === 'platform-admin-login') {
        return (
            <PlatformAdminLoginPage
                onSubmit={handlePlatformAdminLoginSubmit}
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
                onSignIn={handleCitizenSignIn}
                onVerifiedPending={() => handleVerifyEmail()}
            />
        )
    }

    if (!currentUser && route === 'verify-email') {
        return (
            <VerifyEmailPage
                onSignIn={handleCitizenSignIn}
            />
        )
    }

    if (!currentUser && route === 'forgot-password') {
        return (
            <ForgotPasswordPage />
        )
    }

    if (!currentUser && route === 'reset-password') {
        return (
            <ResetPasswordPage
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
            <AuthPage
                description="This account is signed in as a citizen account. Citizen accounts only have access to the citizen portal, so the organization workspace, case management, and staff administration stay closed."
                title="Organization workspace unavailable"
                width="wide"
            >
                <div className="auth-actions">
                    <button
                        className="primary-button"
                        onClick={() => {
                            setViewSelection('citizen-dashboard')
                            navigateTo(WORKSPACE_PATH)
                            setRoute('workspace')
                        }}
                        type="button"
                    >
                        Go to my dashboard
                    </button>
                    <button className="secondary-button" onClick={handleSignOut} type="button">
                        Sign out
                    </button>
                </div>
            </AuthPage>
        )
    }

    // The platform administration portal is only reachable for super
    // administrator accounts. Staff and citizen accounts that navigate to it
    // are told why and are returned to their own portal.
    if (route === 'platform-admin-login' && !platformAdminAccess) {
        return (
            <AuthPage
                description="This account is not a platform administrator. Platform administration is limited to super administrator accounts; staff use the organization portal and citizens use the citizen portal."
                title="Platform administration unavailable"
                width="wide"
            >
                <div className="auth-actions">
                    <button
                        className="primary-button"
                        onClick={() => {
                            navigateTo(WORKSPACE_PATH)
                            setRoute('workspace')
                        }}
                        type="button"
                    >
                        Go to my workspace
                    </button>
                    <button className="secondary-button" onClick={handleSignOut} type="button">
                        Sign out
                    </button>
                </div>
            </AuthPage>
        )
    }

    const casesFiltered = selectedStatus !== 'All' || selectedPriority !== 'All' || search.length > 0
    const caseEmptyMessage = !organizationId
        ? 'Loading your organization workspace.'
        : casesFiltered
            ? 'No cases match the current filter.'
            : 'No cases are available in this organization.'

    const summaryTotalCases = organizationStatistics?.totalCases ?? caseTotalCount
    const summaryNeedsReview = organizationStatistics
        ? organizationStatistics.submittedCases + organizationStatistics.underReviewCases
        : cases.filter((item) => item.status === 'Submitted' || item.status === 'UnderReview').length
    const summaryInProgress = organizationStatistics
        ? organizationStatistics.inProgressCases
        : cases.filter((item) => item.status === 'InProgress').length
    const summaryResolved = organizationStatistics
        ? organizationStatistics.resolvedCases + organizationStatistics.closedCases
        : cases.filter((item) => item.status === 'Resolved' || item.status === 'Closed').length

    const workspaceLabel = activePortal === 'citizen'
        ? 'Citizen portal'
        : activePortal === 'platform-admin'
            ? 'Platform administration'
            : 'Organization workspace'

    const workspaceCaption = activePortal === 'citizen'
        ? currentUser.email
        : activePortal === 'platform-admin'
            ? 'Platform administrator'
            : activeOrganization?.name ?? 'Organization membership required'

    const viewLabels: Record<WorkspaceView, string> = {
        'citizen-dashboard': 'Dashboard',
        'citizen-cases': 'My cases',
        citizen: 'Public intake',
        profile: 'My profile',
        cases: 'Cases',
        staff: 'Staff & team',
        activity: 'Activity log',
        'organization-dashboard': 'Dashboard',
        organizations: platformAdminAccess ? 'Organizations' : 'My Organization',
        'platform-dashboard': 'Dashboard',
        'platform-cases': 'All cases',
        'platform-directory': 'Directory',
        'platform-audit': 'Audit log',
        'platform-errors': 'Errors',
        'platform-jobs': 'Background jobs',
    }

    return (
        <div className="app-shell">
            <a className="skip-link" href="#main-content">Skip to main content</a>
            <aside className="sidebar">
                <button
                    className="brand brand-button"
                    onClick={() => navigateToWorkspaceView(
                        availableViews.includes('organization-dashboard')
                            ? 'organization-dashboard'
                            : availableViews.includes('platform-dashboard')
                                ? 'platform-dashboard'
                                : availableViews[0] ?? 'citizen-dashboard',
                    )}
                    type="button"
                >
                    <span className="brand-mark">C</span>
                    <span>{applicationName}</span>
                </button>

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
                            onClick={() => navigateToWorkspaceView(item.view)}
                            type="button"
                        >
                            <Icon aria-hidden="true" className="nav-icon" name={item.icon} />
                            <span>{item.label}</span>
                            {item.view === 'cases' && caseTotalCount > 0 && <b>{caseTotalCount}</b>}
                        </button>
                    ))}
                </nav>
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
                                    navigateToCase(caseId)
                                }
                                : citizenAccess
                                    ? openCitizenCase
                                    : undefined}
                        />
                        <ProfileMenu
                            email={currentUser.email}
                            fullName={currentUser.fullName}
                            onOpenProfile={() => navigateToWorkspaceView('profile')}
                            onSignOut={navigateToSignOut}
                        />
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

                            {activeView === 'citizen-dashboard' ||
                            activeView === 'citizen-cases' ? (
                                <CitizenDashboard
                                    key={`${activeView}-${citizenCasesVersion}`}
                                    mode={activeView === 'citizen-cases' ? 'cases' : 'dashboard'}
                                    fullName={currentUser.fullName}
                                    email={currentUser.email}
                                    emailConfirmed={currentUser.emailConfirmed}
                                    onOpenIntake={() => setViewSelection('citizen')}
                                    onOpenProfile={() => setViewSelection('profile')}
                                    onOpenCases={openCitizenCase}
                                    initialCaseId={citizenInitialCaseId}
                                    onInitialCaseOpened={() => setCitizenInitialCaseId(null)}
                                />
                            ) : activeView === 'citizen' ? (
                                <CitizenIntake
                                    onSubmitted={() => setCitizenCasesVersion((version) => version + 1)}
                                    onTrackCase={openCitizenCase}
                                />
                            ) : activeView === 'profile' ? (
                                platformAdminAccess
                                    ? <PlatformAdminProfile user={currentUser}/>
                                    : <ProfilePage/>
                            ) : activeView === 'staff' && organizationAccess ? (
                                <StaffDirectory
                                    staff={staffDirectoryPage.items}
                                    result={staffDirectoryPage}
                                    query={staffDirectoryQuery}
                                    onQueryChange={setStaffDirectoryQuery}
                                    canManage={activeOrganization?.role === 'OrganizationAdmin'}
                                    authenticated={Boolean(currentUser)}
                                    loading={adminLoading}
                                    onToggle={setStaffActive}
                                    onUpdatePhone={updateStaffPhone}
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
                                    organizationBreakdown={platformOrganizationBreakdown}
                                    loading={dashboardLoading}
                                />
                            ) : activeView === 'platform-cases' && platformAdminAccess ? (
                                <PlatformCases/>
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
                                    users={platformUsers}
                                    result={directoryResult}
                                    onQueryChange={setDirectoryQuery}
                                    refreshData={() => setRetryVersion((version) => version + 1)}
                                    onOpenOrganization={(organizationId) => {
                                        setSelectedOrganizationId(organizationId)
                                        setViewSelection('organizations')
                                        setError(null)
                                    }}
                                />
                            ) : activeView === 'platform-errors' && platformAdminAccess ? (
                                <PlatformErrors
                                    errors={applicationErrors}
                                    loading={errorsLoading}
                                    page={errorPage}
                                    pageCount={errorTotalPages}
                                    totalCount={errorTotalCount}
                                    filter={errorFilter}
                                    onPageChange={setErrorPage}
                                    onFilterChange={(filter) => {
                                        setErrorFilter(filter)
                                        setErrorPage(1)
                                    }}
                                    onMarkAsHandled={async (errorId) => {
                                        try {
                                            await requestJson(`/api/errors/${errorId}/mark-handled`, {method: 'PATCH'})
                                            setRetryVersion((version) => version + 1)
                                        } catch (reason) {
                                            setError(reason instanceof Error ? reason.message : 'Failed to mark error as handled')
                                        }
                                    }}
                                />
                            ) : activeView === 'platform-audit' && platformAdminAccess ? (
                                <AuditLog
                                    title="Platform audit trail"
                                    description="Every audited change recorded across the platform, including organization, staff, invitation, access, and authentication events."
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
                            ) : activeView === 'organizations' &&
                                (platformAdminAccess || organizationAdminAccess) ? (
                                <OrganizationAdministration
                                    authenticated={Boolean(currentUser)}
                                    loading={adminLoading}
                                    organizations={organizations}
                                    organizationsPageInfo={organizationsPageInfo}
                                    organizationsQuery={organizationsQuery}
                                    onOrganizationsQueryChange={setOrganizationsQuery}
                                    memberships={memberships}
                                    membershipsPageInfo={membershipsPageInfo}
                                    membershipsQuery={membershipsQuery}
                                    onMembershipsQueryChange={setMembershipsQuery}
                                    invitations={organizationInvitations}
                                    invitationsPageInfo={invitationsPageInfo}
                                    invitationsQuery={invitationsQuery}
                                    onInvitationsQueryChange={setInvitationsQuery}
                                    selectedOrganizationId={selectedOrganizationId}
                                    canManageOrganizations={platformAdminAccess}
                                    onSelectOrganization={(id) => {
                                        setSelectedOrganizationId(id)
                                        setMembershipsQuery((query) => ({ ...query, page: 1 }))
                                        setInvitationsQuery((query) => ({ ...query, page: 1 }))
                                        setMembershipsPageInfo({ page: 1, pageSize: membershipsQuery.pageSize, totalCount: 0, totalPages: 0 })
                                        setInvitationsPageInfo({ page: 1, pageSize: invitationsQuery.pageSize, totalCount: 0, totalPages: 0 })
                                    }}
                                    refreshData={() => setRetryVersion((version) => version + 1)}
                                    onUpdateMembership={updateMembership}
                                    onInvite={(fullName, email, phoneNumber, role) =>
                                        inviteStaff(selectedOrganizationId, fullName, email, phoneNumber, role)}
                                    onResendInvitation={(invitationId) =>
                                        resendInvitation(selectedOrganizationId, invitationId)}
                                    onRevokeInvitation={(invitationId) =>
                                        revokeInvitation(selectedOrganizationId, invitationId)}
                                />
                            ) : activeView === 'organizations' && organizationAccess ? (
                                <MyOrganization
                                    organization={activeOrganization}
                                    staffPage={staffDirectoryPage}
                                    staffQuery={staffDirectoryQuery}
                                    onStaffQueryChange={setStaffDirectoryQuery}
                                    loading={adminLoading}
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
                                            <Icon className="button-icon" name="plus" />
                                            <span>New case</span>
                                        </button>
                                    </div>

                                    {showCreateForm && (
                                        <form className="case-card create-form" noValidate onSubmit={submitCase}>
                                            <h2>Create case</h2>
                                            <label htmlFor="staff-case-title">
                                                Title
                                                <input
                                                    aria-describedby={
                                                        caseFormSubmitted && validateCaseTitle(newTitle) !== null
                                                            ? 'staff-case-title-error'
                                                            : undefined
                                                    }
                                                    aria-invalid={caseFormSubmitted && validateCaseTitle(newTitle) !== null}
                                                    id="staff-case-title"
                                                    required
                                                    minLength={MIN_CASE_TITLE_LENGTH}
                                                    maxLength={MAX_CASE_TITLE_LENGTH}
                                                    value={newTitle}
                                                    onChange={(event) => setNewTitle(event.target.value)}
                                                />
                                            </label>
                                            {caseFormSubmitted && validateCaseTitle(newTitle) !== null && (
                                                <span className="field-error" id="staff-case-title-error" role="alert">
                                                    {validateCaseTitle(newTitle)}
                                                </span>
                                            )}
                                            <label htmlFor="staff-case-description">
                                                Description
                                                <textarea
                                                    aria-describedby={
                                                        caseFormSubmitted && validateCaseDescription(newDescription) !== null
                                                            ? 'staff-case-description-error'
                                                            : undefined
                                                    }
                                                    aria-invalid={caseFormSubmitted && validateCaseDescription(newDescription) !== null}
                                                    id="staff-case-description"
                                                    required
                                                    minLength={MIN_CASE_DESCRIPTION_LENGTH}
                                                    maxLength={MAX_CASE_DESCRIPTION_LENGTH}
                                                    value={newDescription}
                                                    onChange={(event) => setNewDescription(event.target.value)}
                                                />
                                            </label>
                                            {caseFormSubmitted && validateCaseDescription(newDescription) !== null && (
                                                <span className="field-error" id="staff-case-description-error" role="alert">
                                                    {validateCaseDescription(newDescription)}
                                                </span>
                                            )}
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
                                            icon="cases"
                                            label="Total cases"
                                            value={formatNumber(summaryTotalCases)}
                                            detail="Across this organization"
                                            tone="blue"
                                        />
                                        <StatCard
                                            icon="eye"
                                            label="Needs review"
                                            value={formatNumber(summaryNeedsReview)}
                                            detail="Submitted or under review"
                                            tone="amber"
                                        />
                                        <StatCard
                                            icon="activity"
                                            label="In progress"
                                            value={formatNumber(summaryInProgress)}
                                            detail="Active workflow"
                                            tone="purple"
                                        />
                                        <StatCard
                                            icon="check"
                                            label="Resolved"
                                            value={formatNumber(summaryResolved)}
                                            detail="Resolved or closed"
                                            tone="green"
                                        />
                                    </div>

                                    <div className="section-heading">
                                        <div>
                                            <h2>Cases</h2>
                                            <span
                                                className="muted">Showing {formatNumber(cases.length)} of {formatNumber(caseTotalCount)}</span>
                                        </div>
                                    </div>

                                    <div aria-label="Case workspace sections" className="case-workspace-tabs" role="tablist">
                                        <button
                                            aria-selected={caseWorkspacePanel === 'list'}
                                            aria-controls="organization-cases-panel"
                                            className={caseWorkspacePanel === 'list' ? 'active' : ''}
                                            onClick={() => navigateToPanel('list')}
                                            role="tab"
                                            type="button"
                                        >Cases</button>
                                        <button
                                            aria-selected={caseWorkspacePanel === 'details'}
                                            aria-controls="organization-case-details-panel"
                                            className={caseWorkspacePanel === 'details' ? 'active' : ''}
                                            disabled={!selectedCase}
                                            onClick={() => navigateToPanel('details')}
                                            role="tab"
                                            type="button"
                                        >Case details</button>
                                    </div>
                                    <div className="case-layout case-workspace-layout">
                                        <section
                                            aria-label="Cases"
                                            className="case-card"
                                            hidden={caseWorkspacePanel !== 'list'}
                                            id="organization-cases-panel"
                                            role="tabpanel"
                                        >
                                            <div className="case-toolbar data-grid-toolbar">
                                                <div className="search data-grid-search">
                                                    <Icon className="search-icon" name="search" />
                                                    <input
                                                        aria-label="Search cases"
                                                        maxLength={200}
                                                        placeholder="Search cases…"
                                                        value={caseSearchInput}
                                                        onChange={(event) => setCaseSearchInput(event.target.value)}
                                                    />
                                                </div>

                                                <label className="data-grid-filter">
                                                    <span>Status</span>
                                                    <select
                                                        aria-label="Filter by status"
                                                        value={selectedStatus}
                                                        onChange={(event) => {
                                                            setSelectedStatus(event.target.value as 'All' | CaseStatus)
                                                            setCasePage(1)
                                                        }}
                                                    >
                                                        <option value="All">All statuses</option>
                                                        {statusOptions.map((status) => (
                                                            <option key={status}
                                                                    value={status}>{statusLabels[status]}</option>
                                                        ))}
                                                    </select>
                                                </label>

                                                <label className="data-grid-filter">
                                                    <span>Priority</span>
                                                    <select
                                                        aria-label="Filter by priority"
                                                        value={selectedPriority}
                                                        onChange={(event) => {
                                                            setSelectedPriority(event.target.value as 'All' | CasePriority)
                                                            setCasePage(1)
                                                        }}
                                                    >
                                                        <option value="All">All priorities</option>
                                                        <option value="Critical">Critical</option>
                                                        <option value="High">High</option>
                                                        <option value="Normal">Normal</option>
                                                        <option value="Low">Low</option>
                                                    </select>
                                                </label>
                                            </div>

                                            {loading ? (
                                                <p className="muted empty-state">Loading cases…</p>
                                            ) : cases.length === 0 ? (
                                                <p className="muted empty-state">{caseEmptyMessage}</p>
                                            ) : (
                                                <div aria-label="Cases list" className="table-wrap" role="region"
                                                     tabIndex={0}>
                                                    <table className="data-grid-table">
                                                        <thead>
                                                        <tr>
                                                            <th scope="col">CASE</th>
                                                            <th scope="col">STATUS</th>
                                                            <th scope="col">PRIORITY</th>
                                                            <th scope="col">UPDATED</th>
                                                        </tr>
                                                        </thead>
                                                        <tbody>
                                                        {cases.map((item) => (
                                                            <tr
                                                                className={selectedCase?.id === item.id ? 'selected-row' : ''}
                                                                key={item.id}
                                                            >
                                                                <td>
                                                                    <button
                                                                        aria-pressed={selectedCase?.id === item.id}
                                                                        className="case-row-button"
                                                                        onClick={() => navigateToCase(item.id)}
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
                                                                <td>{formatDateTime(item.createdAt)}</td>
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

                                        <div
                                            aria-label="Case details"
                                            className="case-details-panel"
                                            hidden={caseWorkspacePanel !== 'details'}
                                            id="organization-case-details-panel"
                                            role="tabpanel"
                                        >
                                        <CaseDetails
                                            caseRecord={selectedCase}
                                            history={history}
                                            updates={caseUpdates}
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
                                            onPriorityChange={changePriority}
                                            onAddUpdate={addCaseUpdate}
                                            onUpdate={updateCaseDetails}
                                            onDelete={deleteCase}
                                            onAssignmentChange={assignCase}
                                            onUpload={uploadDocument}
                                            onDownload={downloadDocument}
                                            onLoadDocument={loadCaseDocument}
                                            onUnsavedChangesChange={setCaseHasUnsavedChanges}
                                        />
                                        </div>
                                    </div>
                                </>
                            ) : (
                                <CitizenDashboard
                                    key={`citizen-dashboard-${citizenCasesVersion}`}
                                    mode="dashboard"
                                    fullName={currentUser.fullName}
                                    email={currentUser.email}
                                    emailConfirmed={currentUser.emailConfirmed}
                                    onOpenIntake={() => setViewSelection('citizen')}
                                    onOpenProfile={() => setViewSelection('profile')}
                                    onOpenCases={openCitizenCase}
                                    initialCaseId={citizenInitialCaseId}
                                    onInitialCaseOpened={() => setCitizenInitialCaseId(null)}
                                />
                            )}
                        </>
                    )}

                </section>
                <ConfirmationDialog
                    open={pendingUnsavedNavigation !== null}
                    title="Discard unsaved case changes?"
                    description="Your case edits have not been saved. Continue and discard those changes?"
                    confirmLabel="Discard changes"
                    cancelLabel="Continue editing"
                    destructive
                    onCancel={() => setPendingUnsavedNavigation(null)}
                    onConfirm={() => {
                        const navigation = pendingUnsavedNavigation
                        setCaseHasUnsavedChanges(false)
                        if (navigation?.caseId) setSelectedCaseId(navigation.caseId)
                        if (navigation?.panel) setCaseWorkspacePanel(navigation.panel)
                        if (navigation?.view) setViewSelection(navigation.view)
                        if (navigation?.signOut) void handleSignOut()
                        setPendingUnsavedNavigation(null)
                    }}
                />
                <AppFooter />
            </main>
        </div>
    )
}

export default App
