# API Contract and Authorization Matrix

This inventory reflects the controller routes currently mapped by
`app.MapControllers()`; the API has no separately mapped minimal endpoints.
The request/response names below refer to the DTOs in
`PublicServiceCaseManagement.Api/Features`. Business endpoints that consult
`BusinessEndpoints:Enabled` return `404` when that gate is disabled; citizen
self-service routes are exempt and remain available.

## Authentication and Tenant Resolution

- Browser clients use the ASP.NET Core Identity HttpOnly cookie. Unsafe
  cookie-authenticated API requests require the antiforgery cookie and
  `X-CSRF-TOKEN` header when `Security:RequireAntiforgery` is enabled.
  Non-browser clients may use a personal access token in the
  `Authorization: Bearer` header.
- In Development, Swagger UI's request interceptor obtains a fresh token from
  `GET /api/auth/csrf` and adds the header to unsafe same-origin `/api/*`
  requests. This leaves cookie/Identity authentication and server-side
  antiforgery validation enabled; requests authorized with a bearer token
  continue through the existing non-ambient bearer path.
- API clients may exchange a valid personal access token at
  `POST /api/auth/token` for a short-lived signed JWT (default 15 minutes).
  The JWT handler validates issuer, audience, signature, algorithm, and
  lifetime, then re-reads the active account, current Identity roles, and
  source PAT status. The JWT carries only the source PAT's identifier, not
  its secret. Revoking the source PAT or logging out with its JWT invalidates
  all JWTs issued from that PAT. Organization membership is resolved from the
  database per request.
- The `CivicFlowSmart` policy scheme routes `Bearer civicflow_pat_*` to the PAT
  handler, any other `Bearer` value to the JWT handler, and requests without an
  `Authorization` header to the Identity cookie. A bearer header is not
  ambient, so bearer requests are exempt from CSRF; invalid bearer values fail
  closed with `401`. Cookie requests keep the existing CSRF requirement.
- Policies derive platform roles from the validated session. The active
  application account and organization membership are re-read from the
  database; a client role, organization selector, or hidden UI control is not
  authoritative.
- Organization endpoints use `X-Organization-Id` as a selector only. It must
  identify an active organization with an active membership for the signed-in
  user. There is no implicit “first membership” fallback. Missing, inactive,
  or non-member selection does not resolve an organization context.
- `citizen`, `staff`, and `superadmin` are platform Identity roles.
  `Viewer`, `CaseWorker`, and `OrganizationAdmin` are organization membership
  roles. A platform `superadmin` receives cross-organization authority only
  on endpoints that explicitly check that database-backed platform role.
  Otherwise, organization operations require a resolved active membership.
- Organization role is evaluated for the selected membership on each request.
  Inactive accounts, organizations, or memberships fail resolution. If an
  account has several memberships, the selected active membership determines
  the role for that request; roles are not combined across organizations.
- Every unsafe cookie-authenticated `/api/*` request is subject to the
  configured CSRF middleware. Bearer-authenticated requests (PAT or JWT) are
  exempt because browsers cannot attach an `Authorization` header cross-origin
  without a CORS preflight. Authentication and authorization policies are
  applied after authentication and before endpoint execution.

## Role and Resource Matrix

| Resource / operation | Citizen | Viewer | CaseWorker | OrganizationAdmin | SuperAdmin |
|---|---|---|---|---|---|
| Own account/profile and own PATs | Read/update own profile; own tokens | Own profile/tokens | Own profile/tokens | Own profile/tokens | Own tokens; platform profile is not provided |
| Citizen case intake | Submit to an active organization; list/read own submitted cases | No | No | No | No |
| Organization directory | Active public-intake organizations only | Selected organization's staff list; no phone numbers | Selected organization's staff list; no phone numbers | Own organization's staff and phone numbers; Organizations view for administered organizations | Platform directory and organization administration |
| Case reads | Own submitted cases only (summary and status history) | Only assigned Viewer cases | Only assigned staff cases | All cases in selected organization | Platform-wide read-only case inspection (`/api/platform/cases`); no global write access |
| Case changes | No | Read-only | Assigned CaseWorker case details/status/priority/documents only | Organization cases; assign/reassign | No implicit case-wide bypass |
| Notifications | Own notifications only | Own notifications only | Own notifications only | Own notifications only | Own notifications only |
| Background jobs | No | No | No | No | Global job summaries/details and state-eligible retry/cancel |
| Memberships/invitations | No | No | No | Selected organization they administer; cannot grant `OrganizationAdmin`; invitations create `CaseWorker` memberships | Platform-authorized organization membership and invitation operations, including permitted admin provisioning |
| Audit | No | Own actor events in selected organization | Own actor events in selected organization | All events in selected organization | Platform-wide audit and explicitly authorized organization audit |
| Platform directory, organization CRUD, dashboard, errors | No | No | No | Organizations view lists only administered organizations; no organization CRUD | Explicit platform management and cross-organization reports |

Additional boundaries:

- `StaffClient` means authenticated platform `staff` or `superadmin`; it does
  not establish an organization role by itself.
- Platform-wide case inspection (`GET /api/platform/cases*`) is read-only and
  requires the platform-level `SuperAdmin` role. Queries run inside a
  transaction-local `civicflow.platform_read` context whose PostgreSQL
  policies are `SELECT`-only; the platform flag is never a write path, and
  status, assignment, communication, and document changes remain scoped to
  the owning organization.
- CaseWorker write permission is limited to the case assigned to their active
  staff record with the `CaseWorker` case assignment role. Viewer assignments
  are read-only. Assignment itself is OrganizationAdmin-only and only accepts
  active staff in the same organization.
- Organization administrators can read and manage cases in their active
  organization. Case deletion is not available to any role, including Platform
  SuperAdmins; cases are retained permanently. Neither role can use a
  frontend-supplied creator identifier; case ownership is taken from the
  authenticated account. Organization administrators cannot grant themselves or
  another member the `OrganizationAdmin` membership role.
- Citizens may submit to any organization returned by the active public intake
  directory. The authenticated user ID is assigned server-side; the request
  cannot set a creator ID.
