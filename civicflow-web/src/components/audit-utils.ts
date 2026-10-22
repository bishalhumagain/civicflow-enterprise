/**
 * Filters the audit trail can be narrowed by. The API accepts the same
 * values, so the UI never invents a filter the server cannot apply.
 */
export type AuditFilters = {
  category: string
  action: string
  search: string
  from: string
  to: string
}

export const emptyAuditFilters: AuditFilters = {
  category: '',
  action: '',
  search: '',
  from: '',
  to: '',
}
