export type CaseStatus = 'Submitted' | 'UnderReview' | 'Assigned' | 'InProgress' | 'AwaitingInformation' | 'Resolved' | 'Closed' | 'Rejected'
export type CasePriority = 'High' | 'Normal' | 'Low'
export type WorkspaceView =
  | 'citizen-dashboard'
  | 'citizen'
  | 'profile'
  | 'cases'
  | 'staff'
  | 'activity'
  | 'organizations'
  | 'platform-directory'
  | 'platform-audit'
  | 'access-tokens'

export type CaseRecord = {
  id: string
  referenceNumber: string
  title: string
  description: string
  status: CaseStatus
  priority: CasePriority
  createdAt: string
}
export type StaffMember = { id: string; fullName: string; email: string; phoneNumber: string | null; isActive: boolean }
export type HistoryEntry = { id: string; previousStatus: CaseStatus; newStatus: CaseStatus; changedAt: string }
export type CaseDocument = { id: string; originalFileName: string; contentType: string; sizeBytes: number; uploadedAt: string }
export type AssignmentEntry = { id: string; previousStaffMemberId: string | null; newStaffMemberId: string | null; assignedAt: string; assignedByUserId: string | null }
export type CasePage = { items: CaseRecord[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export type Organization = { id: string; name: string; code: string; isActive: boolean; createdAt: string; updatedAt: string }
export type Membership = { id: string; userId: string; organizationId: string; role: string; isActive: boolean; createdAt: string; updatedAt: string }
export type AccountInvitation = {
  id: string
  organizationId: string
  email: string
  fullName: string
  phoneNumber: string | null
  role: string
  createdAt: string
  expiresAt: string
  isExpired: boolean
  isUsed: boolean
  isRevoked: boolean
}
export type InvitationDetails = {
  email: string
  fullName: string
  phoneNumber: string | null
  organizationName: string
  role: string
  expiresAt: string
}

/**
 * A single entry of the audit trail. The organization is optional because
 * platform-wide events (for example a failed sign-in attempt) do not belong
 * to one organization.
 */
export type AuditEvent = {
  id: string
  category: string
  action: string
  organizationId: string | null
  organizationName: string | null
  actorUserId: string | null
  actorEmail: string
  entityType: string | null
  entityId: string | null
  details: string | null
  occurredAt: string
}
export type AuditEventPage = {
  items: AuditEvent[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
export type PlatformUserOrganization = {
  membershipId: string
  organizationId: string
  organizationName: string
  organizationCode: string
  role: string
  isActive: boolean
}
export type PlatformUser = {
  id: string
  fullName: string
  email: string
  phoneNumber: string | null
  emailConfirmed: boolean
  isActive: boolean
  platformRole: string
  roles: string[]
  createdAt: string
  updatedAt: string
  organizations: PlatformUserOrganization[]
}
export type PlatformStaff = {
  userId: string
  membershipId: string
  organizationId: string
  organizationName: string
  organizationCode: string
  fullName: string
  email: string
  phoneNumber: string | null
  role: string
  isActive: boolean
  emailConfirmed: boolean
  accountIsActive: boolean
  membershipCreatedAt: string
}
export type PlatformOverview = {
  userCount: number
  activeUserCount: number
  staffCount: number
  activeStaffCount: number
  organizationCount: number
  activeOrganizationCount: number
  invitationCount: number
  pendingInvitationCount: number
}

export type CaseHistoryData = { caseId: string; items: HistoryEntry[] }
export type AssignmentHistoryData = { caseId: string; items: AssignmentEntry[] }
export type CaseDocumentsData = { caseId: string; items: CaseDocument[] }
export type MembershipsData = { organizationId: string; items: Membership[] }

export const statusLabels: Record<CaseStatus, string> = {
  Submitted: 'Submitted', UnderReview: 'Under review', Assigned: 'Assigned', InProgress: 'In progress',
  AwaitingInformation: 'Awaiting information', Resolved: 'Resolved', Closed: 'Closed', Rejected: 'Rejected',
}
export const statusOptions: CaseStatus[] = ['Submitted', 'UnderReview', 'Assigned', 'InProgress', 'AwaitingInformation', 'Resolved', 'Closed', 'Rejected']
export const auditCategories = [
  'Authentication',
  'Account',
  'Organization',
  'Membership',
  'Staff',
  'Case',
  'AccessToken',
]
export const organizationId = import.meta.env.VITE_CIVICFLOW_ORGANIZATION_ID as string | undefined
export const demoCases: CaseRecord[] = [
  { id: 'demo-1', referenceNumber: 'CF-8A31C', title: 'Damaged pedestrian crossing', description: 'The crossing signal is not working.', status: 'InProgress', priority: 'High', createdAt: new Date().toISOString() },
  { id: 'demo-2', referenceNumber: 'CF-7F20B', title: 'Streetlight outage on Cedar Avenue', description: 'The streetlight has been out for three nights.', status: 'UnderReview', priority: 'Normal', createdAt: new Date().toISOString() },
  { id: 'demo-3', referenceNumber: 'CF-6D19A', title: 'Accessible bus stop signage', description: 'Request for accessible signage at the bus stop.', status: 'Submitted', priority: 'Normal', createdAt: new Date().toISOString() },
]