- Case updates are available through `POST/GET /api/cases/{id}/updates`, and
  citizen responses through `POST /api/citizens/cases/{id}/responses`. Both are
  append-only: there is no update-edit/delete API, bulk operation, export API,
  or general file-storage API. Case history, assignment history, update-list,
  and audit routes are read-only.

## Endpoint Inventory

`200` means a successful read or update unless otherwise noted. Framework
model-binding and DTO validation failures return `400`; authorization failures
return `401` or `403`; scoped resource misses generally return `404`. Conflict,
rate-limit, and workflow-specific statuses are noted where applicable.

### Health

| Method and path | Access / parameters | Response and effects |
|---|---|---|
| `GET /api/health` | Anonymous; no parameters | `200` `{status, service}` liveness response. |
| `GET /api/health/ready` | Anonymous; no parameters | `200` when PostgreSQL is reachable; otherwise `503` with a generic unavailable response. |

### Public configuration

| Method and path | Access / parameters | Response and effects |
|---|---|---|
| `GET /api/config/public` | Anonymous; no parameters | `200` `{applicationName}`. Safe branding only, sourced from `Application:Name`; no connection strings, secrets, or operational settings are exposed. |

### Authentication, invitations redemption, and personal tokens

| Method and path | Access / request | Success, failures, and effects |
|---|---|---|
| `GET /api/auth/csrf` | Anonymous; no parameters | `200` `{token}`; issues antiforgery cookie/request token. |
| `POST /api/auth/register` | Anonymous; `RegisterRequest` (name, email, phone, identity numbers, password) | `200 AuthOperationResult`; validation/registration failure `400`; creates citizen account and initiates verification email. |
| `POST /api/auth/login` | Anonymous; `LoginRequest` | `200` and citizen session; `401` invalid credentials, `403` wrong portal/unconfirmed email, `423` lockout. |
| `POST /api/auth/login/organization` | Anonymous; `LoginRequest` | `200` and staff session; same failure classes as portal login above. |
| `POST /api/auth/login/platform-admin` | Anonymous; `LoginRequest` | `200` and SuperAdmin session; same failure classes as portal login above. |
| `POST /api/auth/invitations/lookup` | Anonymous; `InvitationLookupRequest` token | `200` safe invitation details; `410` invalid, expired, used, or revoked invitation. |
| `POST /api/auth/invitations/accept` | Anonymous; `AcceptInvitationRequest` token and chosen password | `200 AcceptInvitationResponse`; `400` invalid input, `403` inactive account, `410` unavailable invitation. Creates/links account and membership atomically; token is single-use. |
| `POST /api/auth/logout` | Authenticated; no body | `204`; clears the cookie session or, for PAT/JWT callers, revokes the source PAT so its JWTs cannot be replayed. |
| `POST /api/auth/verify-email` | Anonymous; `VerifyEmailRequest` | `200 AuthOperationResult`; invalid/expired token `400`. |
| `POST /api/auth/resend-verification` | Anonymous; `ResendVerificationRequest` | `200` enumeration-safe result; may send verification email. |
| `POST /api/auth/forgot-password` | Anonymous; `ForgotPasswordRequest` | `200` enumeration-safe result; may send password-reset email. |
| `POST /api/auth/reset-password` | Anonymous; `ResetPasswordRequest` | `200 AuthOperationResult`; invalid token/password `400`; resets password. |
| `POST /api/auth/change-password` | Authenticated; `ChangePasswordRequest` | `200 AuthOperationResult`; failed current/new password `400`; changes own password. |
| `GET /api/auth/me` | Authenticated; no parameters | `200 CurrentUserResponse`; `401` for inactive/unresolvable account. |
| `POST /api/auth/token` | Personal access token only; no body | `200 AccessTokenResponse` (`access_token`, `token_type`, `expires_in`, `expires_at`) containing a short-lived signed JWT; missing/invalid PAT `401`. Cookies cannot mint tokens. |
| `GET /api/auth/tokens` | Authenticated; no parameters | `200 PersonalAccessTokenResponse[]`; lists only the caller's tokens. |
| `POST /api/auth/tokens` | Authenticated; `CreatePersonalAccessTokenRequest` | `201 CreatePersonalAccessTokenResponse`; plaintext is returned once; invalid or over-limit request `400`. |
| `DELETE /api/auth/tokens/{tokenId:guid}` | Authenticated; token ID | `204` revoked; `404` if not owned or not found. |

### Citizen and self-service profile

| Method and path | Access / request | Success, failures, and effects |
|---|---|---|
| `GET /api/citizens/me` | Citizen; no parameters | `200 CitizenProfileResponse`; `404` if profile does not exist. |
| `POST /api/citizens/me` | Citizen; `CreateCitizenProfileRequest` | `201` on create, `200` if the profile already exists and is updated; `400` invalid data, `409` duplicate identity data. User ID comes from session. |
| `PUT /api/citizens/me` | Citizen; `UpdateCitizenProfileRequest` | `200` updated profile; `400` invalid data, `404` if absent, `409` duplicate identity data. |
| `GET /api/citizens/organizations` | Citizen; no parameters | `200 CitizenOrganizationOption[]`; active organizations eligible for public intake only. |
| `GET /api/citizens/cases?page={n}&pageSize={n}&search={text}&status={status}&sort={sort}` | Citizen; optional `page` (default 1, range 1–1,000,000), `pageSize` (default 20, range 1–100), `search` (reference/title), `status` (`CaseStatus`), `sort` (`updated_desc` default, `updated_asc`, `created_desc`, `created_asc`) | `200 CitizenCasePage` with caller-owned items, total count, and total pages; `400` invalid paging/sort. Rows never include another citizen's case. |
| `GET /api/citizens/cases/{id:guid}` | Citizen; case ID | `200 CitizenCaseDetails` with description, organization, current status, `canRespond`, `awaitingInformation`, citizen-visible status history, updates, and documents; `404` when the case is absent or was not submitted by the authenticated citizen. Internal notes and non-visible documents/explanations are filtered in the database projection. |
| `POST /api/citizens/cases` | Citizen; multipart `CitizenCreateCaseRequest` (`organizationId`, title, description, optional `additionalDocument`); max request body 11 MiB and document limit 10 MiB | `201 CitizenCaseSubmissionResponse`; `400` inactive/unknown organization or invalid case/file; creates a citizen-submitted case and a citizen-visible attachment, then routes the case to an organization administrator for triage. |
| `POST /api/citizens/cases/{id:guid}/responses` | Citizen owner; `CitizenCaseResponseRequest` (message 1–4,000 characters) | `201 CitizenCaseUpdate`; `400` closed/rejected case or invalid message, `404` case absent or not owned. Adds a `CitizenResponse` update; when the case is `AwaitingInformation`, moves it to `UnderReview` with a citizen-visible history entry and notifies the assigned staff account. |
| `GET /api/citizens/cases/{id:guid}/documents` | Citizen owner | `200 CitizenCaseDocument[]`; citizen-visible documents only; `404` case absent or not owned. |
| `GET /api/citizens/cases/{id:guid}/documents/{documentId:guid}` | Citizen owner; document ID | `200` attachment stream with range support; `404` absent, not owned, not citizen-visible, or file missing. |
| `GET /api/profile/me` | Citizen or staff; no parameters | `200 ProfileResponse`; `401` unresolved identity, `404` if profile is absent. |
| `PUT /api/profile/me` | Citizen or staff; `UpdateProfileRequest` | `200 ProfileResponse`; updates only the authenticated user's permitted self-service fields. Account type and organization membership are server-derived. |

