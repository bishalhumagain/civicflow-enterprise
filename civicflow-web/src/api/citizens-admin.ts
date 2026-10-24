import { authFetch } from "../auth";
import type { PlatformCitizen, PlatformCitizenUpdate } from "../domain";

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

export class CitizenAdminApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "CitizenAdminApiError";
    this.status = status;
  }
}

export async function getPlatformCitizens(): Promise<PlatformCitizen[]> {
  const response = await authFetch("/api/platform/citizens");

  if (!response.ok) {
    throw new CitizenAdminApiError(
      await readErrorMessage(
        response,
        `Failed to fetch citizen profiles: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as PlatformCitizen[];
}

export async function updateCitizenProfile(
  userId: string,
  request: PlatformCitizenUpdate,
): Promise<PlatformCitizen> {
  const response = await authFetch(
    `/api/platform/citizens/${encodeURIComponent(userId)}`,
    {
      method: "PUT",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    },
  );

  if (!response.ok) {
    throw new CitizenAdminApiError(
      await readErrorMessage(
        response,
        `Failed to update citizen profile: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as PlatformCitizen;
}

export async function toggleCitizenAccountActive(
  userId: string,
  isActive: boolean,
): Promise<void> {
  const response = await authFetch(
    `/api/platform/citizens/${encodeURIComponent(userId)}/${isActive ? "activate" : "deactivate"}`,
    {
      method: "PATCH",
    },
  );

  if (!response.ok) {
    throw new CitizenAdminApiError(
      await readErrorMessage(
        response,
        `Failed to toggle citizen account status: ${response.status}`,
      ),
      response.status,
    );
  }
}
