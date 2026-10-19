import type { AuthSession } from './auth'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? ''

export async function apiRequest(path: string, session: AuthSession, organizationId: string, init: RequestInit = {}): Promise<Response> {
  if (session.expiresAt > 0 && session.expiresAt <= Date.now()) {
    throw new Error('Your session has expired. Sign in again to continue.')
  }
  const headers = new Headers(init.headers)
  headers.set('Authorization', `Bearer ${session.accessToken}`)
  headers.set('X-Organization-Id', organizationId)
  if (init.body && !(init.body instanceof FormData) && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  return fetch(`${apiBaseUrl}${path}`, { ...init, headers })
}