### Organizations, memberships, invitations, and staff

| Method and path | Access / parameters | Success, failures, and effects |
|---|---|---|
| `POST /api/organizations` | SuperAdmin; `CreateOrganizationRequest` (optional `OrganizationTypeId`) | `201 OrganizationResponse`; `400` invalid values, `409` duplicate conflict; creates organization. When `OrganizationTypeId` is `null`/`Guid.Empty`, the seeded `Uncategorized` type is applied; unknown or inactive types return `400`. |
| `GET /api/organizations?page={n}&pageSize={n}&search={text}&organizationTypeId={guid}&isActive={bool}` | SuperAdmin or authenticated staff; page defaults `1`, pageSize defaults `25` (maximum `100`) | `200 PagedResponse<OrganizationResponse>`; SuperAdmin sees all organizations, other staff see only organizations they administer. Filters and stable name sorting are applied before `Skip/Take`. |
| `GET /api/organizations/me` | Authenticated staff with active account; none | `200 OrganizationWorkspaceOption[]`; only the caller's active memberships. |
| `GET /api/organizations/{id:guid}` | SuperAdmin | `200 OrganizationResponse`; `404` unknown ID. |
| `PUT /api/organizations/{id:guid}` | SuperAdmin; `UpdateOrganizationRequest` (optional `OrganizationTypeId`) | `200 OrganizationResponse`; `400` invalid values or unknown/inactive type, `404` missing, `409` conflict; updates organization details. |
| `PATCH /api/organizations/{id:guid}/deactivate` | SuperAdmin | `200 OrganizationResponse`; `404` missing; deactivates organization. |
| `PATCH /api/organizations/{id:guid}/reactivate` | SuperAdmin | `200 OrganizationResponse`; `404` missing; reactivates organization. |
| `DELETE /api/organizations/{id:guid}` | SuperAdmin | `204` deleted; `404` missing, `409` while cases remain; removes organization memberships, staff rows, and pending invitations. |
| `GET /api/platform/organization-types` | SuperAdmin | `200 OrganizationTypeResponse[]`; shared catalogue with `id`, `name`, `code`, `isActive`, `createdAt`, `updatedAt`, and `organizationCount`. `404` when the business endpoints are disabled; `403` for non-super-admins. |
| `GET /api/platform/organization-types/{id:guid}` | SuperAdmin | `200 OrganizationTypeResponse`; `404` missing. |
| `POST /api/platform/organization-types` | SuperAdmin; `CreateOrganizationTypeRequest` (name 1–200, code 1–50) | `201 OrganizationTypeResponse`; `400` invalid values, `409` duplicate code; audits `organization_type.created`. |
| `PUT /api/platform/organization-types/{id:guid}` | SuperAdmin; `UpdateOrganizationTypeRequest` | `200 OrganizationTypeResponse`; `400` invalid values, `404` missing, `409` duplicate code; audits `organization_type.updated`. |
| `PATCH /api/platform/organization-types/{id:guid}/deactivate` | SuperAdmin | `200 OrganizationTypeResponse`; `404` missing; `409` when the target is the protected `Uncategorized` seed; audits `organization_type.deactivated`. |
| `PATCH /api/platform/organization-types/{id:guid}/reactivate` | SuperAdmin | `200 OrganizationTypeResponse`; `404` missing; audits `organization_type.activated`. |
| `DELETE /api/platform/organization-types/{id:guid}` | SuperAdmin | `204` deleted; `404` missing; `409` when any organization still references the type or when the target is `Uncategorized`; audits `organization_type.deleted`. |
| `POST /api/organizations/{organizationId:guid}/memberships` | SuperAdmin, or OrganizationAdmin for their selected organization; `CreateOrganizationMembershipRequest` | `201 OrganizationMembershipResponse`; `400` invalid/prohibited role, `404` missing user/org, `409` existing/conflicting membership. Only SuperAdmin may grant OrganizationAdmin. |
| `GET /api/organizations/{organizationId:guid}/memberships?isActive={bool}&page={n}&pageSize={n}&search={text}` | Same membership administrators; page defaults `1`, pageSize defaults `25` (maximum `100`) | `200 PagedResponse<OrganizationMembershipResponse>`; filtered and sorted before database pagination and restricted to the specified authorized organization. |
| `GET /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}` | Same membership administrators | `200 OrganizationMembershipResponse`; `404` outside/missing membership. |
| `PUT /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}/role` | Same membership administrators; `ChangeOrganizationMembershipRoleRequest` | `200 OrganizationMembershipResponse`; `400` invalid/prohibited role, `404` missing membership. |
| `PATCH /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}/deactivate` | Same membership administrators | `200 OrganizationMembershipResponse`; `404` missing membership. |
| `PATCH /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}/reactivate` | Same membership administrators | `200 OrganizationMembershipResponse`; `404` missing membership. |
| `POST /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}/transfer` | SuperAdmin; `TransferOrganizationMembershipRequest` | `200 OrganizationMembershipResponse` for the new destination membership; deactivates the source membership and audits `membership.transferred` against both source and destination organizations. `400` same organization or invalid role; `404` missing membership/organization; `409` inactive destination or duplicate destination membership. |
| `POST /api/organizations/{organizationId:guid}/invitations` | SuperAdmin, or OrganizationAdmin for their selected organization; `CreateInvitationRequest` | `201 InvitationResponse`; `400` invalid/prohibited role, `404` missing organization, `409` duplicate/conflict; generates single-use expiring token and sends invite email. OrganizationAdmins may invite CaseWorkers only. |
| `GET /api/organizations/{organizationId:guid}/invitations?page={n}&pageSize={n}&search={text}` | Same invitation administrators; page defaults `1`, pageSize defaults `25` (maximum `100`) | `200 PagedResponse<InvitationResponse>`; organization-scoped, filtered and sorted list; token/hash is never returned. |
| `POST /api/organizations/{organizationId:guid}/invitations/{invitationId:guid}/resend` | Same invitation administrators | `200 InvitationResponse`; `404` missing, `409` used/revoked/unavailable; creates and sends a replacement single-use link. |
| `DELETE /api/organizations/{organizationId:guid}/invitations/{invitationId:guid}` | Same invitation administrators | `204` revoked; `404` missing, `409` already used/unavailable. |
| `GET /api/staff?isActive={bool}&page={n}&pageSize={n}&search={text}` | Active staff membership; page defaults `1`, pageSize defaults `25` (maximum `100`) | `200 PagedResponse<StaffSummary>`; selected-organization staff only. Phone number is returned to OrganizationAdmin and omitted for other organization roles. |
| `POST /api/staff` | OrganizationAdmin in selected organization; `CreateStaffMemberRequest` | `201 StaffSummary`; `400` invalid values, `409` duplicate email; creates an organization-scoped staff directory record and audit event, but does not create an account or membership. Use the invitation route to enable sign-in. |
| `GET /api/staff/platform?organizationId={guid}&isActive={bool}&page={n}&pageSize={n}&search={text}` | SuperAdmin; filters optional, page defaults `1`, pageSize defaults `25` (maximum `100`) | `200 PagedResponse<StaffSummary>`; platform-wide staff directory. |
| `PATCH /api/staff/platform/{id:guid}/deactivate?organizationId={guid}` | SuperAdmin; required organization selector | `200 StaffSummary`; `404` missing staff row. |
| `PATCH /api/staff/platform/{id:guid}/reactivate?organizationId={guid}` | SuperAdmin; required organization selector | `200 StaffSummary`; `404` missing staff row. |
| `PATCH /api/staff/{id:guid}/deactivate` | OrganizationAdmin in selected organization | `200 StaffSummary`; `404` staff row outside/missing. |
| `PATCH /api/staff/{id:guid}/reactivate` | OrganizationAdmin in selected organization | `200 StaffSummary`; `404` staff row outside/missing. |
| `PATCH /api/staff/{id:guid}/phone` | OrganizationAdmin; `UpdateStaffPhoneRequest` | `200 StaffSummary`; `400` invalid phone, `404` staff row outside/missing. |

