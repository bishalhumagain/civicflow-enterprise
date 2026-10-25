import { formatFileSize } from "./format"

/**
 * Client-side validation rules. These mirror the API rules to give immediate
 * feedback; the server remains the source of truth and revalidates everything.
 */

export const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/
export const PHONE_ALLOWED_PATTERN = /^[0-9+()\-./\s]*$/


export const MAX_NAME_LENGTH = 150
export const MAX_EMAIL_LENGTH = 254
export const MIN_PHONE_LENGTH = 6
export const MAX_PHONE_LENGTH = 30
export const MAX_IDENTIFIER_LENGTH = 50
export const MAX_ADDRESS_LENGTH = 500
export const MAX_GENDER_LENGTH = 20
export const MAX_CASE_TITLE_LENGTH = 200
export const MIN_CASE_TITLE_LENGTH = 3
export const MAX_CASE_DESCRIPTION_LENGTH = 5000
export const MIN_CASE_DESCRIPTION_LENGTH = 10
export const MAX_CASE_UPDATE_LENGTH = 4000
export const MAX_STATUS_EXPLANATION_LENGTH = 2000
export const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024

export const MIN_PASSWORD_LENGTH = 12
export const MAX_PASSWORD_LENGTH = 128
export const MIN_PASSWORD_UNIQUE_CHARACTERS = 6
export const PASSWORD_RULES =
  "Use at least 12 characters with upper case, lower case, a number, and a symbol."

/** Collapses runs of whitespace and trims single-line text. */
export function normalizeWhitespace(value: string): string {
  return value.trim().replace(/\s+/g, " ")
}

/** Normalizes line endings and trims multiline text without changing content. */
export function normalizeMultiline(value: string): string {
  return value.replace(/\r\n/g, "\n").replace(/\r/g, "\n").trim()
}

/** Trims an optional value and maps empty input to null. */
export function normalizeOptional(
  value: string | null | undefined,
): string | null {
  const normalized = value?.trim() ?? ""
  return normalized.length > 0 ? normalized : null
}

/** Canonical comparison and storage form for an email address. */
export function normalizeEmail(value: string): string {
  return value.trim().toLowerCase()
}

/** Canonical comparison and storage form for NID or citizenship identifiers. */
export function normalizeIdentifier(value: string): string {
  return value.trim().toUpperCase()
}

/** Upper-cases a trimmed optional identifier and maps empty input to null. */
export function normalizeOptionalIdentifier(
  value: string | null | undefined,
): string | null {
  const normalized = normalizeIdentifier(value ?? "")
  return normalized.length > 0 ? normalized : null
}

/** Normalizes a search term while preserving internal word spacing. */
export function normalizeSearch(value: string): string {
  return value.trim().replace(/\s+/g, " ")
}

/** Keeps only characters accepted by the API phone-number rule. */
export function sanitizePhoneInput(value: string): string {
  return value.replace(/[^0-9+()\-./\s]/g, "")
}

/** Detects embedded control characters that single-line fields must reject. */
export function hasControlCharacters(value: string): boolean {
  for (const character of value) {
    const code = character.charCodeAt(0)
    if (code === 9 || code === 10 || code === 13) continue
    if (code < 32 || code === 127) return true
  }
  return false
}

export function countDigits(value: string): number {
  const matches = value.match(/\d/g)
  return matches === null ? 0 : matches.length
}

export function validateRequired(
  value: string | null | undefined,
  label: string,
): string | null {
  const normalized = value?.trim() ?? ""
  return normalized.length > 0 ? null : label + " is required."
}

export function validateName(value: string): string | null {
  const normalized = normalizeWhitespace(value)
  if (normalized.length === 0) return "Full name is required."
  if (normalized.length > MAX_NAME_LENGTH) {
    return "Full name must be " + MAX_NAME_LENGTH + " characters or fewer."
  }
  if (hasControlCharacters(normalized)) {
    return "Full name contains unsupported characters."
  }
  return null
}

export function validateEmail(value: string): string | null {
  const normalized = value.trim()
  if (normalized.length === 0) return "Email address is required."
  if (normalized.length > MAX_EMAIL_LENGTH) {
    return "Email address must be " + MAX_EMAIL_LENGTH + " characters or fewer."
  }
  if (EMAIL_PATTERN.test(normalized) === false) {
    return "Enter a valid email address, for example name@example.com."
  }
  return null
}

export function validateOptionalEmail(value: string): string | null {
  return value.trim().length === 0 ? null : validateEmail(value)
}

