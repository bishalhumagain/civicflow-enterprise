import { useEffect, useRef, useState, type FormEvent } from 'react'
import { StatusPill } from './StatusPill'
import { Icon } from './Icon'
import { DocumentPreviewButton } from './DocumentPreviewButton'
import { formatDateTime, formatFileSize } from '../utils/format'
import {
  MAX_CASE_DESCRIPTION_LENGTH,
  MAX_CASE_TITLE_LENGTH,
  MAX_CASE_UPDATE_LENGTH,
  MAX_FILE_SIZE_BYTES,
  MAX_STATUS_EXPLANATION_LENGTH,
  MIN_CASE_DESCRIPTION_LENGTH,
  MIN_CASE_TITLE_LENGTH,
  normalizeMultiline,
  normalizeWhitespace,
  validateCaseDescription,
  validateCaseTitle,
  validateCaseUpdate,
  validateFile,
} from '../utils/validation'
import {
  statusLabels,
  statusOptions,
  type AssignmentEntry,
  type CaseDocument,
  type CasePriority,
  type CaseRecord,
  type CaseStatus,
  type CaseUpdate,
  type CaseUpdateKind,
  type HistoryEntry,
  type StaffMember,
} from '../domain'

const updateKindLabels: Record<CaseUpdateKind, string> = {
  PublicUpdate: 'Public update',
  InformationRequest: 'Information request',
  InternalNote: 'Internal note',
  CitizenResponse: 'Citizen response',
}

const staffUpdateKinds: CaseUpdateKind[] = [
  'PublicUpdate',
  'InformationRequest',
  'InternalNote',
]

const priorityOptions: CasePriority[] = ['Critical', 'High', 'Normal', 'Low']

export type CaseDetailsDraft = {
  title: string
  description: string
  status: CaseStatus
  priority: CasePriority
  assignedToStaffMemberId: string
  assignedRole: 'CaseWorker' | 'Viewer'
  statusExplanation: string
  statusExplanationVisible: boolean
}

function createDraft(caseRecord: CaseRecord): CaseDetailsDraft {
  return {
    title: caseRecord.title,
    description: caseRecord.description,
    status: caseRecord.status,
    priority: caseRecord.priority,
    assignedToStaffMemberId: caseRecord.assignedToStaffMemberId ?? '',
    assignedRole: caseRecord.assignedRole ?? 'CaseWorker',
    statusExplanation: '',
    statusExplanationVisible: false,
  }
}