Staff directory records can be added without creating an account or
membership. Invitation acceptance can link/reuse a matching staff record; the
invitation endpoint is required to provision an account and grant an
organization role.

### Cases and documents

All `/api/cases` routes require the `staff` or `superadmin` platform policy,
BusinessEndpoints enabled, and a valid active organization context. Organization
Administrators see their selected organization's cases. Viewer and CaseWorker
results and identifier lookups are restricted to the authenticated user's
active staff record and assigned cases.

| Method and path | Additional access / request | Success, failures, and effects |
|---|---|---|
| `POST /api/cases` | CaseWorker or OrganizationAdmin; `CreateCaseRequest` (title 3–200, description 10–5,000) | `201 CreateCaseResponse`; `400` invalid or whitespace-only case text; normalizes the title by collapsing whitespace and trims the description while preserving line breaks; creates a case in the resolved organization and records audit event. |
| `GET /api/cases?page={n}&pageSize={n}` | Active organization member; defaults page `1`, pageSize `20`, page range `1–1,000,000`, pageSize range `1–100` | `200 PagedCasesResponse`; admin gets org cases, staff get their assignments only. |
| `GET /api/cases/{id:guid}` | OrganizationAdmin or assigned staff | `200 CreateCaseResponse`; `404` missing, cross-org, or unassigned case. |
| `PUT /api/cases/{id:guid}` | OrganizationAdmin or assigned CaseWorker; `UpdateCaseRequest` | `200 CreateCaseResponse`; `400` invalid/business-closed case, `403` Viewer/unassigned write, `404` inaccessible case; updates permitted case details with the same title/description bounds and normalization as creation. |
| `PATCH /api/cases/{id:guid}/status` | OrganizationAdmin or assigned CaseWorker; `ChangeCaseStatusRequest` (`newStatus`, optional `explanation`, `explanationVisibleToCitizen`) | `200 ChangeCaseStatusResponse`; `400` invalid transition/explanation, `403` Viewer/unassigned write, `404` inaccessible case; persists status history and audit. The transition is always citizen-visible; the explanation is projected to the citizen timeline only when the visibility flag is true. |
| `PATCH /api/cases/{id:guid}/priority` | OrganizationAdmin or assigned CaseWorker; `ChangeCasePriorityRequest` (`newPriority`, a `CasePriority` value) | `200` `{id, referenceNumber, priority, updatedAt}`; `400` invalid priority or closed/rejected case, `403` Viewer/unassigned write, `404` inaccessible case; persists the new priority and records `case.priority_changed` in the audit trail. |
| `GET /api/cases/{id:guid}/history` | OrganizationAdmin or assigned staff | `200` status-history entries including explanation and `isVisibleToCitizen`; `404` inaccessible case. |
| `POST /api/cases/{id:guid}/updates` | OrganizationAdmin or assigned CaseWorker; `AddCaseUpdateRequest` (`kind`: `PublicUpdate`, `InformationRequest`, or `InternalNote`; message 1–4,000) | `201 CaseUpdateResponse`; `400` invalid kind/message or closed case, `403` Viewer/unassigned write, `404` inaccessible case; records audit and notifies the submitter for citizen-visible kinds. `InformationRequest` moves the case to `AwaitingInformation` with a citizen-visible history entry. `InternalNote` and non-visible entries never reach the citizen projection. |
| `GET /api/cases/{id:guid}/updates` | OrganizationAdmin or assigned staff | `200 CaseUpdateResponse[]`; staff see internal and citizen-visible entries with visibility and author IDs; `404` inaccessible case. |
| `PATCH /api/cases/{id:guid}/assignment` | OrganizationAdmin; `AssignCaseRequest` (staff ID and assignment role) | `200` case assignment; `400` invalid role/target, `404` inaccessible case; target must be active staff in the same organization; records assignment history and audit. |
| `GET /api/cases/{id:guid}/assignment-history` | OrganizationAdmin or assigned staff | `200` assignment-history entries; `404` inaccessible case. |
| `POST /api/cases/{id:guid}/documents` | OrganizationAdmin or assigned CaseWorker; multipart `file` and optional `visibleToCitizen` (default false), max request body 11 MiB/file 10 MiB | `201 CaseDocumentResponse`; `400` empty file, invalid file name, oversize file, or declared-type signature mismatch, `403` Viewer/unassigned write, `404` inaccessible case; stores file and metadata and records audit. Declared `application/pdf`, `image/png`, `image/jpeg`, and `image/gif` content must match its magic signature; other declared media types are accepted by the arbitrary-file contract. Only documents marked `visibleToCitizen` are listed or downloadable by the citizen owner. |
| `GET /api/cases/{id:guid}/documents` | OrganizationAdmin or assigned staff | `200 CaseDocumentResponse[]`; `404` inaccessible case. |
| `GET /api/cases/{id:guid}/documents/{documentId:guid}` | OrganizationAdmin or assigned staff | `200` file stream with range support and attachment filename; `404` inaccessible case/document/file. |

