import { useState, type FormEvent } from 'react'
import { StatusPill } from './StatusPill'
import {
  statusLabels,
  statusOptions,
  type AssignmentEntry,
  type CaseDocument,
  type CaseRecord,
  type CaseStatus,
  type HistoryEntry,
  type StaffMember,
} from '../domain'

export function CaseDetails({
  caseRecord,
  history,
  assignmentHistory,
  documents,
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
}: {
  caseRecord: CaseRecord | null
  history: HistoryEntry[]
  assignmentHistory: AssignmentEntry[]
  documents: CaseDocument[]
  staff: StaffMember[]
  canAssignCases: boolean
  canEditCase: boolean
  actionLoading: boolean
  onStatusChange: (status: CaseStatus) => Promise<void>
  onUpdate: (title: string, description: string) => Promise<boolean>
  canDeleteCase: boolean
  onDelete: () => Promise<void>
  onAssignmentChange: (
    staffMemberId: string,
    role: 'CaseWorker' | 'Viewer',
  ) => Promise<void>
  onUpload: (file: File) => Promise<void>
  onDownload: (document: CaseDocument) => Promise<void>
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
            void onUpdate(editTitle.trim(), editDescription.trim()).then((saved) => {
              if (saved) {
                setCaseDraft({
                  caseId: caseRecord.id,
                  title: editTitle.trim(),
                  description: editDescription.trim(),
                })
              }
            })
          }}
        >
          <label>
            Case title
            <input
              maxLength={200}
              minLength={3}
              onChange={(event) => setCaseDraft({
                caseId: caseRecord.id,
                title: event.target.value,
                description: editDescription,
              })}
              required
              value={editTitle}
            />
          </label>
          <label>
            Description
            <textarea
              maxLength={5000}
              minLength={10}
              onChange={(event) => setCaseDraft({
                caseId: caseRecord.id,
                title: editTitle,
                description: event.target.value,
              })}
              required
              value={editDescription}
            />
          </label>
          <button
            className="secondary-button"
            disabled={
              actionLoading ||
              caseRecord.id.startsWith('demo-') ||
              (editTitle.trim() === caseRecord.title &&
                editDescription.trim() === caseRecord.description)
            }
            type="submit"
          >
            Save case details
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
        <label>
          Status
          <select
            disabled={actionLoading || !canEditCase || caseRecord.id.startsWith('demo-')}
            value={caseRecord.status}
            onChange={(event) => void onStatusChange(event.target.value as CaseStatus)}
          >
            {statusOptions.map((status) => (
              <option key={status} value={status}>{statusLabels[status]}</option>
            ))}
          </select>
        </label>

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
            <label className="upload-control">
              Attach supporting document
              <input
                disabled={actionLoading || !canEditCase}
                type="file"
                onChange={(event) => {
                  const file = event.target.files?.[0]
                  if (file) void onUpload(file)
                  event.currentTarget.value = ''
                }}
              />
            </label>

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
                    <small>{Math.ceil(document.sizeBytes / 1024)} KB</small>
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
                <small>{new Date(item.changedAt).toLocaleString()}</small>
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
                <small>{new Date(item.assignedAt).toLocaleString()}</small>
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
            Delete
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
