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
  | 'platform-dashboard'
  | 'organization-dashboard'
  | 'platform-errors'
  | 'platform-jobs'

export type CaseRecord = {
  id: string
  referenceNumber: string
  title: string
  description: string
  status: CaseStatus
  priority: CasePriority
  createdAt: string
  assignedToStaffMemberId?: string | null
  assignedRole?: 'CaseWorker' | 'Viewer' | null
}
export type StaffMember = { id: string; fullName: string; email: string; phoneNumber: string | null; isActive: boolean }
export type HistoryEntry = {
  id: string
  previousStatus: CaseStatus
  newStatus: CaseStatus
  changedAt: string
  changedByUserId?: string | null
  explanation?: string | null
  isVisibleToCitizen?: boolean
}
export type CaseUpdateKind =
  | 'PublicUpdate'
  | 'InformationRequest'
  | 'InternalNote'
  | 'CitizenResponse'
export type CaseUpdate = {
  id: string
  caseId: string
  kind: CaseUpdateKind
  message: string
  isVisibleToCitizen: boolean
  authorUserId: string
  createdAt: string
}
export type CaseDocument = {
  id: string
  caseId: string
  originalFileName: string
  contentType: string
  sizeBytes: number
  sha256Hash: string
  uploadedAt: string
  isVisibleToCitizen: boolean
}
export type AssignmentEntry = { id: string; previousStaffMemberId: string | null; newStaffMemberId: string | null; previousRole: 'CaseWorker' | 'Viewer' | null; newRole: 'CaseWorker' | 'Viewer'; assignedAt: string; assignedByUserId: string | null }
export type CasePage = { items: CaseRecord[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export type Organization = { id: string; name: string; code: string; organizationTypeId: string; organizationTypeName: string | null; isActive: boolean; createdAt: string; updatedAt: string }
export type OrganizationType = { id: string; name: string; code: string; isActive: boolean; createdAt: string; updatedAt: string; organizationCount: number }
export type OrganizationWorkspaceOption = { id: string; name: string; code: string; role: string }
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
  deliveryFailed: boolean
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
  isCitizen: boolean
  isStaff: boolean
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
export type PlatformCitizen = {
  userId: string
  profileId: string
  fullName: string
  email: string
  phoneNumber: string | null
  emailConfirmed: boolean
  isActive: boolean
  nidNumber: string | null
  citizenshipNumber: string | null
  age: number | null
  gender: string | null
  permanentAddress: string | null
  temporaryAddress: string | null
  createdAt: string
  updatedAt: string
}

export type PlatformCitizenUpdate = {
  fullName: string
  phoneNumber?: string | null
  nidNumber?: string | null
  citizenshipNumber?: string | null
  age?: number | null
  gender?: string | null
  permanentAddress?: string | null
  temporaryAddress?: string | null
}

export type PlatformStaffUpdate = {
  fullName: string
  phoneNumber?: string | null
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

export type CaseStatistics = {
  totalCases: number
  submittedCases: number
  underReviewCases: number
  assignedCases: number
  inProgressCases: number
  awaitingInformationCases: number
  resolvedCases: number
  closedCases: number
  rejectedCases: number
  highPriorityCases: number
  normalPriorityCases: number
  lowPriorityCases: number
  casesCreatedThisWeek: number
  casesCreatedThisMonth: number
  casesResolvedThisWeek: number
  casesResolvedThisMonth: number
}

export type ApplicationError = {
  id: string
  message: string
  stackTrace: string
  source: string | null
  methodName: string | null
  requestPath: string | null
  requestMethod: string | null
  userEmail: string | null
  userId: string | null
  organizationId: string | null
  additionalData: string | null
  isHandled: boolean
  occurredAt: string
}

export type CaseHistoryData = { caseId: string; items: HistoryEntry[] }
export type CaseUpdatesData = { caseId: string; items: CaseUpdate[] }
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
export const demoCases: CaseRecord[] = [
  { id: 'demo-1', referenceNumber: 'CF-8A31C', title: 'Damaged pedestrian crossing', description: 'The crossing signal is not working.', status: 'InProgress', priority: 'High', createdAt: new Date().toISOString() },
  { id: 'demo-2', referenceNumber: 'CF-7F20B', title: 'Streetlight outage on Cedar Avenue', description: 'The streetlight has been out for three nights.', status: 'UnderReview', priority: 'Normal', createdAt: new Date().toISOString() },
  { id: 'demo-3', referenceNumber: 'CF-6D19A', title: 'Accessible bus stop signage', description: 'Request for accessible signage at the bus stop.', status: 'Submitted', priority: 'Normal', createdAt: new Date().toISOString() },
]
