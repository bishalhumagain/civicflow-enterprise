import { requestJson } from '../api-client'

/**
 * The two account types that can maintain a self-service profile. Platform
 * administrators are intentionally excluded: they administer the platform
 * rather than the citizen and organization workspaces.
 */
export type ProfileAccountType = 'Citizen' | 'Staff'

export type Profile = {
  userId: string
  fullName: string
  email: string
  emailConfirmed: boolean
  phoneNumber: string | null
  accountType: ProfileAccountType
  roles: string[]
  organizationId: string | null
  organizationName: string | null
  organizationRole: string | null
  nidNumber: string | null
  citizenshipNumber: string | null
  age: number | null
  gender: string | null
  permanentAddress: string | null
  temporaryAddress: string | null
  updatedAt: string
}

export type UpdateProfileRequest = {
  fullName: string
  phoneNumber?: string | null
  nidNumber?: string | null
  citizenshipNumber?: string | null
  age?: number | null
  gender?: string | null
  permanentAddress?: string | null
  temporaryAddress?: string | null
}

/**
 * Loads the signed-in account's profile. The API resolves whether the account
 * is a citizen or an organization staff member and returns the matching
 * details.
 */
export async function getMyProfile(): Promise<Profile> {
  return requestJson<Profile>('/api/profile/me')
}

/**
 * Saves the signed-in account's profile. The email address is not part of the
 * request because changing it requires a fresh verification.
 */
export async function updateMyProfile(
  request: UpdateProfileRequest,
): Promise<Profile> {
  return requestJson<Profile>('/api/profile/me', {
    method: 'PUT',
    body: JSON.stringify(request),
  })
}
