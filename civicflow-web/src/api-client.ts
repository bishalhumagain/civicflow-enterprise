import { apiRequest } from './api'
import { organizationId } from './domain'
import type { AuthSession } from './auth'

export async function requestJson<T>(path: string, session: AuthSession, init?: RequestInit): Promise<T> {
  const response = await apiRequest(path, session, organizationId ?? '', init)
  if (response.status === 401) throw new Error('Your session is missing or has expired. Sign in again.')
  if (response.status === 403) throw new Error('You are not authorized for this organization or operation.')
  if (!response.ok) throw new Error(`API request failed with HTTP ${response.status}.`)
  if (response.status === 204) return undefined as T
  return await response.json() as T
}
