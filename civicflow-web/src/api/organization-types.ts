import { authFetch } from "../auth";
import type { OrganizationType } from "../domain";

async function readErrorMessage(
  response: Response,
  fallback: string,
): Promise<string> {
  try {
    const body = await response.json();
    if (body && typeof body.error === "string" && body.error.length > 0) {
      return body.error;
    }
  } catch {
    // ignore malformed bodies
  }
  return fallback;
}

export class OrganizationTypeApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "OrganizationTypeApiError";
    this.status = status;
  }
}

export async function getOrganizationTypes(): Promise<OrganizationType[]> {
  const response = await authFetch("/api/platform/organization-types");

  if (!response.ok) {
    throw new OrganizationTypeApiError(
      await readErrorMessage(
        response,
        `Failed to fetch organization types: ${response.status}`,
      ),
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
    throw new OrganizationTypeApiError(
      await readErrorMessage(
        response,
        `Failed to create organization type: ${response.status}`,
      ),
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
    throw new OrganizationTypeApiError(
      await readErrorMessage(
        response,
        `Failed to update organization type: ${response.status}`,
      ),
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
    throw new OrganizationTypeApiError(
      await readErrorMessage(
        response,
        `Failed to update organization type status: ${response.status}`,
      ),
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
    throw new OrganizationTypeApiError(
      await readErrorMessage(
        response,
        `Failed to delete organization type: ${response.status}`,
      ),
      response.status,
    );
  }
}
