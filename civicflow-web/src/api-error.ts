export type ApiErrorDetails = {
  message: string | null
  code: string | null
}

function collectMessages(value: unknown): string[] {
  if (typeof value === 'string') {
    const trimmed = value.trim()
    return trimmed ? [trimmed] : []
  }

  if (Array.isArray(value)) {
    return value.flatMap(collectMessages)
  }

  if (value && typeof value === 'object') {
    return Object.values(value as Record<string, unknown>).flatMap(collectMessages)
  }

  return []
}

function firstMessage(...values: unknown[]): string | null {
  for (const value of values) {
    const messages = collectMessages(value)
    if (messages.length > 0) return messages.join(' ')
  }

  return null
}

export async function readApiErrorDetails(
  response: Response,
): Promise<ApiErrorDetails> {
  try {
    const body = await response.clone().json()

    if (!body || typeof body !== 'object') {
      return { message: null, code: null }
    }

    const record = body as Record<string, unknown>
    const validationMessages = collectMessages(record.errors)

    return {
      message: validationMessages.length > 0
        ? [...new Set(validationMessages)].join(' ')
        : firstMessage(
            record.error,
            record.detail,
            record.message,
            record.title,
          ),
      code: typeof record.code === 'string' ? record.code : null,
    }
  } catch {
    return { message: null, code: null }
  }
}

export function fallbackApiErrorMessage(status: number): string {
  if (status === 400) {
    return 'The request could not be processed. Check the entered details and try again.'
  }

  if (status === 401) {
    return 'Your session is missing or has expired. Sign in again.'
  }

  if (status === 403) {
    return 'You are not authorized for this operation.'
  }

  if (status === 404) {
    return 'The requested item could not be found. It may have been removed or you may not have access.'
  }

  if (status === 409) {
    return 'The request conflicts with the current state. Refresh the page and try again.'
  }

  if (status === 422) {
    return 'The submitted information is incomplete or invalid. Review the form and try again.'
  }

  if (status === 429) {
    return 'Too many requests were sent. Wait a moment and try again.'
  }

  if (status >= 500) {
    return 'The server could not complete the request. Try again shortly.'
  }

  return `The request failed (HTTP ${status}).`
}