export function CaseDetails({
  caseRecord,
  history,
  assignmentHistory,
  documents,
  updates,
  staff,
  canAssignCases,
  canEditCase,
  actionLoading,
  onSave,
  onUpload,
  onDownload,
  onLoadDocument,
  onUnsavedChangesChange,
  onAddUpdate,
}: {
  caseRecord: CaseRecord | null
  history: HistoryEntry[]
  assignmentHistory: AssignmentEntry[]
  documents: CaseDocument[]
  updates: CaseUpdate[]
  staff: StaffMember[]
  canAssignCases: boolean
  canEditCase: boolean
  actionLoading: boolean
  onSave: (draft: CaseDetailsDraft) => Promise<boolean>
  onUpload: (file: File, visibleToCitizen: boolean) => Promise<void>
  onDownload: (document: CaseDocument) => Promise<void>
  onLoadDocument: (document: CaseDocument) => Promise<Blob>
  onUnsavedChangesChange: (hasUnsavedChanges: boolean) => void
  onAddUpdate: (kind: CaseUpdateKind, message: string) => Promise<boolean>
}) {
  const [draftState, setDraftState] = useState<{
    caseId: string
    values: CaseDetailsDraft
  } | null>(null)
  const [documentVisible, setDocumentVisible] = useState(false)
  const [documentError, setDocumentError] = useState<string | null>(null)
  const [updateKind, setUpdateKind] =
    useState<CaseUpdateKind>('PublicUpdate')
  const [updateMessage, setUpdateMessage] = useState('')
  const [sendingUpdate, setSendingUpdate] = useState(false)

  const editTitleRef = useRef<HTMLInputElement | null>(null)
  const editDescriptionRef = useRef<HTMLTextAreaElement | null>(null)
  const updateMessageRef = useRef<HTMLTextAreaElement | null>(null)

  const draft = caseRecord
    ? draftState?.caseId === caseRecord.id
      ? draftState.values
      : createDraft(caseRecord)
    : null

  const hasUnsavedChanges = !!caseRecord && !!draft && (
    draft.title !== caseRecord.title ||
    draft.description !== caseRecord.description ||
    draft.status !== caseRecord.status ||
    draft.priority !== caseRecord.priority ||
    (canAssignCases && (
      draft.assignedToStaffMemberId !== (caseRecord.assignedToStaffMemberId ?? '') ||
      draft.assignedRole !== (caseRecord.assignedRole ?? 'CaseWorker')
    ))
  )

  useEffect(() => {
    onUnsavedChangesChange(hasUnsavedChanges)
  }, [hasUnsavedChanges, onUnsavedChangesChange])

  useEffect(() => {
    if (!hasUnsavedChanges) return

    const warnBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault()
      event.returnValue = ''
    }

    window.addEventListener('beforeunload', warnBeforeUnload)
    return () => window.removeEventListener('beforeunload', warnBeforeUnload)
  }, [hasUnsavedChanges])

  if (!caseRecord || !draft) {
    return (
      <aside className="detail-card">
        <p className="muted empty-state">Select a case to view details.</p>
      </aside>
    )
  }

  const record = caseRecord
  const isDemo = caseRecord.id.startsWith('demo-')
  const editTitleError = validateCaseTitle(draft.title)
  const editDescriptionError = validateCaseDescription(draft.description)
  const updateError = validateCaseUpdate(updateMessage)
  const assignedStaffName = staff.find(
    (member) => member.id === caseRecord.assignedToStaffMemberId,
  )?.fullName

  function updateDraft(patch: Partial<CaseDetailsDraft>) {
    const base = draftState && draftState.caseId === record.id
      ? draftState.values
      : createDraft(record)
    setDraftState({
      caseId: record.id,
      values: { ...base, ...patch },
    })
  }

  function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (editTitleError !== null) {
      editTitleRef.current?.focus()
      return
    }

    if (editDescriptionError !== null) {
      editDescriptionRef.current?.focus()
      return
    }

    if (!hasUnsavedChanges) return

    const statusExplanation = normalizeMultiline(draft.statusExplanation)
    const payload: CaseDetailsDraft = {
      ...draft,
      title: normalizeWhitespace(draft.title),
      description: normalizeMultiline(draft.description),
      statusExplanation,
      statusExplanationVisible:
        statusExplanation.length > 0 && draft.statusExplanationVisible,
    }

    void onSave(payload).then((saved) => {
      if (saved) {
        setDraftState(null)
        onUnsavedChangesChange(false)
      }
    })
  }

  return (
    <aside className="detail-card">
      <div className="detail-header">
        <span>CASE DETAILS</span>
      </div>

      <div className="detail-title">
        <span className="case-reference">{caseRecord.referenceNumber}</span>
        <StatusPill status={caseRecord.status} />
        <span className={`priority ${caseRecord.priority.toLowerCase()}`}>
          <i aria-hidden="true" />{caseRecord.priority} priority
        </span>
      </div>

      {canEditCase ? (
        <form className="case-edit-form case-details-form" onSubmit={handleSave}>
          <label>
            Case title
            <input
              aria-describedby={
                editTitleError !== null ? 'case-edit-title-error' : undefined
              }
              aria-invalid={editTitleError !== null}
              id="case-edit-title"
              maxLength={MAX_CASE_TITLE_LENGTH}
              minLength={MIN_CASE_TITLE_LENGTH}
              onChange={(event) => updateDraft({ title: event.target.value })}
              ref={editTitleRef}
              required
              value={draft.title}
            />
          </label>
          {editTitleError !== null && (
            <span className="field-error" id="case-edit-title-error" role="alert">
              {editTitleError}
            </span>
          )}
          <label>
            Description
            <textarea
              aria-describedby={
                editDescriptionError !== null
                  ? 'case-edit-description-error'
                  : undefined
              }
              aria-invalid={editDescriptionError !== null}
              id="case-edit-description"
              maxLength={MAX_CASE_DESCRIPTION_LENGTH}
              minLength={MIN_CASE_DESCRIPTION_LENGTH}
              onChange={(event) =>
                updateDraft({ description: event.target.value })
              }
              ref={editDescriptionRef}
              required
              value={draft.description}
            />
          </label>
          {editDescriptionError !== null && (
            <span
              className="field-error"
              id="case-edit-description-error"
              role="alert"
            >
              {editDescriptionError}
            </span>
          )}

          <div className="detail-section">
            <h3>Workflow</h3>
            <label>
              Status
              <select
                disabled={actionLoading || isDemo}
                onChange={(event) =>
                  updateDraft({ status: event.target.value as CaseStatus })
                }
                value={draft.status}
              >
                {statusOptions.map((status) => (
                  <option key={status} value={status}>
                    {statusLabels[status]}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Explanation <small className="muted">(optional)</small>
              <textarea
                aria-describedby="status-explanation-hint"
                disabled={actionLoading || isDemo}
                id="status-explanation"
                maxLength={MAX_STATUS_EXPLANATION_LENGTH}
                onChange={(event) =>
                  updateDraft({ statusExplanation: event.target.value })
                }
                placeholder="Record why the case is moving to this status."
                rows={3}
                value={draft.statusExplanation}
              />
            </label>
            <small className="muted" id="status-explanation-hint">
              {draft.statusExplanation.length}/{MAX_STATUS_EXPLANATION_LENGTH}{' '}
              characters. Explanations are internal unless shared below.
            </small>
            <label className="checkbox-control">
              <input
                checked={draft.statusExplanationVisible}
                disabled={
                  actionLoading ||
                  isDemo ||
                  draft.statusExplanation.trim().length === 0
                }
                onChange={(event) =>
                  updateDraft({
                    statusExplanationVisible: event.target.checked,
                  })
                }
                type="checkbox"
              />
              <span>Include this explanation in the citizen timeline</span>
            </label>

            <label>
              Priority
              <select
                aria-label="Case priority"
                disabled={actionLoading || isDemo}
                onChange={(event) =>
                  updateDraft({ priority: event.target.value as CasePriority })
                }
                value={draft.priority}
              >
                {priorityOptions.map((priority) => (
                  <option key={priority} value={priority}>
                    {priority}
                  </option>
                ))}
              </select>
            </label>

            {canAssignCases && (
              <>
                <label>
                  Assign to
                  <select
                    disabled={actionLoading || isDemo}
                    onChange={(event) =>
                      updateDraft({
                        assignedToStaffMemberId: event.target.value,
                      })
                    }
                    value={draft.assignedToStaffMemberId}
                  >
                    <option disabled value="">
                      Select staff member
                    </option>
                    {staff.filter((member) => member.isActive).map((member) => (
                      <option key={member.id} value={member.id}>
                        {member.fullName}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Case role
                  <select
                    disabled={actionLoading || isDemo}
                    onChange={(event) =>
                      updateDraft({
                        assignedRole: event.target.value as
                          | 'CaseWorker'
                          | 'Viewer',
                      })
                    }
                    value={draft.assignedRole}
                  >
                    <option value="CaseWorker">CaseWorker</option>
                    <option value="Viewer">Viewer</option>
                  </select>
                </label>
              </>
            )}
          </div>

          <div className="case-save-bar">
            <span className="muted" aria-live="polite">
              {hasUnsavedChanges
                ? 'You have unsaved changes.'
                : 'All changes saved.'}
            </span>
            <button
              className="primary-button"
              disabled={
                actionLoading ||
                isDemo ||
                !hasUnsavedChanges ||
                editTitleError !== null ||
                editDescriptionError !== null
              }
              type="submit"
            >
              <Icon className="button-icon" name="save" />
              <span>{actionLoading ? 'Saving…' : 'Save changes'}</span>
            </button>
          </div>
        </form>
      ) : (
        <>
          <h2 className="case-details-title">{caseRecord.title}</h2>
          <p className="case-description">{caseRecord.description}</p>

          <div className="detail-section">
            <h3>Workflow</h3>
            <dl className="case-workflow-summary">
              <div>
                <dt>Status</dt>
                <dd>
                  <StatusPill status={caseRecord.status} />
                </dd>
              </div>
              <div>
                <dt>Priority</dt>
                <dd>{caseRecord.priority}</dd>
              </div>
              <div>
                <dt>Assigned to</dt>
                <dd>
                  {assignedStaffName ?? 'Unassigned'}
                  {assignedStaffName ? ` (${caseRecord.assignedRole ?? 'CaseWorker'})` : ''}
                </dd>
              </div>
            </dl>
          </div>
        </>
      )}

      <div className="detail-section">
        <h3>Documents</h3>
        {isDemo ? (
          <p className="muted">Documents are available for live cases.</p>
        ) : (
          <>
            <label className="checkbox-control">
              <input
                checked={documentVisible}
                disabled={actionLoading || !canEditCase}
                onChange={(event) => setDocumentVisible(event.target.checked)}
                type="checkbox"
              />
              <span>Share new documents with the citizen</span>
            </label>
            <label className="upload-control">
              Attach supporting document (maximum{" "}
              {formatFileSize(MAX_FILE_SIZE_BYTES)})
              <input
                disabled={actionLoading || !canEditCase}
                aria-describedby={
                  documentError !== null ? 'case-document-error' : undefined
                }
                aria-invalid={documentError !== null}
                type="file"
                onChange={(event) => {
                  const file = event.target.files?.[0] ?? null
                  const validationError = validateFile(file)
                  setDocumentError(validationError)
                  if (file !== null && validationError === null) {
                    void onUpload(file, documentVisible)
                  }
                  event.currentTarget.value = ''
                }}
              />
            </label>

            {documentError !== null && (
              <p className="field-error" id="case-document-error" role="alert">
                {documentError}
              </p>
            )}

            {documents.length === 0 ? (
              <p className="muted">No documents attached.</p>
            ) : (
              <ul>
                {documents.map((document) => (
                  <li key={document.id}>
                    <DocumentPreviewButton
                      document={document}
                      loadDocument={() => onLoadDocument(document)}
                    />{' '}
                    <button
                      className="document-link"
                      disabled={actionLoading}
                      onClick={() => void onDownload(document)}
                      type="button"
                    >
                      {document.originalFileName}
                    </button>{' '}
                    <small>{formatFileSize(document.sizeBytes)}</small>{' '}
                    <span
                      className={
                        document.isVisibleToCitizen
                          ? 'visibility-badge visible'
                          : 'visibility-badge internal'
                      }
                    >
                      {document.isVisibleToCitizen
                        ? 'Shared with citizen'
                        : 'Internal'}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </div>

      <div className="detail-section">
        <h3>Citizen communication</h3>
        {isDemo ? (
          <p className="muted">Updates are available for live cases.</p>
        ) : (
          <>
            {canEditCase && (
              <form
                className="case-update-form"
                onSubmit={(event: FormEvent<HTMLFormElement>) => {
                  event.preventDefault()
                  const message = normalizeMultiline(updateMessage)
                  if (message.length === 0 || updateError !== null) {
                    updateMessageRef.current?.focus()
                    return
                  }
                  setSendingUpdate(true)
                  void onAddUpdate(updateKind, message)
                    .then((saved) => {
                      if (saved) setUpdateMessage('')
                    })
                    .finally(() => setSendingUpdate(false))
                }}
              >
                <label>
                  Update type
                  <select
                    disabled={sendingUpdate}
                    onChange={(event) =>
                      setUpdateKind(event.target.value as CaseUpdateKind)
                    }
                    value={updateKind}
                  >
                    {staffUpdateKinds.map((kind) => (
                      <option key={kind} value={kind}>
                        {updateKindLabels[kind]}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  Message
                  <textarea
                    aria-describedby={
                      updateError !== null ? 'case-update-error' : undefined
                    }
                    aria-invalid={updateError !== null}
                    disabled={sendingUpdate}
                    id="case-update-message"
                    maxLength={MAX_CASE_UPDATE_LENGTH}
                    minLength={1}
                    onChange={(event) => setUpdateMessage(event.target.value)}
                    placeholder={
                      updateKind === 'InternalNote'
                        ? 'Only organization staff can read this note.'
                        : 'This message will appear in the citizen timeline.'
                    }
                    ref={updateMessageRef}
                    required
                    rows={4}
                    value={updateMessage}
                  />
                </label>
                {updateError !== null && (
                  <span
                    className="field-error"
                    id="case-update-error"
                    role="alert"
                  >
                    {updateError}
                  </span>
                )}
                <small className="muted">
                  {updateMessage.length}/{MAX_CASE_UPDATE_LENGTH} characters
                </small>
                <p className="muted form-hint">
                  {updateKind === 'InternalNote'
                    ? 'Internal notes stay within the organization and never appear in the citizen timeline.'
                    : updateKind === 'InformationRequest'
                      ? 'The case moves to Awaiting information and the citizen can reply.'
                      : 'The citizen is notified and the update becomes part of the visible timeline.'}
                </p>
                <button
                  className="secondary-button"
                  disabled={
                    sendingUpdate ||
                    updateMessage.trim().length === 0 ||
                    updateError !== null
                  }
                  type="submit"
                >
                  <Icon className="button-icon" name="save" />
                  <span>{sendingUpdate ? 'Publishing…' : 'Publish update'}</span>
                </button>
              </form>
            )}

            {updates.length === 0 ? (
              <p className="muted">No updates recorded yet.</p>
            ) : (
              <ul className="case-update-list">
                {updates.map((update) => (
                  <li
                    className={
                      update.isVisibleToCitizen
                        ? 'case-update visible'
                        : 'case-update internal'
                    }
                    key={update.id}
                  >
                    <div className="case-update-header">
                      <strong>{updateKindLabels[update.kind]}</strong>
                      <span
                        className={
                          update.isVisibleToCitizen
                            ? 'visibility-badge visible'
                            : 'visibility-badge internal'
                        }
                      >
                        {update.isVisibleToCitizen
                          ? 'Visible to citizen'
                          : 'Staff only'}
                      </span>
                    </div>
                    <p>{update.message}</p>
                    <small>{formatDateTime(update.createdAt)}</small>
                  </li>
                ))}
              </ul>
            )}
          </>
        )}
      </div>

      <div className="detail-section timeline">
        <h3>Status history</h3>
        {isDemo || history.length === 0 ? (
          <p className="muted">No recorded status changes yet.</p>
        ) : (
          history.map((item) => (
            <p key={item.id}>
              <span className="timeline-dot" />
              <span>
                <strong>{statusLabels[item.previousStatus]} → {statusLabels[item.newStatus]}</strong>
                <small>{formatDateTime(item.changedAt)}</small>
                {item.explanation && (
                  <span className="timeline-explanation">
                    {item.explanation}{' '}
                    <span
                      className={
                        item.isVisibleToCitizen
                          ? 'visibility-badge visible'
                          : 'visibility-badge internal'
                      }
                    >
                      {item.isVisibleToCitizen
                        ? 'Visible to citizen'
                        : 'Staff only'}
                    </span>
                  </span>
                )}
              </span>
            </p>
          ))
        )}
      </div>

      <div className="detail-section timeline">
        <h3>Assignment history</h3>
        {isDemo || assignmentHistory.length === 0 ? (
          <p className="muted">No recorded assignments yet.</p>
        ) : (
          assignmentHistory.map((item) => (
            <p key={item.id}>
              <span className="timeline-dot" />
              <span>
                <strong>
                  {item.previousStaffMemberId ? 'Reassigned case' : 'Case assigned'}
                  {' · '}{item.newRole}
                  {item.previousRole && item.previousRole !== item.newRole
                    ? ` (was ${item.previousRole})`
                    : ''}
                </strong>
                <small>{formatDateTime(item.assignedAt)}</small>
              </span>
            </p>
          ))
        )}
      </div>
    </aside>
  )
}
