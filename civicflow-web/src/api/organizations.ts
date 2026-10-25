import { authFetch } from "../auth";
import { ApiError } from "../api-client";
import { readApiErrorMessage } from "../api-error";
import type { AccountInvitation, Organization } from "../domain";

export { ApiError as OrganizationApiError };

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
