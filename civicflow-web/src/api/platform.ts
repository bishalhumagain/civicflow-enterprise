import { authFetch } from "../auth";
import { ApiError } from "../api-client";
import { readApiErrorMessage } from "../api-error";
import type { PlatformOverview, PlatformStaff, PlatformStaffUpdate, PlatformUser } from "../domain";

export { ApiError as PlatformApiError };

export async function getPlatformOverview(): Promise<PlatformOverview> {
  const response = await authFetch("/api/platform/overview");

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as PlatformOverview;
}

export async function getPlatformUsers(): Promise<PlatformUser[]> {
  const response = await authFetch("/api/platform/users");

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as PlatformUser[];
}

export async function getPlatformStaff(): Promise<PlatformStaff[]> {
  const response = await authFetch("/api/platform/staff");

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }

  return (await response.json()) as PlatformStaff[];
}

export async function updatePlatformStaff(
  userId: string,
  request: PlatformStaffUpdate,
): Promise<PlatformUser> {
  const response = await authFetch(
    `/api/platform/staff/${encodeURIComponent(userId)}`,
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

  return (await response.json()) as PlatformUser;
}

export async function activatePlatformUser(userId: string): Promise<void> {
  const response = await authFetch(`/api/platform/users/${encodeURIComponent(userId)}/activate`, {
    method: "PATCH",
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }
}

export async function deactivatePlatformUser(userId: string): Promise<void> {
  const response = await authFetch(`/api/platform/users/${encodeURIComponent(userId)}/deactivate`, {
    method: "PATCH",
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }
}

export async function changePlatformRole(userId: string, platformRole: string): Promise<void> {
  const response = await authFetch(`/api/platform/users/${encodeURIComponent(userId)}/platform-role`, {
    method: "PATCH",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ platformRole }),
  });

  if (!response.ok) {
    throw new ApiError(
      await readApiErrorMessage(response),
      response.status,
    );
  }
}
