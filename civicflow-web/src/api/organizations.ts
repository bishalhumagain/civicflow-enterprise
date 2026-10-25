import { authFetch } from "../auth";
import { ApiError } from "../api-client";
import { readApiErrorMessage } from "../api-error";
import type { AccountInvitation, Membership, Organization } from "../domain";

export { ApiError as OrganizationApiError };

export async function getOrganizations(): Promise<Organization[]> {
  const response = await authFetch("/api/organizations");

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as Organization[];
}

export async function createOrganization(
  name: string,
  code: string,
  organizationTypeId?: string,
): Promise<Organization> {
  const response = await authFetch("/api/organizations", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      name,
      code,
      organizationTypeId: organizationTypeId ?? null,
    }),
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as Organization;
}

export async function updateOrganization(
  id: string,
  name: string,
  code: string,
  organizationTypeId?: string,
): Promise<Organization> {
  const response = await authFetch(`/api/organizations/${encodeURIComponent(id)}`, {
    method: "PUT",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      name,
      code,
      organizationTypeId: organizationTypeId ?? null,
    }),
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as Organization;
}

export async function activateOrganization(id: string): Promise<Organization> {
  const response = await authFetch(`/api/organizations/${encodeURIComponent(id)}/reactivate`, {
    method: "PATCH",
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as Organization;
}

export async function deactivateOrganization(id: string): Promise<Organization> {
  const response = await authFetch(`/api/organizations/${encodeURIComponent(id)}/deactivate`, {
    method: "PATCH",
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as Organization;
}

export async function deleteOrganization(id: string): Promise<void> {
  const response = await authFetch(`/api/organizations/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }
}

export async function updateMembershipRole(
  organizationId: string,
  membershipId: string,
  role: string,
): Promise<Membership> {
  const response = await authFetch(
    `/api/organizations/${encodeURIComponent(organizationId)}/memberships/${encodeURIComponent(membershipId)}/role`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ role }),
    },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as Membership;
}

export async function setMembershipActive(
  organizationId: string,
  membershipId: string,
  isActive: boolean,
): Promise<Membership> {
  const response = await authFetch(
    `/api/organizations/${encodeURIComponent(organizationId)}/memberships/${encodeURIComponent(membershipId)}/${isActive ? "reactivate" : "deactivate"}`,
    {
      method: "PATCH",
    },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as Membership;
}

export async function transferMembership(
  sourceOrganizationId: string,
  membershipId: string,
  destinationOrganizationId: string,
  role: string,
): Promise<Membership> {
  const response = await authFetch(
    `/api/organizations/${encodeURIComponent(sourceOrganizationId)}/memberships/${encodeURIComponent(membershipId)}/transfer`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ destinationOrganizationId, role }),
    },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as Membership;
}

export async function getInvitations(
  organizationId: string,
): Promise<AccountInvitation[]> {
  const response = await authFetch(
    `/api/organizations/${encodeURIComponent(organizationId)}/invitations`,
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as AccountInvitation[];
}

export async function createInvitation(
  organizationId: string,
  request: {
    fullName: string;
    email: string;
    phoneNumber: string | null;
    role: string;
  },
): Promise<AccountInvitation> {
  const response = await authFetch(
    `/api/organizations/${encodeURIComponent(organizationId)}/invitations`,
    {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as AccountInvitation;
}

export async function resendInvitation(
  organizationId: string,
  invitationId: string,
): Promise<AccountInvitation> {
  const response = await authFetch(
    `/api/organizations/${encodeURIComponent(organizationId)}/invitations/${encodeURIComponent(invitationId)}/resend`,
    {
      method: "POST",
    },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as AccountInvitation;
}

export async function revokeInvitation(
  organizationId: string,
  invitationId: string,
): Promise<void> {
  const response = await authFetch(
    `/api/organizations/${encodeURIComponent(organizationId)}/invitations/${encodeURIComponent(invitationId)}`,
    {
      method: "DELETE",
    },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }
}
