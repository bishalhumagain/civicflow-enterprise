import { useEffect, useRef, useState, type FormEvent } from "react"
import { requestJson } from "../api-client"
import { formatFileSize, formatReference } from "../utils/format"
import {
  MAX_CASE_DESCRIPTION_LENGTH,
  MAX_CASE_TITLE_LENGTH,
  MAX_FILE_SIZE_BYTES,
  MIN_CASE_DESCRIPTION_LENGTH,
  MIN_CASE_TITLE_LENGTH,
  normalizeMultiline,
  normalizeWhitespace,
  validateCaseDescription,
  validateCaseTitle,
  validateFile,
} from "../utils/validation"

type CitizenOrganization = { id: string; name: string; code: string }
type CitizenCaseSubmission = {
  id: string
  referenceNumber: string
  additionalDocument: { originalFileName: string } | null
}

export type CitizenIntakeProps = {
  onSubmitted?: (submission: CitizenCaseSubmission) => void
  onTrackCase?: (caseId: string) => void
}

type FieldErrors = Partial<
  Record<"organizationId" | "title" | "description" | "document", string>
>

export function CitizenIntake({ onSubmitted, onTrackCase }: CitizenIntakeProps = {}) {
  const [organizations, setOrganizations] = useState<CitizenOrganization[]>([])
  const [organizationId, setOrganizationId] = useState("")
  const [title, setTitle] = useState("")
  const [description, setDescription] = useState("")
  const [document, setDocument] = useState<File | null>(null)
  const [touched, setTouched] = useState<Record<string, boolean>>({})
  const [submitted, setSubmitted] = useState(false)
  const [loadingOrganizations, setLoadingOrganizations] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)
  const [submittedCaseId, setSubmittedCaseId] = useState<string | null>(null)
  const [retryVersion, setRetryVersion] = useState(0)

  const organizationRef = useRef<HTMLSelectElement | null>(null)
  const titleRef = useRef<HTMLInputElement | null>(null)
  const descriptionRef = useRef<HTMLTextAreaElement | null>(null)
  const documentRef = useRef<HTMLInputElement | null>(null)

  useEffect(() => {
    let mounted = true

    void requestJson<CitizenOrganization[]>(
      "/api/citizens/organizations",
      undefined,
      "",
    )
      .then((loaded) => {
        if (mounted === false) return
        setOrganizations(loaded)
        setOrganizationId((current) =>
          current && loaded.some((item) => item.id === current)
            ? current
            : loaded[0]?.id ?? "")
        setError(null)
      })
      .catch((reason: unknown) => {
        if (mounted) {
          setError(reason instanceof Error
            ? reason.message
            : "Organizations could not be loaded.")
        }
      })
      .finally(() => {
        if (mounted) setLoadingOrganizations(false)
      })

    return () => {
      mounted = false
    }
  }, [retryVersion])

  const errors: FieldErrors = {}

  if (organizationId.length === 0) {
    errors.organizationId = "Select an organization."
  }

  const titleError = validateCaseTitle(title)
  if (titleError !== null) errors.title = titleError

  const descriptionError = validateCaseDescription(description)
  if (descriptionError !== null) errors.description = descriptionError

  const fileError = validateFile(document)
  if (fileError !== null) errors.document = fileError

  function errorFor(field: keyof FieldErrors): string | null {
    if (submitted === false && touched[field] !== true) return null
    return errors[field] ?? null
  }

  function markTouched(field: keyof FieldErrors) {
    setTouched((current) => ({ ...current, [field]: true }))
  }

  function focusFirstInvalid() {
    if (errors.organizationId !== undefined) {
      organizationRef.current?.focus()
    } else if (errors.title !== undefined) {
      titleRef.current?.focus()
    } else if (errors.description !== undefined) {
      descriptionRef.current?.focus()
    } else if (errors.document !== undefined) {
      documentRef.current?.focus()
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    setSubmitted(true)
    setError(null)
    setSuccess(null)
    setSubmittedCaseId(null)

    if (Object.keys(errors).length > 0) {
      setError("Fix the highlighted fields before submitting your request.")
      focusFirstInvalid()
      return
    }

    const payload = new FormData()
    payload.set("organizationId", organizationId)
    payload.set("title", normalizeWhitespace(title))
    payload.set("description", normalizeMultiline(description))
    if (document !== null) payload.set("additionalDocument", document)

    setSubmitting(true)

    try {
      const result = await requestJson<CitizenCaseSubmission>(
        "/api/citizens/cases",
        { method: "POST", body: payload },
        "",
      )
      setSuccess(
        "Request " +
          formatReference(result.referenceNumber) +
          " was submitted successfully" +
          (result.additionalDocument
            ? " with " + result.additionalDocument.originalFileName + "."
            : "."),
      )
      setSubmittedCaseId(result.id)
      setTitle("")
      setDescription("")
      setDocument(null)
      setTouched({})
      setSubmitted(false)
      form.reset()
      onSubmitted?.(result)
    } catch (reason) {
      setError(reason instanceof Error
        ? reason.message
        : "Your request could not be submitted. Please retry.")
    } finally {
      setSubmitting(false)
    }
  }

  const organizationError = errorFor("organizationId")
  const visibleTitleError = errorFor("title")
  const visibleDescriptionError = errorFor("description")
  const visibleFileError = errorFor("document")

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">PUBLIC SERVICE PORTAL</p>
          <h1>Request public assistance</h1>
          <p className="muted">Select the responsible organization, describe the issue, and attach any supporting file.</p>
        </div>
      </div>

      <section className="case-card citizen-card">
        <form noValidate onSubmit={(event) => void submit(event)}>
          <label htmlFor="intake-organization">
            Organization
            <select
              aria-describedby={
                organizationError !== null ? "intake-organization-error" : undefined
              }
              aria-invalid={organizationError !== null}
              disabled={loadingOrganizations || organizations.length === 0}
              id="intake-organization"
              onBlur={() => markTouched("organizationId")}
              onChange={(event) => {
                setOrganizationId(event.target.value)
                setError(null)
              }}
              ref={organizationRef}
              required
              value={organizationId}
            >
              <option value="">Select an organization</option>
              {organizations.map((organization) => (
                <option key={organization.id} value={organization.id}>
                  {organization.name}
                </option>
              ))}
            </select>
          </label>
          {organizationError !== null && (
            <span
              className="field-error"
              id="intake-organization-error"
              role="alert"
            >
              {organizationError}
            </span>
          )}

          <label htmlFor="intake-title">
            Issue title
            <input
              aria-describedby={visibleTitleError !== null ? "intake-title-error" : undefined}
              aria-invalid={visibleTitleError !== null}
              id="intake-title"
              maxLength={MAX_CASE_TITLE_LENGTH}
              minLength={MIN_CASE_TITLE_LENGTH}
              onBlur={() => markTouched("title")}
              onChange={(event) => {
                setTitle(event.target.value)
                setError(null)
              }}
              placeholder="What do you need help with?"
              ref={titleRef}
              required
              value={title}
            />
          </label>
          {visibleTitleError !== null && (
            <span className="field-error" id="intake-title-error" role="alert">
              {visibleTitleError}
            </span>
          )}

          <label htmlFor="intake-description">
            Details
            <textarea
              aria-describedby={
                visibleDescriptionError !== null
                  ? "intake-description-error intake-description-hint"
                  : "intake-description-hint"
              }
              aria-invalid={visibleDescriptionError !== null}
              id="intake-description"
              maxLength={MAX_CASE_DESCRIPTION_LENGTH}
              minLength={MIN_CASE_DESCRIPTION_LENGTH}
              onBlur={() => markTouched("description")}
              onChange={(event) => {
                setDescription(event.target.value)
                setError(null)
              }}
              placeholder="Include location, dates, and relevant context."
              ref={descriptionRef}
              required
              value={description}
            />
          </label>
          {visibleDescriptionError !== null && (
            <span
              className="field-error"
              id="intake-description-error"
              role="alert"
            >
              {visibleDescriptionError}
            </span>
          )}
          <small className="muted" id="intake-description-hint">
            {description.length}/{MAX_CASE_DESCRIPTION_LENGTH} characters
          </small>

          <label htmlFor="intake-document">
            Additional document (optional, any file type; maximum{" "}
            {formatFileSize(MAX_FILE_SIZE_BYTES)})
            <input
              aria-describedby={
                visibleFileError !== null ? "intake-document-error" : undefined
              }
              aria-invalid={visibleFileError !== null}
              id="intake-document"
              onBlur={() => markTouched("document")}
              onChange={(event) => {
                const selected = event.target.files?.[0] ?? null
                setDocument(selected)
                markTouched("document")
                setError(validateFile(selected))
              }}
              ref={documentRef}
              type="file"
            />
          </label>
          {visibleFileError !== null && (
            <span className="field-error" id="intake-document-error" role="alert">
              {visibleFileError}
            </span>
          )}

          {error !== null && (
            <div className="auth-error" role="alert">
              {error}
              <button
                className="secondary-button"
                disabled={loadingOrganizations || submitting}
                onClick={() => {
                  setLoadingOrganizations(true)
                  setRetryVersion((version) => version + 1)
                }}
                // Keep the click target stable when the focused field blurs.
                onMouseDown={(event) => event.preventDefault()}
                type="button"
              >
                Retry
              </button>
            </div>
          )}
          {success !== null && (
            <div className="success-message citizen-success" role="status">
              <p>{success}</p>
              {submittedCaseId !== null && onTrackCase !== undefined && (
                <button
                  className="secondary-button"
                  onClick={() => onTrackCase(submittedCaseId)}
                  // Keep the click target stable when the focused field blurs.
                  onMouseDown={(event) => event.preventDefault()}
                  type="button"
                >
                  Track this request
                </button>
              )}
            </div>
          )}
          {organizations.length === 0 && !loadingOrganizations && error === null && (
            <p className="muted">No active organizations are accepting requests.</p>
          )}
          <button
            className="primary-button"
            disabled={loadingOrganizations || submitting || organizations.length === 0}
            // Keep focus on the active field until the click completes. Without
            // this, blurring a field renders its validation message mid-click,
            // which moves the button and swallows the click that should submit.
            onMouseDown={(event) => event.preventDefault()}
            type="submit"
          >
            {submitting ? "Submitting…" : "Submit request"}
          </button>
        </form>
      </section>
    </>
  )
}
