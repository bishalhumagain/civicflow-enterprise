# API Contract and Authorization Matrix

This inventory reflects the controller routes currently mapped by
`app.MapControllers()`; the API has no separately mapped minimal endpoints.
The request/response names below refer to the DTOs in
`PublicServiceCaseManagement.Api/Features`. Business endpoints that consult
`BusinessEndpoints:Enabled` return `404` when that gate is disabled; citizen
self-service routes are exempt and remain available.

## Authentication and Tenant Resolution

- Browser clients use the ASP.NET Core Identity HttpOnly cookie. Unsafe API
  requests require the antiforgery cookie and `X-CSRF-TOKEN` header when
  `Security:RequireAntiforgery` is enabled. Non-browser clients may use a
  personal access token in the `Authorization: Bearer` header.
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
- Every unsafe `/api/*` request is subject to the configured CSRF middleware.
  Authentication and authorization policies are applied after authentication
  and before endpoint execution.

## Role and Resource Matrix

| Resource / operation | Citizen | Viewer | CaseWorker | OrganizationAdmin | SuperAdmin |
|---|---|---|---|---|---|
| Own account/profile and own PATs | Read/update own profile; own tokens | Own profile/tokens | Own profile/tokens | Own profile/tokens | Own tokens; platform profile is not provided |
| Citizen case intake | Submit to an active organization; list/read own submitted cases | No | No | No | No |
| Organization directory | Active public-intake organizations only | Selected organization's staff list; no phone numbers | Selected organization's staff list; no phone numbers | Own organization's staff and phone numbers; Organizations view for administered organizations | Platform directory and organization administration |
| Case reads | Own submitted cases only (summary and status history) | Only assigned Viewer cases | Only assigned staff cases | All cases in selected organization | No global case API; requires the endpoint's normal organization context |
| Case changes | No | Read-only | Assigned CaseWorker case details/status/documents only | Organization cases; assign/reassign; delete with confirmation | No implicit case-wide bypass |
| Notifications | Own notifications only | Own notifications only | Own notifications only | Own notifications only | Own notifications only |
| Background jobs | No | No | No | No | Global job summaries/details and state-eligible retry/cancel |
| Memberships/invitations | No | No | No | Selected organization they administer; cannot grant `OrganizationAdmin`; invitations create `CaseWorker` memberships | Platform-authorized organization membership and invitation operations, including permitted admin provisioning |
| Audit | No | Own actor events in selected organization | Own actor events in selected organization | All events in selected organization | Platform-wide audit and explicitly authorized organization audit |
| Platform directory, organization CRUD, dashboard, errors | No | No | No | Organizations view lists only administered organizations; no organization CRUD | Explicit platform management and cross-organization reports |

Additional boundaries:

- `StaffClient` means authenticated platform `staff` or `superadmin`; it does
  not establish an organization role by itself.
- CaseWorker write permission is limited to the case assigned to their active
  staff record with the `CaseWorker` case assignment role. Viewer assignments
  are read-only. Assignment itself is OrganizationAdmin-only and only accepts
  active staff in the same organization.
