import { authFetch } from "../auth";
import type { PlatformOverview, PlatformUser, PlatformStaff } from "../domain";

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

export class PlatformApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "PlatformApiError";
    this.status = status;
  }
}

export async function getPlatformOverview(): Promise<PlatformOverview> {
  const response = await authFetch("/api/platform/overview");

  if (!response.ok) {
    throw new PlatformApiError(
      await readErrorMessage(
        response,
        `Failed to fetch platform overview: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as PlatformOverview;
}

export async function getPlatformUsers(): Promise<PlatformUser[]> {
  const response = await authFetch("/api/platform/users");

  if (!response.ok) {
    throw new PlatformApiError(
      await readErrorMessage(
        response,
        `Failed to fetch platform users: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as PlatformUser[];
}

export async function getPlatformStaff(): Promise<PlatformStaff[]> {
  const response = await authFetch("/api/platform/staff");

  if (!response.ok) {
    throw new PlatformApiError(
      await readErrorMessage(
        response,
        `Failed to fetch platform staff: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as PlatformStaff[];
}

export async function activatePlatformUser(userId: string): Promise<void> {
  const response = await authFetch(`/api/platform/users/${encodeURIComponent(userId)}/activate`, {
    method: "PATCH",
  });

  if (!response.ok) {
    throw new PlatformApiError(
      await readErrorMessage(
        response,
        `Failed to activate user: ${response.status}`,
      ),
      response.status,
    );
  }
}

export async function deactivatePlatformUser(userId: string): Promise<void> {
  const response = await authFetch(`/api/platform/users/${encodeURIComponent(userId)}/deactivate`, {
    method: "PATCH",
  });

  if (!response.ok) {
    throw new PlatformApiError(
      await readErrorMessage(
        response,
        `Failed to deactivate user: ${response.status}`,
      ),
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
    throw new PlatformApiError(
      await readErrorMessage(
        response,
        `Failed to change platform role: ${response.status}`,
      ),
      response.status,
    );
  }
}