The case detail, history, assignment-history, upload, and download actions
repeat organization/assignment checks; the query predicates and PostgreSQL RLS
provide additional tenant isolation. A Viewer assignment cannot update status,
details, documents, or updates.

Citizen tracking is owner-scoped in the controller and in PostgreSQL RLS. The
citizen transaction sets the transaction-local `civicflow.user_id`; the
`case_updates`, `case_status_history`, `case_documents`, and assigned
`staff_members` policies then add owner-scoped clauses on top of organization
isolation. A citizen list or identifier lookup is constrained to
`SubmittedByUserId = caller`; another citizen's case returns `404` without
disclosing whether it exists. Citizen routes do not accept an organization
selector.

### Platform case inspection (read-only)

These routes serve platform super administrators. They are read-only: status,
assignment, communication, and document changes remain scoped to the owning
organization, and the queries run inside a transaction-local
`civicflow.platform_read` context whose PostgreSQL policies are `SELECT`-only.
They require the platform `SuperAdmin` role and the business-endpoints gate;
when the gate is disabled they return `404`, and a non-super-admin receives
`403`.

| Method and path | Access / parameters | Response and effects |
|---|---|---|
| `GET /api/platform/cases?page={n}&pageSize={n}&organizationId={guid}&status={status}&priority={priority}&search={text}&sort={sort}` | SuperAdmin; page defaults `1`, pageSize defaults `20` (ranges `1–1,000,000` and `1–100`); `search` up to 200 characters; `sort` one of `created_desc` (default), `created_asc`, `updated_desc`, `updated_asc`, `priority_desc`, `reference_asc` | `200 PlatformCasePageResponse` (`items`, `page`, `pageSize`, `totalCount`, `totalPages`) with the reference number, title, status, priority, organization, assignee, submitter, and timestamps; `400` for an unsupported sort value. |
| `GET /api/platform/cases/{caseId:guid}` | SuperAdmin | `200 PlatformCaseDetailResponse` with the case summary, submitter and assignee, status history, updates, assignment history, and document metadata; `404` unknown case. Internal notes, internal-only documents, and non-visible explanations are included — platform inspection is not subject to citizen visibility rules. |
| `GET /api/platform/cases/{caseId:guid}/documents/{documentId:guid}` | SuperAdmin | `200` file stream with range support; `404` unknown case/document/file. Any document, including internal-only ones, is downloadable by a super administrator. |

### Notifications and background jobs

All notification endpoints require an authenticated user (cookie session or PAT); each query is constrained to the database-resolved caller's `RecipientUserId`. Unsafe cookie-authenticated operations remain subject to CSRF middleware. Page range is 1–1,000,000 and page size is 1–100. Expired notifications are excluded. The API returns only notification content and sanitized delivery codes, never email job payloads.

| Method and path | Access / parameters | Success, failures, and effects |
|---|---|---|
| `GET /api/notifications?page={n}&pageSize={n}&unreadOnly={bool}` | Authenticated caller; defaults page `1`, pageSize `20`; pageSize max `100` | `200 NotificationPageResponse` with the caller's items, total count, and unread count; `400` invalid paging. |
| `GET /api/notifications/unread-count` | Authenticated caller | `200 {count}` for the caller's unexpired unread notifications. |
| `PATCH /api/notifications/{id:guid}/read` | Authenticated recipient only | `204`; `404` if absent or owned by another user. Idempotent if already read. |
| `POST /api/notifications/read` | Authenticated caller | `200 {updatedCount}`; marks only the caller's unexpired unread items as read. |
| `GET /api/platform/jobs?status={status}&type={type}&from={date}&to={date}&correlationId={id}&page={n}&pageSize={n}` | SuperAdmin only; page defaults `1`, pageSize `25`, pageSize max `100` | `200 BackgroundJobPageResponse`; supported status/type filters only; invalid filters, bounds, or reversed dates return `400`. |
| `GET /api/platform/jobs/{id:guid}` | SuperAdmin only | `200 BackgroundJobDetailsResponse` with summary and sanitized attempt history; `404` unknown job. No raw payload or stack trace is returned. |
| `POST /api/platform/jobs/{id:guid}/cancel` | SuperAdmin only; rate-limited to 20 operations/minute per source IP | `204` queued/retry-scheduled job cancelled; `404` unknown job; `409` if job is running or terminal. Audited. |
| `POST /api/platform/jobs/{id:guid}/retry` | SuperAdmin only; same rate limit | `204` failed job requeued; `404` unknown job; `409` unless failed. Audited; the attempt ceiling and attempt history remain bounded. |