- Organization administrators can read and manage cases in their active
  organization, and can delete only after sending the explicit confirmation
  request. They cannot grant themselves or another member the
  `OrganizationAdmin` membership role.
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
| `POST /api/auth/logout` | Authenticated; no body | `204`; clears the current authentication session. |
| `POST /api/auth/verify-email` | Anonymous; `VerifyEmailRequest` | `200 AuthOperationResult`; invalid/expired token `400`. |
| `POST /api/auth/resend-verification` | Anonymous; `ResendVerificationRequest` | `200` enumeration-safe result; may send verification email. |
| `POST /api/auth/forgot-password` | Anonymous; `ForgotPasswordRequest` | `200` enumeration-safe result; may send password-reset email. |
| `POST /api/auth/reset-password` | Anonymous; `ResetPasswordRequest` | `200 AuthOperationResult`; invalid token/password `400`; resets password. |
| `POST /api/auth/change-password` | Authenticated; `ChangePasswordRequest` | `200 AuthOperationResult`; failed current/new password `400`; changes own password. |
| `GET /api/auth/me` | Authenticated; no parameters | `200 CurrentUserResponse`; `401` for inactive/unresolvable account. |
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
| `GET /api/organizations` | SuperAdmin or authenticated staff; none | `200 OrganizationResponse[]`; SuperAdmin sees all organizations, OrganizationAdmin sees active organizations they administer, other staff receive an empty list. Every row carries `organizationTypeId` and `organizationTypeName`. |
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
| `GET /api/organizations/{organizationId:guid}/memberships?isActive={bool}` | Same membership administrators; optional `isActive` | `200 OrganizationMembershipResponse[]`; restricted to the specified authorized organization. |
| `GET /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}` | Same membership administrators | `200 OrganizationMembershipResponse`; `404` outside/missing membership. |
| `PUT /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}/role` | Same membership administrators; `ChangeOrganizationMembershipRoleRequest` | `200 OrganizationMembershipResponse`; `400` invalid/prohibited role, `404` missing membership. |
| `PATCH /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}/deactivate` | Same membership administrators | `200 OrganizationMembershipResponse`; `404` missing membership. |
| `PATCH /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}/reactivate` | Same membership administrators | `200 OrganizationMembershipResponse`; `404` missing membership. |
| `POST /api/organizations/{organizationId:guid}/memberships/{membershipId:guid}/transfer` | SuperAdmin; `TransferOrganizationMembershipRequest` | `200 OrganizationMembershipResponse` for the new destination membership; deactivates the source membership and audits `membership.transferred`. `400` same organization or invalid role; `404` missing membership/organization; `409` inactive destination or duplicate destination membership. |
| `POST /api/organizations/{organizationId:guid}/invitations` | SuperAdmin, or OrganizationAdmin for their selected organization; `CreateInvitationRequest` | `201 InvitationResponse`; `400` invalid/prohibited role, `404` missing organization, `409` duplicate/conflict; generates single-use expiring token and sends invite email. OrganizationAdmins may invite CaseWorkers only. |
| `GET /api/organizations/{organizationId:guid}/invitations` | Same invitation administrators | `200 InvitationResponse[]`; organization-scoped list; token/hash is never returned. |
| `POST /api/organizations/{organizationId:guid}/invitations/{invitationId:guid}/resend` | Same invitation administrators | `200 InvitationResponse`; `404` missing, `409` used/revoked/unavailable; creates and sends a replacement single-use link. |
| `DELETE /api/organizations/{organizationId:guid}/invitations/{invitationId:guid}` | Same invitation administrators | `204` revoked; `404` missing, `409` already used/unavailable. |
| `GET /api/staff?isActive={bool}` | Active staff membership; optional filter | `200 StaffSummary[]`; selected-organization staff only. Phone number is returned to OrganizationAdmin and omitted for other organization roles. |
| `POST /api/staff` | OrganizationAdmin in selected organization; `CreateStaffMemberRequest` | `201 StaffSummary`; `400` invalid values, `409` duplicate email; creates an organization-scoped staff directory record and audit event, but does not create an account or membership. Use the invitation route to enable sign-in. |
| `GET /api/staff/platform?organizationId={guid}&isActive={bool}` | SuperAdmin; both filters optional | `200 StaffSummary[]`; platform-wide staff directory. |
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
| `POST /api/cases` | CaseWorker or OrganizationAdmin; `CreateCaseRequest` | `201 CreateCaseResponse`; `400` invalid case; creates a case in the resolved organization and records audit event. |
| `GET /api/cases?page={n}&pageSize={n}` | Active organization member; defaults page `1`, pageSize `20`, page range `1–1,000,000`, pageSize range `1–100` | `200 PagedCasesResponse`; admin gets org cases, staff get their assignments only. |
| `GET /api/cases/{id:guid}` | OrganizationAdmin or assigned staff | `200 CreateCaseResponse`; `404` missing, cross-org, or unassigned case. |
| `PUT /api/cases/{id:guid}` | OrganizationAdmin or assigned CaseWorker; `UpdateCaseRequest` | `200 CreateCaseResponse`; `400` invalid/business-closed case, `403` Viewer/unassigned write, `404` inaccessible case; updates permitted case details. |
| `DELETE /api/cases/{id:guid}` | OrganizationAdmin; `ConfirmCaseDeletionRequest` must explicitly confirm | `204`; `400` missing confirmation, `403` unauthorized role, `404` inaccessible case; deletes case/history/document metadata and stored files and records an audit event. |
| `PATCH /api/cases/{id:guid}/status` | OrganizationAdmin or assigned CaseWorker; `ChangeCaseStatusRequest` (`newStatus`, optional `explanation`, `explanationVisibleToCitizen`) | `200 ChangeCaseStatusResponse`; `400` invalid transition/explanation, `403` Viewer/unassigned write, `404` inaccessible case; persists status history and audit. The transition is always citizen-visible; the explanation is projected to the citizen timeline only when the visibility flag is true. |
| `GET /api/cases/{id:guid}/history` | OrganizationAdmin or assigned staff | `200` status-history entries including explanation and `isVisibleToCitizen`; `404` inaccessible case. |
| `POST /api/cases/{id:guid}/updates` | OrganizationAdmin or assigned CaseWorker; `AddCaseUpdateRequest` (`kind`: `PublicUpdate`, `InformationRequest`, or `InternalNote`; message 1–4,000) | `201 CaseUpdateResponse`; `400` invalid kind/message or closed case, `403` Viewer/unassigned write, `404` inaccessible case; records audit and notifies the submitter for citizen-visible kinds. `InformationRequest` moves the case to `AwaitingInformation` with a citizen-visible history entry. `InternalNote` and non-visible entries never reach the citizen projection. |
| `GET /api/cases/{id:guid}/updates` | OrganizationAdmin or assigned staff | `200 CaseUpdateResponse[]`; staff see internal and citizen-visible entries with visibility and author IDs; `404` inaccessible case. |
| `PATCH /api/cases/{id:guid}/assignment` | OrganizationAdmin; `AssignCaseRequest` (staff ID and assignment role) | `200` case assignment; `400` invalid role/target, `404` inaccessible case; target must be active staff in the same organization; records assignment history and audit. |
| `GET /api/cases/{id:guid}/assignment-history` | OrganizationAdmin or assigned staff | `200` assignment-history entries; `404` inaccessible case. |
| `POST /api/cases/{id:guid}/documents` | OrganizationAdmin or assigned CaseWorker; multipart `file` and optional `visibleToCitizen` (default false), max request body 11 MiB/file 10 MiB | `201 CaseDocumentResponse`; `400` invalid upload, `403` Viewer/unassigned write, `404` inaccessible case; stores file and metadata and records audit. Only documents marked `visibleToCitizen` are listed or downloadable by the citizen owner. |
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
| `GET /api/platform/users` | SuperAdmin | `200 PlatformUserSummary[]`; platform-wide accounts and role/membership summaries. |
| `GET /api/platform/staff` | SuperAdmin | `200 PlatformStaffSummary[]`; platform-wide staff memberships. |
| `PUT /api/platform/staff/{userId:guid}` | SuperAdmin; `UpdatePlatformStaffRequest` | `200 PlatformUserSummary`; updates a staff account's full name and phone number. `400` invalid name/phone; `404` missing account; `409` account has no organization membership. |
| `GET /api/platform/citizens` | SuperAdmin | `200 PlatformCitizenSummary[]`; citizen accounts joined to their citizen profile. |
| `PUT /api/platform/citizens/{userId:guid}` | SuperAdmin; `UpdatePlatformCitizenRequest` | `200 PlatformCitizenSummary`; updates profile and account name/phone. `400` invalid name/age; `404` missing account/profile; `409` duplicate NID/citizenship number. |
| `PATCH /api/platform/citizens/{userId:guid}/activate` | SuperAdmin | `204`; activates the citizen account. |
| `PATCH /api/platform/citizens/{userId:guid}/deactivate` | SuperAdmin | `204`; deactivates the citizen account. |
| `GET /api/platform/overview` | SuperAdmin | `200 PlatformOverviewResponse`; platform counters. |
| `GET /api/dashboard/platform` | SuperAdmin | `200 CaseStatistics`; platform-wide statistics. |
| `GET /api/dashboard/organization?organizationId={guid}` | Active organization member; optional ID must match resolved membership | `200 CaseStatistics`; `403` absent/mismatched organization context. |
| `GET /api/errors?isHandled={bool}&page={n}&pageSize={n}` | SuperAdmin; page defaults `1`, range `1–1,000,000`; pageSize defaults `25`, range `1–200` | `200 ApplicationError[]`; `400` out-of-range pagination. Error records include diagnostic details and are platform-admin-only. |
| `GET /api/errors/{errorId:guid}` | SuperAdmin | `200 ApplicationError`; `404` unknown error. |
| `PATCH /api/errors/{errorId:guid}/mark-handled` | SuperAdmin | `204` marked handled; `404` unknown error. |

