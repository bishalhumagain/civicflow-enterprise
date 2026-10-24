import { apiRequest } from './api'
import { fallbackApiErrorMessage, readApiErrorDetails } from './api-error'

let requestOrganizationId: string | null = null

export function setRequestOrganizationId(organizationId: string | null): void {
  requestOrganizationId = organizationId
}

/**
 * Error raised by an organization-scoped API request. It carries the HTTP
 * status and the stable error code the API returned so callers can react to
 * specific outcomes (for example an expired invitation) instead of parsing
 * a human-readable message.
 */
export class ApiError extends Error {
  readonly status: number
  readonly code: string | null

  constructor(message: string, status: number, code: string | null = null) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

/**
 * Authenticated JSON request against an organization-scoped endpoint. The
 * organization is resolved server-side from `X-Organization-Id`, which is
 * passed by `apiRequest`. Platform administrators may override the
 * organization for the organization they are administering; the API still
 * authorizes the request.
 */
export async function requestJson<T>(
  path: string,
  init?: RequestInit,
  organizationIdOverride?: string,
): Promise<T> {
  const response = await apiRequest(
    path,
    organizationIdOverride ?? requestOrganizationId ?? '',
    init,
  )

  if (!response.ok) {
    const { message, code } = await readApiErrorDetails(response)

    if (response.status === 401) {
      throw new ApiError(
        message ?? 'Your session is missing or has expired. Sign in again.',
        401,
        code,
      )
    }

    if (response.status === 403) {
      throw new ApiError(
        message ?? 'You are not authorized for this organization or operation.',
        403,
        code,
      )
    }

    throw new ApiError(
      message ?? fallbackApiErrorMessage(response.status),
      response.status,
      code,
    )
  }

  if (response.status === 204) return undefined as T

  return (await response.json()) as T
}