The worker accepts only the registered `notification.email` type and looks up the destination through the notification's recipient account. Case submission notifies its submitting user, assignment notifies the active account linked to the active same-organization staff record, and status changes notify the submitting user and the assigned staff account (`CaseWorker` or `Viewer`). A citizen-visible public update or information request notifies the submitter; an internal note does not. A citizen response notifies the active account linked to the assigned staff record. These messages contain no private case text, internal notes, or document contents. Invitation emails remain on their existing synchronous email path because the single-use invitation secret is intentionally not persisted. Membership and organization administration currently produce no new notification events.

### Audit, platform administration, dashboard, and errors

| Method and path | Access / parameters | Success, failures, and effects |
|---|---|---|
| `GET /api/platform/audit` | SuperAdmin; optional `category`, `action`, `actorUserId`, `organizationId`, `search`, `from`, `to`, `page`, `pageSize` | `200 AuditEventPageResponse`; platform-wide query with filter and page limits. `from` must not be later than `to`. |
| `GET /api/platform/audit/categories` | Authenticated `staff` or `superadmin` | `200 string[]`; returns static category names only (no audit event data). |
| `GET /api/organizations/{organizationId:guid}/audit` | SuperAdmin, or active same-organization member; same audit query filters | `200 AuditEventPageResponse`; OrganizationAdmin sees org events; Viewer/CaseWorker actor filter is mandatory and cannot be widened by query. |
| `GET /api/platform/users?page={n}&pageSize={n}&search={text}&isStaff={bool}&isCitizen={bool}&sort={value}` | SuperAdmin; page defaults `1`, pageSize defaults `25` (maximum `100`) | `200 PagedResponse<PlatformUserSummary>`; platform-wide account and role/membership summaries. Search/filter/sort are applied server-side. |
| `PATCH /api/platform/users/{userId:guid}/platform-role` | SuperAdmin-only provisioning API; `ChangePlatformRoleRequest` | `204`; synchronizes the platform-role field and Identity role, rotates the security stamp, and audits each change. Ordinary user-management screens do not expose this control. |
| `PATCH /api/platform/users/{userId:guid}/activate` | SuperAdmin | `204`; activates the account and rotates the security stamp so the change applies to live sessions. Unknown account: `404` (`KeyNotFoundException`, generic error envelope). |
| `PATCH /api/platform/users/{userId:guid}/deactivate` | SuperAdmin | `204`; deactivates the account and rotates the security stamp so live sessions lose access on the next request. Unknown account: `404` (`KeyNotFoundException`, generic error envelope). |
| `GET /api/platform/staff?page={n}&pageSize={n}&organizationId={guid}&isActive={bool}&search={text}` | SuperAdmin; page defaults `1`, pageSize defaults `25` (maximum `100`) | `200 PagedResponse<PlatformStaffSummary>`; platform-wide memberships, optionally filtered by organization, membership state, and search. |
| `PUT /api/platform/staff/{userId:guid}` | SuperAdmin; `UpdatePlatformStaffRequest` | `200 PlatformUserSummary`; updates a staff account's full name and phone number. `400` invalid name/phone; `404` missing account; `409` account has no organization membership. |
| `GET /api/platform/citizens?page={n}&pageSize={n}&search={text}&sort={value}` | SuperAdmin; page defaults `1`, pageSize defaults `25` (maximum `100`) | `200 PagedResponse<PlatformCitizenSummary>`; citizen accounts joined to their citizen profile; search and sort are server-side. |
| `PUT /api/platform/citizens/{userId:guid}` | SuperAdmin; `UpdatePlatformCitizenRequest` | `200 PlatformCitizenSummary`; updates profile and account name/phone. `400` invalid name/age; `404` missing account/profile; `409` duplicate NID/citizenship number. |
| `PATCH /api/platform/citizens/{userId:guid}/activate` | SuperAdmin | `204`; activates the citizen account. |
| `PATCH /api/platform/citizens/{userId:guid}/deactivate` | SuperAdmin | `204`; deactivates the citizen account. |
| `GET /api/platform/overview` | SuperAdmin | `200 PlatformOverviewResponse`; platform counters. |
| `GET /api/dashboard/platform` | SuperAdmin | `200 CaseStatistics`; platform-wide statistics. |
| `GET /api/dashboard/platform/organizations` | SuperAdmin | `200 PlatformOrganizationCaseBreakdown[]`; one entry per organization with `organizationId`, `organizationName`, `organizationCode`, `isActive`, `totalCases`, `openCases`, `resolvedCases`, `criticalPriorityCases`, and `highPriorityCases`. Reads through the platform read-only context. |
| `GET /api/dashboard/organization?organizationId={guid}` | Active organization member; optional ID must match resolved membership | `200 CaseStatistics`; `403` absent/mismatched organization context. |
| `GET /api/errors?isHandled={bool}&page={n}&pageSize={n}` | SuperAdmin; page defaults `1`, range `1–1,000,000`; pageSize defaults `25`, range `1–200` | `200 PagedResponse<ApplicationError>` with accurate total metadata; `400` out-of-range pagination. Error records include diagnostic details and are platform-admin-only. |
| `GET /api/errors/{errorId:guid}` | SuperAdmin | `200 ApplicationError`; `404` unknown error. |
| `PATCH /api/errors/{errorId:guid}/mark-handled` | SuperAdmin | `204` marked handled; `404` unknown error. |

## Error and Validation Conventions

- Unauthenticated requests receive `401`; authenticated users failing a policy
  or resource permission receive `403`. Cross-organization or unassigned case
  identifiers are queried through the authorized scope and return `404`.
