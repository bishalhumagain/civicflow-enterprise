import type { PagedResponse, StaffMember } from '../domain'
import { PaginationControls } from './PaginationControls'

/**
 * The organization's team contact list. Membership roles, activation, and
 * invitations are managed in the Organizations administration surface so
 * there is a single workflow for each operation.
 */
export function StaffDirectory({
  staff,
  authenticated,
  loading,
  result,
  query,
  onQueryChange,
}: {
  staff: StaffMember[]
  authenticated: boolean
  loading: boolean
  result: Pick<PagedResponse<StaffMember>, 'page' | 'pageSize' | 'totalCount' | 'totalPages'>
  query: { page: number; pageSize: number; search: string; isActive: boolean | null }
  onQueryChange: (query: { page: number; pageSize: number; search: string; isActive: boolean | null }) => void
}) {
  function getInitials(name: string): string {
    const parts = name.trim().split(/\s+/)
    if (parts.length === 1) {
      return parts[0].slice(0, 2).toUpperCase()
    }
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ORGANIZATION DIRECTORY</p>
          <h1>Team contacts</h1>
          <p className="muted">View the staff directory and case assignment contacts.</p>
        </div>
      </div>

      <section className="case-card directory-card">
        <h2>Active staff</h2>
        <div className="data-grid-toolbar">
          <label className="data-grid-filter">
            <span>Search staff</span>
            <input aria-label="Search staff" value={query.search} onChange={(event) => onQueryChange({ ...query, page: 1, search: event.target.value })} />
          </label>
          <label className="data-grid-filter">
            <span>Status</span>
            <select aria-label="Filter staff by status" value={query.isActive === null ? '' : String(query.isActive)} onChange={(event) => onQueryChange({ ...query, page: 1, isActive: event.target.value === '' ? null : event.target.value === 'true' })}>
              <option value="">All statuses</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </label>
        </div>
        {!authenticated ? (
          <p className="muted empty-state">Sign in with a staff identity to load the organization directory.</p>
        ) : staff.length === 0 ? (
          <p className="muted empty-state">No staff members were returned, or this role cannot access staff administration.</p>
        ) : (
          <div className="directory-grid">
            {staff.map((member) => (
              <article className="directory-member" key={member.id}>
                <span className="avatar">{getInitials(member.fullName)}</span>
                <div className="directory-member-content">
                  <strong>{member.fullName}</strong>
                  <span>{member.phoneNumber ?? 'No phone number'}</span>
                  <span className="role-badge">{member.organizationRole ?? 'Staff'}</span>
                </div>
              </article>
            ))}
          </div>
        )}
        <PaginationControls result={result} loading={loading}
          onPageChange={(page) => onQueryChange({ ...query, page })}
          onPageSizeChange={(pageSize) => onQueryChange({ ...query, page: 1, pageSize })} />
      </section>
    </>
  )
}
