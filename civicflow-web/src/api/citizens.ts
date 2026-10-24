import { authFetch } from "../auth";
import type { CaseStatus } from "../domain";

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

export interface CitizenCaseSummary {
  id: string;
  referenceNumber: string;
  title: string;
  status: CaseStatus;
  createdAt: string;
  updatedAt: string;
}

export interface CitizenCaseStatusHistory {
  previousStatus: CaseStatus;
  newStatus: CaseStatus;
  changedAt: string;
}

export interface CitizenCaseDetails extends CitizenCaseSummary {
  description: string;
  statusHistory: CitizenCaseStatusHistory[];
}

export class CitizenApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "CitizenApiError";
    this.status = status;
  }
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
  const response = await authFetch("/api/citizens/me");

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
  const response = await authFetch("/api/citizens/me", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
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

export async function getMyCases(): Promise<CitizenCaseSummary[]> {
  const response = await authFetch("/api/citizens/cases");

  if (!response.ok) {
    throw new CitizenApiError(
      await readErrorMessage(
        response,
        `Failed to fetch your requests: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as CitizenCaseSummary[];
}

export async function getMyCase(id: string): Promise<CitizenCaseDetails> {
  const response = await authFetch(
    `/api/citizens/cases/${encodeURIComponent(id)}`,
  );

  if (!response.ok) {
    throw new CitizenApiError(
      await readErrorMessage(
        response,
        `Failed to fetch request details: ${response.status}`,
      ),
      response.status,
    );
  }

  return (await response.json()) as CitizenCaseDetails;
}