- DataAnnotations and `[ApiController]` model binding return `400` for invalid
  request models or invalid pagination. Business-rule failures currently use
  controller-specific `{error}` objects; authentication flows use stable
  `code` and `error` values.
- Unhandled exceptions are caught by `GlobalExceptionHandlerMiddleware`.
  The middleware logs the exception with the caller's user ID, organization
  ID, and the request path, method, and query string via `IErrorLogger`
  (persisted to `application_errors`), then returns `application/json` with a
  generic envelope that never includes exception details:
  `{ "error": "<user-friendly message>", "statusCode": <code>, "timestamp": "<utc>" }`.
  Known exceptions map to stable responses (`UnauthorizedAccessException` →
  `401`, `ArgumentException` → `400`, `InvalidOperationException` → `400`,
  `KeyNotFoundException` → `404`, `TimeoutException` → `408`); anything else
  returns `500`. See [VALIDATION-AND-FORMATTING.md](VALIDATION-AND-FORMATTING.md) §9.
- Validation and normalization rules are shared across the API, domain, and
  database. Trimming and whitespace collapsing happen before length checks;
  optional blanks become `null`; emails are trimmed and lower-cased;
  identifiers are trimmed and upper-cased; phone numbers allow digits, spaces,
  and `+ - ( ) . /` with 6–30 characters and at least 6 digits. PostgreSQL
  CHECK constraints added by `AddValidationConstraints` enforce the critical
  bounds independently of the application. See
  [VALIDATION-AND-FORMATTING.md](VALIDATION-AND-FORMATTING.md) for the complete
  table, file rules, display formats, and change checklist.
- Duplicate/conflicting writes return `409` where the feature defines a
  conflict. Invitation unavailability returns `410`; authentication throttles
  return `429`; login lockout returns `423`; database readiness failure returns
  `503`.
- Paged collections return `{items,page,pageSize,totalCount,totalPages}`.
  Platform case/audit/job, account-directory, error, organization, membership,
  staff, and invitation list endpoints apply filters and stable ordering before
  database `Skip/Take`. These list routes default to page `1` and pageSize `25`
  (maximum `100`), except errors (maximum `200`).
- Mutating endpoints record audit events where the underlying feature writes
  them. Authentication, invitation, membership, organization, staff, case,
  and token changes are covered. Read operations do not write business audit
  events. Invitation create/resend/revoke events attribute the action to the
  authenticated administrator and retain the invitee address in event details;
  membership transfers emit events for both organizations.

## Current-user (`/me`) endpoints

`me` means the identity already authenticated on this request. It is not a
placeholder for a user ID and cannot be changed to select another account.
The authentication handler validates the Identity cookie, personal access
token, or JWT; the API then reads the user's identifier from the validated
principal and rechecks the active account and current roles. Organization
membership is resolved from the database, not trusted from a role or
organization value supplied by the browser.

| Method and path | Meaning of `me` | Access |
|---|---|---|
| `GET /api/auth/me` | Safe account, role, and permission summary for the caller | Any authenticated account |
| `GET /api/profile/me` | Caller-managed citizen or staff profile | Citizen or staff; business-endpoint gate applies |
| `PUT /api/profile/me` | Updates only the caller's permitted profile fields | Citizen or staff; business-endpoint gate and CSRF apply to cookie clients |
| `GET /api/citizens/me` | Citizen profile attached to the caller's Identity account | Citizen |
| `POST /api/citizens/me` | Creates or updates that citizen profile | Citizen; CSRF applies to cookie clients |
| `PUT /api/citizens/me` | Updates that citizen profile | Citizen; CSRF applies to cookie clients |
| `GET /api/organizations/me` | Active organization memberships of the signed-in staff account | Staff; business-endpoint gate applies |

For example, `GET /api/profile/me` has no user ID in the path or query. A
successful response contains profile information for the authenticated
principal only:

```json
{
  "userId": "00000000-0000-0000-0000-000000000001",
  "fullName": "Example User",
  "email": "user@example.test",
  "emailConfirmed": true,
  "accountType": "Citizen",
  "roles": ["citizen"],
  "organizationId": null,
  "organizationName": null,
  "organizationRole": null
}
```

The actual profile response also includes the applicable optional contact
fields and update timestamp. The exact fields are described by `ProfileResponse`
in Swagger's Schemas section. Use these self-service routes rather than
accepting an arbitrary user ID: this prevents callers from asking the API to
read or edit another account's profile.

## Routes without a resource ID

A route without a final resource ID is intentional when the action is scoped
to the authenticated account, operates on a collection, creates a new
resource, or takes its target from a validated body or query filter. The full
method, path, input, and authorization inventory above is authoritative; this
section explains how to interpret the main route families.

| Route family | Where the target comes from | Example and behavior |
|---|---|---|
| `GET /api/health`, `GET /api/health/ready`, `GET /api/config/public` | Service-wide status or safe public configuration | No identity or request body is needed. Readiness reports `503` if PostgreSQL is unavailable. |
| `/api/auth/*` login, registration, recovery, logout, CSRF, and token exchange routes | Credentials or one-time invitation/recovery token in the request body; otherwise the authenticated principal | `POST /api/auth/login` takes `LoginRequest`; `POST /api/auth/logout` invalidates the caller's session; neither route accepts a target user ID. |
| `GET /api/auth/me`, `/api/profile/me`, `/api/citizens/me`, `/api/organizations/me` | Validated authenticated principal and server-side account/membership lookup | These return only the caller's data, as described above. |
| `GET` collection routes such as `/api/cases`, `/api/citizens/cases`, `/api/organizations`, `/api/staff`, `/api/platform/cases`, `/api/dashboard/platform/organizations`, and `/api/notifications` | Authorized collection scope, optionally narrowed by query parameters | Example: `GET /api/citizens/cases?page=1&pageSize=20&status=UnderReview` returns only the caller's cases. |
| `POST /api/cases`, `POST /api/organizations`, and `POST /api/citizens/cases` | A new resource is created from the validated request body; identity and permitted organization scope are derived server-side | Staff case creation uses `CreateCaseRequest`; citizen intake uses multipart `CitizenCreateCaseRequest`, including its selected organization ID, which is independently validated. The response supplies the new case ID. |
| `POST /api/notifications/read` | All unread, unexpired notifications belonging to the caller | The body is empty; it is a collection action and cannot affect another user's notifications. |
| `GET /api/dashboard/organization` | The authenticated user's selected active membership; an optional `organizationId` query value is accepted only if it matches | The query value is a selector, not authority to read another organization's statistics. |
| `POST /api/auth/invitations/lookup` and `POST /api/auth/invitations/accept` | A single-use invitation token in the request body | The token identifies the pending invitation; it is validated for expiry, revocation, and prior use. |
| `POST /api/organizations/{organizationId}/memberships` and `/invitations` | The organization is identified in the parent route; the request body identifies the requested membership/invite details | The administrator must be authorized for that organization. The route has no membership or invitation ID because it creates one. |

