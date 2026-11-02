import type { OrganizationWorkspaceOption, PagedResponse, StaffMember } from '../domain'
import { StaffDirectory } from './StaffDirectory'

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
      <StaffDirectory
        staff={staffPage.items}
        result={staffPage}
        query={staffQuery}
        onQueryChange={onStaffQueryChange}
        canManage={false}
        authenticated
        loading={loading}
        onToggle={async () => undefined}
        onUpdatePhone={async () => undefined}
      />
    </>
  )
}
