import { statusLabels, type CaseStatus } from '../domain'

export function StatusPill({ status }: { status: CaseStatus }) {
  const statusClass = status.replace(/([a-z])([A-Z])/g, '$1-$2').toLowerCase()
  return <span className={`status-pill ${statusClass}`}><i aria-hidden="true" />{statusLabels[status]}</span>
}