## Error and Validation Conventions

- Unauthenticated requests receive `401`; authenticated users failing a policy
  or resource permission receive `403`. Cross-organization or unassigned case
  identifiers are queried through the authorized scope and return `404`.
- DataAnnotations and `[ApiController]` model binding return `400` for invalid
  request models or invalid pagination. Business-rule failures currently use
  controller-specific `{error}` objects; authentication flows use stable
  `code` and `error` values; the global exception middleware returns a generic
  server error without exception details.
- Duplicate/conflicting writes return `409` where the feature defines a
  conflict. Invitation unavailability returns `410`; authentication throttles
  return `429`; login lockout returns `423`; database readiness failure returns
  `503`.
- Not every collection route is paginated: current staff, membership,
  organization, platform-directory, health, and case-history collections
  return arrays. This is an implementation limitation, not a promise of an
  unbounded dataset; add paging before those collections are used at
  production scale.
- Mutating endpoints record audit events where the underlying feature writes
  them. Authentication, invitation, membership, organization, staff, case,
  and token changes are covered. Read operations do not write business audit
  events.

## Known Contract Limitations

- Error response envelopes are not fully normalized: framework validation
  uses MVC validation responses, feature errors often use `{error}`, and auth
  returns `{code,error}`. Clients should handle these documented shapes.
- Case updates are append-only through the API: staff cannot edit or delete a
  published update, and there is no bulk operation, export, or general document
  deletion endpoint. Citizens cannot edit a submitted case or add attachments
  after submission.
- The email sender is secure by default but requires SMTP configuration outside
  Development; see the README deployment settings. Development logs no message
  body or action token and fails explicitly when delivery is attempted.
- PostgreSQL worker claim, SMTP delivery, and lease recovery require a live
  PostgreSQL instance and SMTP relay; EF InMemory tests do not exercise that
  provider-specific execution path.
