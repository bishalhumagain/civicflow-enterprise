import { authFetch } from "../auth";
import { readApiErrorDetails } from "../api-error";
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

export type CitizenCaseKind =
  | "PublicUpdate"
  | "InformationRequest"
  | "CitizenResponse";

export type CitizenCaseSort =
  | "updated_desc"
  | "updated_asc"
  | "created_desc"
  | "created_asc";

export interface CitizenCaseSummary {
  id: string;
  referenceNumber: string;
  title: string;
  status: CaseStatus;
  organizationId: string;
  organizationName: string;
  createdAt: string;
  updatedAt: string;
}

export interface CitizenCaseStatusHistory {
  previousStatus: CaseStatus;
  newStatus: CaseStatus;
  changedAt: string;
  explanation?: string | null;
}

export interface CitizenCaseUpdate {
  id: string;
  kind: CitizenCaseKind;
  message: string;
  createdAt: string;
}

export interface CitizenCaseDocument {
  id: string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  sha256Hash: string;
  uploadedAt: string;
}

export interface CitizenCasePage {
  items: CitizenCaseSummary[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface CitizenCaseDetails extends CitizenCaseSummary {
  description: string;
  canRespond: boolean;
  awaitingInformation: boolean;
  statusHistory: CitizenCaseStatusHistory[];
  updates: CitizenCaseUpdate[];
  documents: CitizenCaseDocument[];
}

export interface CitizenCaseQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  status?: CaseStatus;
  sort?: CitizenCaseSort;
}

export class CitizenApiError extends Error {
  readonly status: number;

  constructor(message: string, status: number) {
    super(message);
    this.name = "CitizenApiError";
    this.status = status;
  }
}

export async function getMyCitizenProfile(): Promise<CitizenProfile | null> {
  const response = await authFetch("/api/citizens/me");

  if (response.status === 404) {
    return null;
  }

  if (!response.ok) {
    const { message } = await readApiErrorDetails(response)

    throw new CitizenApiError(
      message ?? `Failed to fetch citizen profile: ${response.status}`,
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
    const { message } = await readApiErrorDetails(response)

    throw new CitizenApiError(
      message ?? `Failed to save citizen profile: ${response.status}`,
      response.status,
    );
  }

  return (await response.json()) as CitizenProfile;
}

export async function getMyCases(
  query: CitizenCaseQuery = {},
): Promise<CitizenCasePage> {
  const parameters = new URLSearchParams({
    page: String(query.page ?? 1),
    pageSize: String(query.pageSize ?? 20),
    sort: query.sort ?? "updated_desc",
  });

  if (query.search?.trim()) parameters.set("search", query.search.trim());
  if (query.status) parameters.set("status", query.status);

  const response = await authFetch(
    `/api/citizens/cases?${parameters.toString()}`,
  );

  if (!response.ok) {
    const { message } = await readApiErrorDetails(response)

    throw new CitizenApiError(
      message ?? `Failed to fetch your requests: ${response.status}`,
      response.status,
    );
  }

  return (await response.json()) as CitizenCasePage;
}

export async function getMyCase(id: string): Promise<CitizenCaseDetails> {
  const response = await authFetch(
    `/api/citizens/cases/${encodeURIComponent(id)}`,
  );

  if (!response.ok) {
    const { message } = await readApiErrorDetails(response)

    throw new CitizenApiError(
      message ?? `Failed to fetch request details: ${response.status}`,
      response.status,
    );
  }

  return (await response.json()) as CitizenCaseDetails;
}

export async function respondToMyCase(
  id: string,
  message: string,
): Promise<CitizenCaseUpdate> {
  const response = await authFetch(
    `/api/citizens/cases/${encodeURIComponent(id)}/responses`,
    {
      method: "POST",
      body: JSON.stringify({ message }),
    },
  );

  if (!response.ok) {
    const { message: errorMessage } = await readApiErrorDetails(response)

    throw new CitizenApiError(
      errorMessage ?? `Your response could not be sent: ${response.status}`,
      response.status,
    );
  }

  return (await response.json()) as CitizenCaseUpdate;
}

export async function downloadMyCaseDocument(
  caseId: string,
  documentId: string,
): Promise<Blob> {
  const response = await authFetch(
    `/api/citizens/cases/${encodeURIComponent(caseId)}/documents/` +
      encodeURIComponent(documentId),
  );

  if (!response.ok) {
    const { message } = await readApiErrorDetails(response)

    throw new CitizenApiError(
      message ?? `The document could not be downloaded: ${response.status}`,
      response.status,
    );
  }

  return await response.blob();
}
