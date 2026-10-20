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
  actionLoading,
  onStatusChange,
  onAssignmentChange,
  onUpload,
  onDownload,
}: {
  caseRecord: CaseRecord | null
  history: HistoryEntry[]
  assignmentHistory: AssignmentEntry[]
  documents: CaseDocument[]
  staff: StaffMember[]
  actionLoading: boolean
  onStatusChange: (status: CaseStatus) => Promise<void>
  onAssignmentChange: (staffMemberId: string) => Promise<void>
  onUpload: (file: File) => Promise<void>
  onDownload: (document: CaseDocument) => Promise<void>
}) {
  if (!caseRecord) {
    return (
      <aside className="detail-card">
        <p className="muted empty-state">Select a case to view details.</p>
      </aside>
    )
  }

  return (
    <aside className="detail-card">
      <div className="detail-header">
        <span>CASE DETAILS</span>
      </div>

      <div className="detail-title">
        <span className="case-reference">{caseRecord.referenceNumber}</span>
        <h2>{caseRecord.title}</h2>
        <StatusPill status={caseRecord.status} />
      </div>

      <p className="case-description">{caseRecord.description}</p>

      <div className="detail-section">
        <h3>Workflow</h3>
        <label>
          Status
          <select
            disabled={actionLoading || caseRecord.id.startsWith('demo-')}
            value={caseRecord.status}
            onChange={(event) => void onStatusChange(event.target.value as CaseStatus)}
          >
            {statusOptions.map((status) => (
              <option key={status} value={status}>{statusLabels[status]}</option>
            ))}
          </select>
        </label>

        <label>
          Assign to
          <select
            disabled={actionLoading || caseRecord.id.startsWith('demo-')}
            defaultValue=""
            onChange={(event) => void onAssignmentChange(event.target.value)}
          >
            <option value="">Select staff member</option>
            {staff.filter((member) => member.isActive).map((member) => (
              <option key={member.id} value={member.id}>{member.fullName}</option>
            ))}
          </select>
        </label>
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
                disabled={actionLoading}
                type="file"
                accept=".pdf,.jpg,.jpeg,.png,.doc,.docx"
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
                <strong>{item.previousStaffMemberId ? 'Reassigned case' : 'Case assigned'}</strong>
                <small>{new Date(item.assignedAt).toLocaleString()}</small>
              </span>
            </p>
          ))
        )}
      </div>
    </aside>
  )
}
