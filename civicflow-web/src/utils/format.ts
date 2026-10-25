/**
 * Shared display formatting for CivicFlow. Locale and time zone are explicit
 * so every surface renders the same instant consistently, independent of the
 * browser or operating-system settings.
 */

export const DISPLAY_LOCALE = "en-GB"
export const DISPLAY_TIME_ZONE = "Asia/Kathmandu"
export const EMPTY_VALUE = "—"

export type DateInput = string | number | Date | null | undefined

function toValidDate(value: DateInput): Date | null {
  if (value === null || value === undefined) return null

  const parsed = value instanceof Date ? value : new Date(value)
  return Number.isNaN(parsed.getTime()) ? null : parsed
}

/** Formats a calendar date in the shared display time zone. */
export function formatDate(value: DateInput): string {
  const parsed = toValidDate(value)
  if (parsed === null) return EMPTY_VALUE

  return new Intl.DateTimeFormat(DISPLAY_LOCALE, {
    timeZone: DISPLAY_TIME_ZONE,
    year: "numeric",
    month: "short",
    day: "2-digit",
  }).format(parsed)
}

/** Formats a date and time in the shared display time zone. */
export function formatDateTime(value: DateInput): string {
  const parsed = toValidDate(value)
  if (parsed === null) return EMPTY_VALUE

  return new Intl.DateTimeFormat(DISPLAY_LOCALE, {
    timeZone: DISPLAY_TIME_ZONE,
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  }).format(parsed)
}

/** Formats a whole number with locale-aware grouping. */
export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isFinite(value) === false) {
    return EMPTY_VALUE
  }

  return new Intl.NumberFormat(DISPLAY_LOCALE).format(value)
}

/**
 * Formats a byte count using binary units. Values below 10 units keep one
 * decimal place; larger values are rounded to a whole unit.
 */
export function formatFileSize(sizeBytes: number | null | undefined): string {
  if (
    sizeBytes === null ||
    sizeBytes === undefined ||
    Number.isFinite(sizeBytes) === false ||
    sizeBytes < 0
  ) {
    return EMPTY_VALUE
  }

  if (sizeBytes < 1024) {
    return Math.round(sizeBytes) + " B"
  }

  const units = ["KB", "MB", "GB", "TB"]
  let size = sizeBytes / 1024
  let unitIndex = 0

  while (size >= 1024 && unitIndex < units.length - 1) {
    size = size / 1024
    unitIndex += 1
  }

  const precision = size >= 10 ? 0 : 1
  return size.toFixed(precision) + " " + units[unitIndex]
}

/** Formats a case or account reference, tolerating missing values. */
export function formatReference(reference: string | null | undefined): string {
  const trimmed = reference?.trim() ?? ""
  return trimmed.length > 0 ? trimmed : EMPTY_VALUE
}

/** Returns trimmed display text or an accessible fallback. */
export function formatText(
  value: string | null | undefined,
  fallback: string = EMPTY_VALUE,
): string {
  const trimmed = value?.trim() ?? ""
  return trimmed.length > 0 ? trimmed : fallback
}

/** Formats a stored enum value such as AwaitingInformation for display. */
export function formatStatus(value: string | null | undefined): string {
  const normalized = value?.trim() ?? ""
  if (normalized.length === 0) return EMPTY_VALUE

  return normalized
    .replace(/([a-z0-9])([A-Z])/g, "$1 $2")
    .replace(/_/g, " ")
}

/** Truncates long free text at a word boundary where possible. */
export function truncate(
  value: string | null | undefined,
  maximumLength: number,
): string {
  const normalized = value?.trim() ?? ""
  if (normalized.length === 0) return EMPTY_VALUE
  if (Number.isFinite(maximumLength) === false || maximumLength <= 0) {
    return normalized
  }
  if (normalized.length <= maximumLength) return normalized

  const sliced = normalized.slice(0, maximumLength)
  const lastSpace = sliced.lastIndexOf(" ")
  const boundary = lastSpace > maximumLength / 2 ? lastSpace : sliced.length

  return sliced.slice(0, boundary).trimEnd() + "…"
}
