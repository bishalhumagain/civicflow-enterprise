import { readApiErrorDetails } from './api-error'

/**
 * Cookie-based authentication client for the CivicFlow API.
 *
 * Authentication is handled entirely by the API using first-party,
 * HttpOnly session cookies. This module never stores credentials, tokens,
 * or secrets in JavaScript-accessible storage; it only performs fetch calls
 * and reads the current session from `GET /api/auth/me`.
 *
 * State-changing requests must carry the anti-forgery token obtained from
 * `GET /api/auth/csrf`, which the API exposes through the `X-CSRF-TOKEN`
 * header.
 */

const apiBaseUrl =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? ''

export type AuthUser = {
  id: string
  fullName: string
  email: string
  platformRole: string
  emailConfirmed: boolean
  roles: string[]
  permissions: string[]
}

export type AuthOperationResult = {
  succeeded: boolean
  message: string
}

/**
 * The three separate sign-in surfaces of the product. The API binds every
 * sign-in attempt to one portal, and each portal only accepts the roles that
 * belong to it, so the same credentials can never open another dashboard.
 */
export type SignInPortal = 'citizen' | 'organization' | 'platform-admin'

export const CITIZEN_ROLE = 'citizen'
export const STAFF_ROLE = 'staff'
export const SUPER_ADMIN_ROLE = 'superadmin'

export function loginEndpoint(portal: SignInPortal): string {
  switch (portal) {
    case 'organization':
      return '/api/auth/login/organization'
    case 'platform-admin':
      return '/api/auth/login/platform-admin'
    default:
      return '/api/auth/login'
  }
}

/**
 * The portals the account's roles entitle it to. This mirrors the server-side
 * rule exactly so the UI never offers a dashboard the API would refuse.
 */
export function allowedPortals(user: AuthUser | null): SignInPortal[] {
  if (!user) return []

  const roles = user.roles ?? []
  const portals: SignInPortal[] = []

  if (roles.includes(CITIZEN_ROLE)) portals.push('citizen')
  if (roles.includes(STAFF_ROLE)) portals.push('organization')
  if (roles.includes(SUPER_ADMIN_ROLE)) portals.push('platform-admin')

  return portals
}

export function canUsePortal(
  user: AuthUser | null,
  portal: SignInPortal,
): boolean {
  return allowedPortals(user).includes(portal)
}

export class AuthApiError extends Error {
  readonly status: number
  readonly isCsrfFailure: boolean
  readonly code: string | null

  constructor(
    message: string,
    status: number,
    isCsrfFailure = false,
    code: string | null = null,
  ) {
    super(message)
    this.name = 'AuthApiError'
    this.status = status
    this.isCsrfFailure = isCsrfFailure
    this.code = code
  }
}

let csrfToken: string | null = null
let csrfRequest: Promise<string> | null = null

/**
 * Fetches (and caches) the anti-forgery request token. The token is echoed
 * back in the `X-CSRF-TOKEN` header on unsafe requests, paired with the
 * anti-forgery cookie the API sets on the same response.
 */
export async function fetchCsrfToken(force = false): Promise<string> {
  if (!force && csrfToken) return csrfToken
  if (!force && csrfRequest) return csrfRequest

  csrfRequest = (async () => {
    const response = await fetch(`${apiBaseUrl}/api/auth/csrf`, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
    })

    if (!response.ok) {
      throw new AuthApiError(
        'Your request could not be completed. Refresh the page and try again.',
        response.status,
      )
    }

    const payload = (await response.json()) as { token?: string }

    if (!payload.token) {
      throw new AuthApiError(
        'Your request could not be completed. Refresh the page and try again.',
        500,
      )
    }

    csrfToken = payload.token
    return csrfToken
  })()

  try {
    return await csrfRequest
  } finally {
    csrfRequest = null
  }
}

export function clearCsrfToken(): void {
  csrfToken = null
}

const unsafeMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE'])

/**
 * Performs an authenticated fetch against the API using the session cookie.
 * Unsafe methods automatically attach the anti-forgery token. When the API
 * rejects a request with a 400 anti-forgery failure, the token is refreshed
 * once and the request is retried.
 */