export function validatePhone(value: string, required = false): string | null {
  const normalized = value.trim()
  if (normalized.length === 0) {
    return required ? "Phone number is required." : null
  }
  if (normalized.length < MIN_PHONE_LENGTH) {
    return "Phone number must be at least " + MIN_PHONE_LENGTH + " characters."
  }
  if (normalized.length > MAX_PHONE_LENGTH) {
    return "Phone number must be " + MAX_PHONE_LENGTH + " characters or fewer."
  }
  if (PHONE_ALLOWED_PATTERN.test(normalized) === false) {
    return "Phone number can contain only digits, spaces, and the symbols + ( ) - . /"
  }
  if (countDigits(normalized) < MIN_PHONE_LENGTH) {
    return "Phone number must contain at least " + MIN_PHONE_LENGTH + " digits."
  }
  return null
}

export function validateIdentifier(value: string, label: string): string | null {
  const normalized = normalizeIdentifier(value)
  if (normalized.length > MAX_IDENTIFIER_LENGTH) {
    return label + " must be " + MAX_IDENTIFIER_LENGTH + " characters or fewer."
  }
  if (normalized.length > 0 && hasControlCharacters(normalized)) {
    return label + " contains unsupported characters."
  }
  return null
}

export function validateLength(
  value: string,
  label: string,
  maximum: number,
  minimum = 0,
): string | null {
  const normalized = value.trim()
  if (normalized.length < minimum) {
    return label + " must be at least " + minimum + " characters."
  }
  if (normalized.length > maximum) {
    return label + " must be " + maximum + " characters or fewer."
  }
  return null
}

export function validateAge(value: string): string | null {
  const trimmed = value.trim()
  if (trimmed.length === 0) return null
  if (/^\d+$/.test(trimmed) === false) return "Enter age as a whole number."

  const age = Number(trimmed)
  if (Number.isSafeInteger(age) === false || age < 0 || age > 150) {
    return "Enter an age between 0 and 150."
  }
  return null
}

export function validatePassword(value: string): string | null {
  if (value.length === 0) return "Enter a password."
  if (value.length < MIN_PASSWORD_LENGTH) {
    return "Use at least " + MIN_PASSWORD_LENGTH + " characters."
  }
  if (value.length > MAX_PASSWORD_LENGTH) {
    return "Use " + MAX_PASSWORD_LENGTH + " characters or fewer."
  }
  if (/[a-z]/.test(value) === false) {
    return "Include at least one lower-case letter."
  }
  if (/[A-Z]/.test(value) === false) {
    return "Include at least one upper-case letter."
  }
  if (/[0-9]/.test(value) === false) return "Include at least one number."
  if (/[^a-zA-Z0-9]/.test(value) === false) {
    return "Include at least one symbol."
  }
  if (new Set(value).size < MIN_PASSWORD_UNIQUE_CHARACTERS) {
    return (
      "Use at least " +
      MIN_PASSWORD_UNIQUE_CHARACTERS +
      " different characters."
    )
  }
  return null
}

export function validatePasswordConfirmation(
  password: string,
  confirmation: string,
): string | null {
  if (confirmation.length === 0) return "Confirm the password."
  if (password !== confirmation) return "The two passwords do not match."
  return null
}

export function validateCaseTitle(value: string): string | null {
  const normalized = normalizeWhitespace(value)
  if (normalized.length === 0) return "Issue title is required."
  if (normalized.length < MIN_CASE_TITLE_LENGTH) {
    return "Issue title must be at least " + MIN_CASE_TITLE_LENGTH + " characters."
  }
  return validateLength(normalized, "Issue title", MAX_CASE_TITLE_LENGTH)
}

export function validateCaseDescription(value: string): string | null {
  const normalized = normalizeMultiline(value)
  if (normalized.length === 0) return "Details are required."
  if (normalized.length < MIN_CASE_DESCRIPTION_LENGTH) {
    return (
      "Details must be at least " +
      MIN_CASE_DESCRIPTION_LENGTH +
      " characters."
    )
  }
  return validateLength(normalized, "Details", MAX_CASE_DESCRIPTION_LENGTH)
}

export function validateCaseUpdate(value: string): string | null {
  const normalized = normalizeMultiline(value)
  if (normalized.length === 0) return "Enter a message."
  return validateLength(normalized, "Message", MAX_CASE_UPDATE_LENGTH)
}

export function validateFile(
  file: File | null,
  maximumBytes: number = MAX_FILE_SIZE_BYTES,
): string | null {
  if (file === null) return null
  if (file.size <= 0) return "The selected file is empty."
  if (file.size > maximumBytes) {
    return (
      "The file must be " + formatFileSize(maximumBytes) + " or smaller."
    )
  }
  return null
}
