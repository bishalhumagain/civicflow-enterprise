import { authFetch } from "../auth";
import { ApiError } from "../api-client";
import { readApiErrorMessage } from "../api-error";
import type { OrganizationType } from "../domain";

export { ApiError as OrganizationTypeApiError };

export async function getOrganizationTypes(): Promise<OrganizationType[]> {
  const response = await authFetch("/api/platform/organization-types");

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as OrganizationType[];
}

export async function createOrganizationType(
  name: string,
  code: string,
): Promise<OrganizationType> {
  const response = await authFetch("/api/platform/organization-types", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ name, code }),
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as OrganizationType;
}

export async function updateOrganizationType(
  id: string,
  name: string,
  code: string,
): Promise<OrganizationType> {
  const response = await authFetch(
    `/api/platform/organization-types/${encodeURIComponent(id)}`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ name, code }),
    },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as OrganizationType;
}

export async function setOrganizationTypeActive(
  id: string,
  isActive: boolean,
): Promise<OrganizationType> {
  const action = isActive ? "reactivate" : "deactivate";
  const response = await authFetch(
    `/api/platform/organization-types/${encodeURIComponent(id)}/${action}`,
    { method: "PATCH" },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as OrganizationType;
}

export async function deleteOrganizationType(id: string): Promise<void> {
  const response = await authFetch(
    `/api/platform/organization-types/${encodeURIComponent(id)}`,
    { method: "DELETE" },
  );

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }
}
