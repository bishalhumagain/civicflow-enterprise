import type { AuthSession } from './auth'

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') ?? ''

export async function apiRequest(path: string, session: AuthSession, organizationId: string, init: RequestInit = {}): Promise<Response> {
  const headers = new Headers(init.headers)
  headers.set('Authorization', `Bearer ${session.accessToken}`)
  headers.set('X-Organization-Id', organizationId)
  if (init.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')
  return fetch(`${apiBaseUrl}${path}`, { ...init, headers })
}