Example collection request and response shape:

```http
GET /api/notifications?page=1&pageSize=20&unreadOnly=true
```

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalCount": 0,
  "unreadCount": 0
}
```

Example creation route: the request body supplies the new organization
attributes; the response supplies its generated route ID. Only a
SuperAdmin can call this endpoint.

```http
POST /api/organizations
Content-Type: application/json

{"name":"Example Public Services","code":"EXAMPLE","organizationTypeId":null}
```

```http
201 Created
Location: /api/organizations/00000000-0000-0000-0000-000000000002
```

```json
{
  "id": "00000000-0000-0000-0000-000000000002",
  "name": "Example Public Services",
  "code": "EXAMPLE",
  "organizationTypeId": "00000000-0000-0000-0000-000000000003",
  "organizationTypeName": "Uncategorized",
  "isActive": true,
  "createdAt": "2025-01-15T10:30:00Z",
  "updatedAt": "2025-01-15T10:30:00Z"
}
```

Example user-scoped collection action: the request has no body and can mark
only the authenticated user's unread notifications as read.

```http
POST /api/notifications/read

200 OK
```

```json
{"updatedCount":2}
```

For body-based writes, request DTO validation is applied before feature logic.
For cookie-authenticated unsafe methods, include the antiforgery cookie and
`X-CSRF-TOKEN` request header; bearer requests use the existing non-ambient
bearer scheme. No route shape removes authentication, authorization, tenant
resolution, or resource-level checks.

## Swagger Schemas

Swagger's **Schemas** section describes reusable request and response DTOs. It
does not expose database entities or grant additional API access. Operations
refer to these models to document their JSON body and response shape.

| Workflow | Representative schemas | Purpose and important validation |
|---|---|---|
| Registration and authentication | `RegisterRequest`, `LoginRequest`, `ResetPasswordRequest`, `ChangePasswordRequest`, `CurrentUserResponse` | Registration requires name, email, phone, identity numbers, and a password of 12–128 characters; Identity enforces the effective password policy. Login and recovery models carry credentials or one-time tokens only in request bodies. `CurrentUserResponse` contains safe identity and permission information, never credentials. |
| Profiles | `ProfileResponse`, `UpdateProfileRequest`, `CitizenProfileResponse`, `CreateCitizenProfileRequest`, `UpdateCitizenProfileRequest` | Self-service profile updates exclude account role and organization membership. Optional contact and demographic fields are validated against their declared lengths and ranges. |
| Organizations and memberships | `CreateOrganizationRequest`, `UpdateOrganizationRequest`, `OrganizationResponse`, `CreateOrganizationMembershipRequest`, `ChangeOrganizationMembershipRoleRequest`, `CreateInvitationRequest` | Organization and membership IDs identify existing records only where the route requires them. Role values are restricted by backend policy; Organization Admins cannot grant platform roles or promote other users to Organization Admin. |
| Cases and updates | `CreateCaseRequest`, `CitizenCreateCaseRequest`, `UpdateCaseRequest`, `ChangeCaseStatusRequest`, `ChangeCasePriorityRequest`, `AddCaseUpdateRequest`, `CitizenCaseDetails`, `PlatformCaseSummary`, `PlatformCaseDetailResponse` | Case titles, descriptions, status, priority, update messages, and upload metadata are validated. Citizen intake is multipart and validates the selected active organization; citizen case responses are scoped to the caller's own case. Platform case schemas are read-only inspection contracts for super administrators. |
| Documents | `CaseDocumentResponse`, `CitizenCaseDocument`, `PlatformCaseDocumentEntry` | Metadata responses do not disclose storage keys or local file paths. File streams are returned only through the corresponding authorized document route. |
| Notifications and background jobs | `NotificationPageResponse`, `NotificationResponse`, `BackgroundJobPageResponse`, `BackgroundJobDetailsResponse` | Notification lists are limited to the caller. Job details omit raw payloads and stack traces; administrative routes require platform authorization. |

Required fields, enum values, maximum lengths, ranges, and request/response
examples are shown on the operation or schema where available. `[Required]`,
email, phone, string-length, range, trimmed-string, and file validation
attributes are part of the actual contracts; database constraints provide
additional validation for selected persisted values. A field marked nullable
or optional may be omitted or set to `null` only where its contract permits.
Treat response-only schemas as output contracts, not writable database models.

## Known Contract Limitations

- Error response envelopes are not fully normalized: framework validation
  uses MVC validation responses, feature errors often use `{error}`, and auth
  returns `{code,error}`. Clients should handle these documented shapes.
- Case updates are append-only through the API: staff cannot edit or delete a
  published update, and there is no bulk operation, export, or general document
  deletion endpoint.
- Upload restriction is intentionally limited to size, file-name shape, and
  magic-signature checks for declared PDF/PNG/JPEG/GIF types. Arbitrary other
  types remain allowed, and malware scanning is not implemented. Citizens cannot edit a submitted case or add attachments
  after submission.
- Development email is not delivered when SMTP is unconfigured; the sender
  prints recipients, subjects, message bodies, and one-time action links to the
  API console. Treat that output as sensitive. Configure SMTP outside
  Development for actual delivery. Authentication integration tests use an
  in-memory email capture service.
- PostgreSQL worker claim, SMTP delivery, and lease recovery require a live
  PostgreSQL instance and SMTP relay; EF InMemory tests do not exercise that
  provider-specific execution path.
