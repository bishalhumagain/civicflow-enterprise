import type { AccountInvitation } from '../domain'

export const invitationRoles = ['Viewer', 'CaseWorker', 'OrganizationAdmin']

export function invitationStatus(invitation: AccountInvitation): string {
  if (invitation.isUsed) return 'Accepted'
  if (invitation.isRevoked) return 'Revoked'
  if (invitation.isExpired) return 'Expired'
  if (invitation.deliveryFailed) return 'Delivery failed'
  return 'Pending'
}

export function canResend(invitation: AccountInvitation): boolean {
  return !invitation.isUsed && !invitation.isRevoked
}

export function canRevoke(invitation: AccountInvitation): boolean {
  return !invitation.isUsed && !invitation.isRevoked
}
