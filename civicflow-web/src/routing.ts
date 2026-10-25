import { allowedPortals, type AuthUser, type SignInPortal } from './auth'
import type { WorkspaceView } from './domain'

export const STAFF_LOGIN_PATH = '/admin'
export const PLATFORM_ADMIN_LOGIN_PATH = '/super-platform-admin'
export const SETUP_PASSWORD_PATH = '/setup-password'
export const CITIZEN_LOGIN_PATH = '/citizen/login'
export const CITIZEN_REGISTER_PATH = '/citizen/register'
export const VERIFY_EMAIL_PATH = '/citizen/verify-email'
export const FORGOT_PASSWORD_PATH = '/forgot-password'
export const RESET_PASSWORD_PATH = '/reset-password'
export const WORKSPACE_PATH = '/workspace'

export type AppRoute =
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

export function resolveRoute(pathname: string): AppRoute {
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

export function navigateTo(path: string) {
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

export function rememberPortal(portal: SignInPortal): void {
    try {
        window.sessionStorage.setItem(PORTAL_STORAGE_KEY, portal)
    } catch {
        // Storage can be unavailable in private browsing modes. The portal is
        // then derived from the account roles on the next load.
    }
}

export function forgetPortal(): void {
    try {
        window.sessionStorage.removeItem(PORTAL_STORAGE_KEY)
    } catch {
        // Ignore storage failures; the portal is derived from roles anyway.
    }
}

const WORKSPACE_VIEW_STORAGE_KEY = 'civicflow.workspaceView'

const workspaceViews: WorkspaceView[] = [
    'citizen-dashboard',
    'citizen',
    'profile',
    'cases',
    'staff',
    'activity',
    'organization-dashboard',
    'organizations',
    'platform-dashboard',
    'platform-cases',
    'platform-directory',
    'platform-audit',
    'platform-errors',
    'platform-jobs',
]

export function readRememberedWorkspaceView(): WorkspaceView | null {
    try {
        const stored = window.localStorage.getItem(WORKSPACE_VIEW_STORAGE_KEY)
        return stored && workspaceViews.includes(stored as WorkspaceView)
            ? stored as WorkspaceView
            : null
    } catch {
        return null
    }
}

export function rememberWorkspaceView(view: WorkspaceView): void {
    try {
        window.localStorage.setItem(WORKSPACE_VIEW_STORAGE_KEY, view)
    } catch {
        // Storage can be unavailable; the in-memory tab still works.
    }
}

export function forgetWorkspaceView(): void {
    try {
        window.localStorage.removeItem(WORKSPACE_VIEW_STORAGE_KEY)
    } catch {
        // Ignore storage failures; the default tab is still available.
    }
}

/**
 * Picks the portal a restored session belongs to. The remembered choice only
 * wins when the account's roles actually allow that portal.
 */
export function resolveSessionPortal(user: AuthUser): SignInPortal {
    const portals = allowedPortals(user)
    const remembered = readRememberedPortal()

    if (remembered && portals.includes(remembered)) return remembered

    if (portals.includes('platform-admin')) return 'platform-admin'
    if (portals.includes('citizen')) return 'citizen'
    return 'organization'
}
