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
 * The two separate sign-in surfaces of the product. The API binds every
 * sign-in attempt to one portal, and each portal only accepts the roles that
 * belong to it, so the same credentials can never open both dashboards.
 */
export type SignInPortal = 'citizen' | 'organization'

export const CITIZEN_ROLE = 'citizen'
export const STAFF_ROLE = 'staff'
export const SUPER_ADMIN_ROLE = 'superadmin'

export function loginEndpoint(portal: SignInPortal): string {
  return portal === 'organization'
    ? '/api/auth/login/organization'
    : '/api/auth/login'
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
  if (roles.includes(STAFF_ROLE) || roles.includes(SUPER_ADMIN_ROLE)) {
    portals.push('organization')
  }

  return portals
}

export function canUsePortal(
  user: AuthUser | null,
  portal: SignInPortal,
): boolean {
  return allowedPortals(user).includes(portal)
}

export function isSuperAdmin(user: AuthUser | null): boolean {
  return Boolean(user?.roles?.includes(SUPER_ADMIN_ROLE))
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

async function readErrorCode(response: Response): Promise<string | null> {
  try {
    const body = await response.clone().json()
    return body && typeof body.code === 'string' ? body.code : null
  } catch {
    return null
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
        `Unable to obtain a security token (HTTP ${response.status}).`,
        response.status,
      )
    }

    const payload = (await response.json()) as { token?: string }

    if (!payload.token) {
      throw new AuthApiError('The security token response was malformed.', 500)
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
    const text = JSON.stringify(body).toLowerCase()
    return text.includes('antiforgery') || text.includes('csrf')
  } catch {
    return false
  }
}

async function readErrorMessage(
  response: Response,
  fallback: string,
): Promise<string> {
  try {
    const body = await response.json()

    if (body && typeof body.error === 'string' && body.error.length > 0) {
      return body.error
    }

    if (body && typeof body.message === 'string' && body.message.length > 0) {
      return body.message
    }

    if (body && Array.isArray(body.errors) && body.errors.length > 0) {
      return body.errors.join(' ')
    }
  } catch {
    // Fall through to the generic message.
  }

  return fallback
}

export async function getCurrentUser(): Promise<AuthUser | null> {
  const response = await authFetch('/api/auth/me')

  if (response.status === 401) return null

  if (!response.ok) {
    throw new AuthApiError(
      await readErrorMessage(
        response,
        `Unable to load the current session (HTTP ${response.status}).`,
      ),
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
  const response = await authFetch(loginEndpoint(portal), {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })

  if (!response.ok) {
    throw new AuthApiError(
      await readErrorMessage(response, 'Sign-in failed.'),
      response.status,
      false,
      await readErrorCode(response),
    )
  }

  const user = await getCurrentUser()

  if (!user) {
    throw new AuthApiError(
      'Sign-in succeeded but the session could not be established.',
      500,
    )
  }

  if (!canUsePortal(user, portal)) {
    await logout()

    throw new AuthApiError(
      portal === 'citizen'
        ? 'This account is not a citizen account. Sign in through the organization portal instead.'
        : 'This account is not an organization account. Sign in through the citizen portal instead.',
      403,
      false,
      portal === 'citizen'
        ? 'not_a_citizen_account'
        : 'not_an_organization_account',
    )
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

export async function changePassword(
  currentPassword: string,
  newPassword: string,
): Promise<AuthOperationResult> {
  const response = await authFetch('/api/auth/change-password', {
    method: 'POST',
    body: JSON.stringify({ currentPassword, newPassword }),
  })

  const payload = await safeJson<AuthOperationResult>(response)

  if (!response.ok) {
    throw new AuthApiError(
      payload?.message ?? 'Password change failed.',
      response.status,
    )
  }

  return payload ?? { succeeded: true, message: 'Password changed.' }
}

export type PersonalAccessToken = {
  id: string
  name: string
  publicId: string
  createdAt: string
  expiresAt: string | null
  lastUsedAt: string | null
  revokedAt: string | null
  isActive: boolean
}

export type CreatedPersonalAccessToken = {
  id: string
  name: string
  token: string
  createdAt: string
  expiresAt: string | null
}

export async function listPersonalAccessTokens(): Promise<PersonalAccessToken[]> {
  const response = await authFetch('/api/auth/tokens')

  if (!response.ok) {
    throw new AuthApiError(
      await readErrorMessage(
        response,
        `Unable to load access tokens (HTTP ${response.status}).`,
      ),
      response.status,
    )
  }

  return (await response.json()) as PersonalAccessToken[]
}

export async function createPersonalAccessToken(
  name: string,
  expiresInDays: number | null,
): Promise<CreatedPersonalAccessToken> {
  const response = await authFetch('/api/auth/tokens', {
    method: 'POST',
    body: JSON.stringify({ name, expiresInDays }),
  })

  if (!response.ok) {
    throw new AuthApiError(
      await readErrorMessage(response, 'Unable to create the access token.'),
      response.status,
    )
  }

  return (await response.json()) as CreatedPersonalAccessToken
}

export async function revokePersonalAccessToken(id: string): Promise<void> {
  const response = await authFetch(`/api/auth/tokens/${id}`, {
    method: 'DELETE',
  })

  if (!response.ok && response.status !== 404) {
    throw new AuthApiError(
      await readErrorMessage(response, 'Unable to revoke the access token.'),
      response.status,
    )
  }
}

async function safeJson<T>(response: Response): Promise<T | null> {
  try {
    return (await response.json()) as T
  } catch {
    return null
  }
}
