export type CaseStatus = 'Submitted' | 'UnderReview' | 'Assigned' | 'InProgress' | 'AwaitingInformation' | 'Resolved' | 'Closed' | 'Rejected'
export type CasePriority = 'High' | 'Normal' | 'Low'
export type WorkspaceView = 'cases' | 'staff' | 'organizations' | 'citizen'

export type CaseRecord = {
  id: string
  referenceNumber: string
  title: string
  description: string
  status: CaseStatus
  priority: CasePriority
  createdAt: string
}
export type StaffMember = { id: string; fullName: string; email: string; isActive: boolean }
export type HistoryEntry = { id: string; previousStatus: CaseStatus; newStatus: CaseStatus; changedAt: string }
export type CaseDocument = { id: string; originalFileName: string; contentType: string; sizeBytes: number; uploadedAt: string }
export type AssignmentEntry = { id: string; previousStaffMemberId: string | null; newStaffMemberId: string | null; assignedAt: string; assignedByUserId: string | null }
export type CasePage = { items: CaseRecord[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export type Organization = { id: string; name: string; code: string; isActive: boolean; createdAt: string; updatedAt: string }
export type Membership = { id: string; userId: string; organizationId: string; role: string; isActive: boolean; createdAt: string; updatedAt: string }

export const statusLabels: Record<CaseStatus, string> = {
  Submitted: 'Submitted', UnderReview: 'Under review', Assigned: 'Assigned', InProgress: 'In progress',
  AwaitingInformation: 'Awaiting information', Resolved: 'Resolved', Closed: 'Closed', Rejected: 'Rejected',
}
export const statusOptions: CaseStatus[] = ['Submitted', 'UnderReview', 'Assigned', 'InProgress', 'AwaitingInformation', 'Resolved', 'Closed', 'Rejected']
export const organizationId = import.meta.env.VITE_CIVICFLOW_ORGANIZATION_ID as string | undefined
export const demoCases: CaseRecord[] = [
  { id: 'demo-1', referenceNumber: 'CF-8A31C', title: 'Damaged pedestrian crossing', description: 'The crossing signal is not working.', status: 'InProgress', priority: 'High', createdAt: new Date().toISOString() },
  { id: 'demo-2', referenceNumber: 'CF-7F20B', title: 'Streetlight outage on Cedar Avenue', description: 'The streetlight has been out for three nights.', status: 'UnderReview', priority: 'Normal', createdAt: new Date().toISOString() },
  { id: 'demo-3', referenceNumber: 'CF-6D19A', title: 'Accessible bus stop signage', description: 'Request for accessible signage at the bus stop.', status: 'Submitted', priority: 'Normal', createdAt: new Date().toISOString() },
]
