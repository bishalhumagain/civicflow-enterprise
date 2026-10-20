import { statusLabels, type CaseStatus } from '../domain'

export function StatusPill({ status }: { status: CaseStatus }) {
  return <span className={`status-pill ${status.toLowerCase()}`}><i aria-hidden="true" />{statusLabels[status]}</span>
}

