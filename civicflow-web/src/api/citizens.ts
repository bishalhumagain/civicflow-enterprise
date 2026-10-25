import { authFetch } from "../auth";
import { ApiError } from "../api-client";
import { readApiErrorMessage } from "../api-error";
import type { CaseStatus } from "../domain";

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

export { ApiError as CitizenApiError };

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
    const message = await readApiErrorMessage(response)

    throw new ApiError(
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
    const message = await readApiErrorMessage(response)

    throw new ApiError(
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
    const errorMessage = await readApiErrorMessage(response)

    throw new ApiError(
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
    const message = await readApiErrorMessage(response)

    throw new ApiError(
      message ?? `The document could not be downloaded: ${response.status}`,
      response.status,
    );
  }

  return await response.blob();
}
