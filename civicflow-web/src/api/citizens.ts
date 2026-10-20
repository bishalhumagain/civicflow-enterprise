import { getAccessToken } from "../auth";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000";

export interface CitizenProfile {
  id: string;
  applicationUserId: string;
  fullName: string;
  email: string;
  phoneNumber?: string | null;
  nidNumber?: string | null;
  citizenshipNumber?: string | null;
  age?: number | null;
  gender?: string | null;
  permanentAddress?: string | null;
  temporaryAddress?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCitizenProfileRequest {
  fullName: string;
  email: string;
  phoneNumber?: string;
  nidNumber?: string;
  citizenshipNumber?: string;
  age?: number | null;
  gender?: string;
  permanentAddress?: string;
  temporaryAddress?: string;
}

export class CitizenApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "CitizenApiError";
    this.status = status;
  }
}

async function getAuthHeader(): Promise<HeadersInit> {
  try {
    const token = await getAccessToken();
    if (token) {
      return { Authorization: `Bearer ${token}` };
    }
  } catch {
    // ignore
  }
  return {};
}

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

export async function getMyCitizenProfile(): Promise<CitizenProfile | null> {
  const headers = await getAuthHeader();
  const response = await fetch(`${API_BASE_URL}/api/citizens/me`, {
    headers,
    credentials: "include",
  });

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    throw new CitizenApiError(
      await readErrorMessage(
        response,
        `Failed to fetch citizen profile: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as CitizenProfile;
}

export async function saveMyCitizenProfile(
  request: CreateCitizenProfileRequest,
): Promise<CitizenProfile> {
  const headers = await getAuthHeader();
  const response = await fetch(`${API_BASE_URL}/api/citizens/me`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      ...headers,
    },
    credentials: "include",
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new CitizenApiError(
      await readErrorMessage(
        response,
        `Failed to save citizen profile: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as CitizenProfile;
}
