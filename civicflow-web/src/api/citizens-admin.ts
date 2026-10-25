import { authFetch } from "../auth";
import { ApiError } from "../api-client";
import { readApiErrorMessage } from "../api-error";
import type { PlatformCitizen, PlatformCitizenUpdate } from "../domain";

export { ApiError as CitizenAdminApiError };

export async function getPlatformCitizens(): Promise<PlatformCitizen[]> {
  const response = await authFetch("/api/platform/citizens");

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
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
    throw new ApiError(
      await readApiErrorMessage(response),
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
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }
}
