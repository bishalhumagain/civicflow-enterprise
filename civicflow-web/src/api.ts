import { authFetch } from './auth'

/**
 * Low-level authenticated request helper. The session is carried by the API
 * cookie, so no bearer token is attached here. Organization-scoped endpoints
 * must send the `X-Organization-Id` header; the API remains authoritative and
 * rejects requests outside the caller's organization membership.
 */
export async function apiRequest(
  path: string,
  organizationId: string,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers)

  if (organizationId) {
    headers.set('X-Organization-Id', organizationId)
  }

  return authFetch(path, { ...init, headers })
}