export async function authFetch(
  path: string,
  init: RequestInit = {},
  options: { retryOnCsrf?: boolean } = {},
): Promise<Response> {
  const { retryOnCsrf = true } = options
  const method = (init.method ?? 'GET').toUpperCase()

  const headers = new Headers(init.headers)

  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json')
  }

  if (
    init.body &&
    !(init.body instanceof FormData) &&
    !headers.has('Content-Type')
  ) {
    headers.set('Content-Type', 'application/json')
  }

  if (unsafeMethods.has(method)) {
    const token = await fetchCsrfToken()
    headers.set('X-CSRF-TOKEN', token)
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers,
    credentials: 'include',
  })

  if (
    response.status === 400 &&
    unsafeMethods.has(method) &&
    retryOnCsrf &&
    await looksLikeCsrfFailure(response)
  ) {
    clearCsrfToken()
    await fetchCsrfToken(true)

    return authFetch(path, init, { retryOnCsrf: false })
  }

  return response
}

async function looksLikeCsrfFailure(response: Response): Promise<boolean> {
  try {
    const body = await response.clone().json()

    // Preferred: the server emits a stable, machine-readable code so wording
    // changes in the human-readable message can never break recovery.
    if (
      body &&
      typeof body === 'object' &&
      'code' in body &&
      (body as { code?: unknown }).code === 'antiforgery_invalid'
    ) {
      return true
    }

    // Fallback for older responses that only carried the message text. Match
    // both the hyphenated and non-hyphenated spellings so a stale token is
    // always refreshed instead of surfacing as a hard error.
    const text = JSON.stringify(body).toLowerCase()
    return (
      text.includes('antiforgery') ||
      text.includes('anti-forgery') ||
      text.includes('csrf')
    )
  } catch {
    return false
  }
}

export async function getCurrentUser(): Promise<AuthUser | null> {
  const response = await authFetch('/api/auth/me')

  if (response.status === 401) return null

  if (!response.ok) {
    const { message } = await readApiErrorDetails(response)

    throw new AuthApiError(
      message ?? 'Unable to load your account. Refresh the page and try again.',
      response.status,
    )
  }

  return (await response.json()) as AuthUser
}

/**
 * Signs in against the portal the account belongs to. The API rejects a
 * sign-in whose roles do not match the portal, and this client re-checks the
 * session afterwards so a mismatch can never leave an unusable session
 * behind.
 */
export async function login(
  email: string,
  password: string,
  portal: SignInPortal = 'citizen',
): Promise<AuthUser> {
  // Drop any token cached from a previous user's session before signing in, so
  // the new session never builds requests on a stale anti-forgery pair.
  clearCsrfToken()

  const response = await authFetch(loginEndpoint(portal), {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })

  if (!response.ok) {
    const { message, code } = await readApiErrorDetails(response)

    throw new AuthApiError(
      message ?? 'Sign-in failed.',
      response.status,
      false,
      code,
    )
  }

  const user = await getCurrentUser()

  if (!user) {
    throw new AuthApiError(
      'Sign-in succeeded but the session could not be established.',
      500,
    )
  }

  // The pre-sign-in token was issued for the anonymous identity. Anti-forgery
  // tokens are bound to the authenticated principal, so the first mutation
  // after sign-in must request a fresh pair instead of triggering a retry.
  clearCsrfToken()

  if (!canUsePortal(user, portal)) {
    await logout()

    const mismatch =
      portal === 'citizen'
        ? {
            message:
              'This account is not a citizen account. Sign in through the correct portal instead.',
            code: 'not_a_citizen_account',
          }
        : portal === 'platform-admin'
          ? {
              message:
                'This account is not a platform administrator. Sign in through the organization or citizen portal instead.',
              code: 'not_a_platform_admin',
            }
          : {
              message:
                'This account is not an organization account. Sign in through the citizen or platform administration portal instead.',
              code: 'not_an_organization_account',
            }

    throw new AuthApiError(mismatch.message, 403, false, mismatch.code)
  }

  return user
}

export async function logout(): Promise<void> {
  try {
    await authFetch('/api/auth/logout', { method: 'POST' })
  } finally {
    clearCsrfToken()
  }
}

export type RegisterInput = {
  fullName: string
  email: string
  phoneNumber: string
  nidNumber: string
  citizenshipNumber: string
  password: string
}

