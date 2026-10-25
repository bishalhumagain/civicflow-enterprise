import { useRef, useState, type FormEvent } from 'react'
import { StatusPill } from './StatusPill'
import { Icon } from './Icon'
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
  onStatusChange,
  onUpdate,
  canDeleteCase,
  onDelete,
  onAssignmentChange,
  onUpload,
  onDownload,
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
  onStatusChange: (
    status: CaseStatus,
    explanation?: string,
    explanationVisibleToCitizen?: boolean,
  ) => Promise<void>
  onUpdate: (title: string, description: string) => Promise<boolean>
  canDeleteCase: boolean
  onDelete: () => Promise<void>
  onAssignmentChange: (
    staffMemberId: string,
    role: 'CaseWorker' | 'Viewer',
  ) => Promise<void>
  onUpload: (file: File, visibleToCitizen: boolean) => Promise<void>
  onDownload: (document: CaseDocument) => Promise<void>
  onAddUpdate: (kind: CaseUpdateKind, message: string) => Promise<boolean>
}) {
  const [assignmentRole, setAssignmentRole] =
    useState<'CaseWorker' | 'Viewer'>('CaseWorker')
  const [caseDraft, setCaseDraft] = useState<{
    caseId: string
    title: string
    description: string
  } | null>(null)
  const [deleteConfirmationCaseId, setDeleteConfirmationCaseId] =
    useState<string | null>(null)
  const [pendingStatus, setPendingStatus] = useState<{
    caseId: string
    status: CaseStatus
  } | null>(null)
  const [statusExplanation, setStatusExplanation] = useState('')
  const [statusExplanationVisible, setStatusExplanationVisible] =
    useState(false)
  const [documentVisible, setDocumentVisible] = useState(false)
  const [documentError, setDocumentError] = useState<string | null>(null)
  const [updateKind, setUpdateKind] =
    useState<CaseUpdateKind>('PublicUpdate')
  const [updateMessage, setUpdateMessage] = useState('')
  const [sendingUpdate, setSendingUpdate] = useState(false)

  const editTitleRef = useRef<HTMLInputElement | null>(null)
  const editDescriptionRef = useRef<HTMLTextAreaElement | null>(null)
  const updateMessageRef = useRef<HTMLTextAreaElement | null>(null)

  if (!caseRecord) {
    return (
      <aside className="detail-card">
        <p className="muted empty-state">Select a case to view details.</p>
      </aside>
    )
  }

  const editTitle = caseDraft?.caseId === caseRecord.id
    ? caseDraft.title
    : caseRecord.title
  const editDescription = caseDraft?.caseId === caseRecord.id
    ? caseDraft.description
    : caseRecord.description
  const deleteConfirmationOpen = deleteConfirmationCaseId === caseRecord.id
  const selectedStatus = pendingStatus?.caseId === caseRecord.id
    ? pendingStatus.status
    : caseRecord.status
  const statusChanged = selectedStatus !== caseRecord.status
  const editTitleError = validateCaseTitle(editTitle)
  const editDescriptionError = validateCaseDescription(editDescription)
  const updateError = validateCaseUpdate(updateMessage)

  return (
    <aside className="detail-card">
      <div className="detail-header">
        <span>CASE DETAILS</span>
      </div>

      <div className="detail-title">
        <span className="case-reference">{caseRecord.referenceNumber}</span>
        <StatusPill status={caseRecord.status} />
      </div>

      {canEditCase ? (
        <form
          className="case-edit-form"
          onSubmit={(event: FormEvent<HTMLFormElement>) => {
            event.preventDefault()

            if (editTitleError !== null) {
              editTitleRef.current?.focus()
              return
            }

            if (editDescriptionError !== null) {
              editDescriptionRef.current?.focus()
              return
            }

            const normalizedTitle = normalizeWhitespace(editTitle)
            const normalizedDescription = normalizeMultiline(editDescription)

            void onUpdate(normalizedTitle, normalizedDescription).then((saved) => {
              if (saved) {
                setCaseDraft({
                  caseId: caseRecord.id,
                  title: normalizedTitle,
                  description: normalizedDescription,
                })
              }
            })
          }}
        >
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
              onChange={(event) => setCaseDraft({
                caseId: caseRecord.id,
                title: event.target.value,
                description: editDescription,
              })}
              ref={editTitleRef}
              required
              value={editTitle}
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
              onChange={(event) => setCaseDraft({
                caseId: caseRecord.id,
                title: editTitle,
                description: event.target.value,
              })}
              ref={editDescriptionRef}
              required
              value={editDescription}
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
          <button
            className="secondary-button"
            disabled={
              actionLoading ||
              caseRecord.id.startsWith('demo-') ||
              editTitleError !== null ||
              editDescriptionError !== null ||
              (editTitle.trim() === caseRecord.title &&
                editDescription.trim() === caseRecord.description)
            }
            type="submit"
          >
            <Icon className="button-icon" name="save" />
            <span>Save case details</span>
          </button>
        </form>
      ) : (
        <>
          <h2 className="case-details-title">{caseRecord.title}</h2>
          <p className="case-description">{caseRecord.description}</p>
        </>
      )}

      <div className="detail-section">
        <h3>Workflow</h3>
        <form
          className="status-change-form"
          onSubmit={(event: FormEvent<HTMLFormElement>) => {
            event.preventDefault()
            if (!statusChanged) return

            const explanation = normalizeMultiline(statusExplanation)

            void onStatusChange(
              selectedStatus,
              explanation.length > 0 ? explanation : undefined,
              explanation.length > 0 && statusExplanationVisible,
            ).then(() => {
              setPendingStatus(null)
              setStatusExplanation('')
              setStatusExplanationVisible(false)
            })
          }}
        >
          <label>
            Status
            <select
              disabled={actionLoading || !canEditCase || caseRecord.id.startsWith('demo-')}
              onChange={(event) => setPendingStatus({
                caseId: caseRecord.id,
                status: event.target.value as CaseStatus,
              })}
              value={selectedStatus}
            >
              {statusOptions.map((status) => (
                <option key={status} value={status}>{statusLabels[status]}</option>
              ))}
            </select>
          </label>
          <label>
            Explanation <small className="muted">(optional)</small>
            <textarea
              aria-describedby="status-explanation-hint"
              disabled={actionLoading || !canEditCase}
              id="status-explanation"
              maxLength={MAX_STATUS_EXPLANATION_LENGTH}
              onChange={(event) => setStatusExplanation(event.target.value)}
              placeholder="Record why the case is moving to this status."
              rows={3}
              value={statusExplanation}
            />
          </label>
          <small className="muted" id="status-explanation-hint">
            {statusExplanation.length}/{MAX_STATUS_EXPLANATION_LENGTH} characters.
            Explanations are internal unless shared below.
          </small>
          <label className="checkbox-control">
            <input
              checked={statusExplanationVisible}
              disabled={
                actionLoading ||
                !canEditCase ||
                statusExplanation.trim().length === 0
              }
              onChange={(event) =>
                setStatusExplanationVisible(event.target.checked)
              }
              type="checkbox"
            />
            <span>Include this explanation in the citizen timeline</span>
          </label>
          <button
            className="secondary-button"
            disabled={
              actionLoading ||
              !canEditCase ||
              !statusChanged ||
              caseRecord.id.startsWith('demo-')
            }
            type="submit"
          >
            <Icon className="button-icon" name="save" />
            <span>{actionLoading ? 'Updating…' : 'Apply status change'}</span>
          </button>
        </form>

        {canAssignCases && (
          <>
            <label>
              Assign to
              <select
                disabled={actionLoading || caseRecord.id.startsWith('demo-')}
                value={caseRecord.assignedToStaffMemberId ?? ''}
                onChange={(event) => {
                  if (event.target.value) {
                    void onAssignmentChange(event.target.value, assignmentRole)
                  }
                }}
              >
                <option value="">Select staff member</option>
                {staff.filter((member) => member.isActive).map((member) => (
                  <option key={member.id} value={member.id}>{member.fullName}</option>
                ))}
              </select>
            </label>
            <label>
              Case role
              <select
                disabled={actionLoading || caseRecord.id.startsWith('demo-')}
                value={caseRecord.assignedRole ?? assignmentRole}
                onChange={(event) => {
                  const role = event.target.value as 'CaseWorker' | 'Viewer'
                  setAssignmentRole(role)
                  if (caseRecord.assignedToStaffMemberId) {
                    void onAssignmentChange(caseRecord.assignedToStaffMemberId, role)
                  }
                }}
              >
                <option value="CaseWorker">CaseWorker</option>
                <option value="Viewer">Viewer</option>
              </select>
            </label>
          </>
        )}
      </div>

      <div className="detail-section">
        <h3>Documents</h3>
        {caseRecord.id.startsWith('demo-') ? (
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
        {caseRecord.id.startsWith('demo-') ? (
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
                    ? 'Internal notes never appear in the citizen timeline or API responses.'
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
        {caseRecord.id.startsWith('demo-') || history.length === 0 ? (
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
        {caseRecord.id.startsWith('demo-') || assignmentHistory.length === 0 ? (
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

      {canDeleteCase && !caseRecord.id.startsWith('demo-') && (
        <div className="case-danger-zone">
          <div>
            <strong>Delete case</strong>
            <p>This permanently removes the case and its attached documents.</p>
          </div>
          <button
            className="danger-button"
            disabled={actionLoading}
            onClick={() => setDeleteConfirmationCaseId(caseRecord.id)}
            type="button"
          >
            <Icon className="button-icon" name="trash" />
            <span>Delete</span>
          </button>
        </div>
      )}

      {deleteConfirmationOpen && (
        <div
          aria-labelledby="delete-case-title"
          aria-modal="true"
          className="case-confirmation-backdrop"
          role="dialog"
        >
          <div className="case-confirmation">
            <span className="case-confirmation-icon" aria-hidden="true">!</span>
            <h2 id="delete-case-title">Delete this case?</h2>
            <p>
              <strong>{caseRecord.referenceNumber}</strong> and its attached
              documents will be permanently deleted. This cannot be undone.
            </p>
            <div className="case-confirmation-actions">
              <button
                className="secondary-button"
                disabled={actionLoading}
                onClick={() => setDeleteConfirmationCaseId(null)}
                type="button"
              >
                Keep case
              </button>
              <button
                className="danger-button"
                disabled={actionLoading}
                onClick={() => {
                  setDeleteConfirmationCaseId(null)
                  void onDelete()
                }}
                type="button"
              >
                {actionLoading ? 'Deleting…' : 'Confirm deletion'}
              </button>
            </div>
          </div>
        </div>
      )}
    </aside>
  )
}
