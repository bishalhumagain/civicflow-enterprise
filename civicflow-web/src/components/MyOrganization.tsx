import type { OrganizationWorkspaceOption, PagedResponse, StaffMember } from '../domain'
import { PaginationControls } from './PaginationControls'

export function MyOrganization({
  organization,
  staffPage,
  staffQuery,
  onStaffQueryChange,
  loading,
}: {
  organization: OrganizationWorkspaceOption | null
  staffPage: PagedResponse<StaffMember>
  staffQuery: { page: number; pageSize: number; search: string; isActive: boolean | null }
  onStaffQueryChange: (query: { page: number; pageSize: number; search: string; isActive: boolean | null }) => void
  loading: boolean
}) {
  if (!organization) {
    return (
      <section className="case-card empty-state">
        <h1>My Organization</h1>
        <p className="muted">No active organization membership is available for this account.</p>
      </section>
    )
  }

  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ORGANIZATION WORKSPACE</p>
          <h1>My Organization</h1>
          <p className="muted">Organization details and team contacts for your active membership.</p>
        </div>
      </div>
      <section className="case-card my-organization-card">
        <h2>{organization.name}</h2>
        <dl>
          <div><dt>Organization code</dt><dd>{organization.code}</dd></div>
          <div><dt>Your organization role</dt><dd><span className="role-badge">{organization.role}</span></dd></div>
        </dl>
      </section>
      <section className="case-card directory-card">
        <h2>Team contacts</h2>
        <div className="data-grid-toolbar">
          <label className="data-grid-filter">
            <span>Search staff</span>
            <input
              aria-label="Search staff"
              value={staffQuery.search}
              onChange={(event) => onStaffQueryChange({ ...staffQuery, page: 1, search: event.target.value })}
            />
          </label>
          <label className="data-grid-filter">
            <span>Status</span>
            <select
              aria-label="Filter staff by status"
              value={staffQuery.isActive === null ? '' : String(staffQuery.isActive)}
              onChange={(event) =>
                onStaffQueryChange({
                  ...staffQuery,
                  page: 1,
                  isActive: event.target.value === '' ? null : event.target.value === 'true',
                })
              }
            >
              <option value="">All statuses</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </label>
        </div>
        {staffPage.items.length === 0 ? (
          <p className="muted empty-state">No staff members were returned, or this role cannot access staff administration.</p>
        ) : (
          <div className="directory-grid">
            {staffPage.items.map((member) => (
              <article className="directory-member" key={member.id}>
                <span className="avatar">{member.fullName.slice(0, 2).toUpperCase()}</span>
                <div className="directory-member-content">
                  <strong>{member.fullName}</strong>
                  <span>{member.email}</span>
                  <span className="role-badge">{member.organizationRole ?? 'Staff'}</span>
                  <div className="contact-details">
                    <span className="phone-number">Contact details restricted</span>
                  </div>
                  <small className="status-badge">{member.isActive ? 'Active' : 'Inactive'}</small>
                </div>
              </article>
            ))}
          </div>
        )}
        <PaginationControls
          result={staffPage}
          loading={loading}
          onPageChange={(page) => onStaffQueryChange({ ...staffQuery, page })}
          onPageSizeChange={(pageSize) => onStaffQueryChange({ ...staffQuery, page: 1, pageSize })}
        />
      </section>
    </>
  )
}