export async function register(
  input: RegisterInput,
): Promise<AuthOperationResult> {
  const response = await authFetch('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify(input),
  })

  const payload = await safeJson<AuthOperationResult>(response)

  if (!response.ok) {
    throw new AuthApiError(
      payload?.message ?? 'Registration failed.',
      response.status,
    )
  }

  return payload ?? { succeeded: true, message: 'Registration received.' }
}

export async function verifyEmail(
  email: string,
  token: string,
): Promise<AuthOperationResult> {
  const response = await authFetch('/api/auth/verify-email', {
    method: 'POST',
    body: JSON.stringify({ email, token }),
  })

  const payload = await safeJson<AuthOperationResult>(response)

  if (!response.ok) {
    throw new AuthApiError(
      payload?.message ?? 'Email verification failed.',
      response.status,
    )
  }

  return payload ?? { succeeded: true, message: 'Email verified.' }
}

export async function resendVerification(
  email: string,
): Promise<AuthOperationResult> {
  const response = await authFetch('/api/auth/resend-verification', {
    method: 'POST',
    body: JSON.stringify({ email }),
  })

  const payload = await safeJson<AuthOperationResult>(response)

  if (!response.ok) {
    throw new AuthApiError(
      payload?.message ?? 'Unable to resend the verification email.',
      response.status,
    )
  }

  return payload ?? { succeeded: true, message: 'Verification email requested.' }
}

export async function forgotPassword(
  email: string,
): Promise<AuthOperationResult> {
  const response = await authFetch('/api/auth/forgot-password', {
    method: 'POST',
    body: JSON.stringify({ email }),
  })

  const payload = await safeJson<AuthOperationResult>(response)

  if (!response.ok) {
    throw new AuthApiError(
      payload?.message ?? 'Password recovery request failed.',
      response.status,
    )
  }

  return payload ?? {
    succeeded: true,
    message: 'If the address is registered, a recovery message has been sent.',
  }
}

export async function resetPassword(
  email: string,
  token: string,
  newPassword: string,
): Promise<AuthOperationResult> {
  const response = await authFetch('/api/auth/reset-password', {
    method: 'POST',
    body: JSON.stringify({ email, token, newPassword }),
  })

  const payload = await safeJson<AuthOperationResult>(response)

  if (!response.ok) {
    throw new AuthApiError(
      payload?.message ?? 'Password reset failed.',
      response.status,
    )
  }

  return payload ?? { succeeded: true, message: 'Password reset.' }
}

/**
 * Details of a pending invitation, shown before the invitee chooses a
 * password. The lookup never returns the token or a password.
 */
export type InvitationDetails = {
  email: string
  fullName: string
  organizationName: string
  role: string
  expiresAt: string
}

export type AcceptInvitationResult = {
  succeeded: boolean
  email: string
  portal: string
  message: string
}

/**
 * Looks up a password-setup link. A missing, expired, revoked, or used link
 * is reported as the same `invitation_unavailable` error so the response
 * cannot be used to probe for valid tokens.
 */
export async function lookupInvitation(
  token: string,
): Promise<InvitationDetails> {
  const response = await authFetch('/api/auth/invitations/lookup', {
    method: 'POST',
    body: JSON.stringify({ token }),
  })

  if (!response.ok) {
    const { message, code } = await readApiErrorDetails(response)

    throw new AuthApiError(
      message ?? 'This invitation link is invalid, expired, or has already been used.',
      response.status,
      false,
      code,
    )
  }

  return (await response.json()) as InvitationDetails
}

/**
 * Redeems a single-use invitation with a password chosen by the invitee. The
 * API stores only a salted hash and never generates or emails a password.
 */
export async function acceptInvitation(
  token: string,
  password: string,
): Promise<AcceptInvitationResult> {
  const response = await authFetch('/api/auth/invitations/accept', {
    method: 'POST',
    body: JSON.stringify({ token, password }),
  })

  if (!response.ok) {
    const { message, code } = await readApiErrorDetails(response)

    throw new AuthApiError(
      message ?? 'The invitation could not be accepted.',
      response.status,
      false,
      code,
    )
  }

  return (await response.json()) as AcceptInvitationResult
}

async function safeJson<T>(response: Response): Promise<T | null> {
  try {
    return (await response.json()) as T
  } catch {
    return null
  }
}
