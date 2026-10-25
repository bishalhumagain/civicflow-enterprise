# Integration Verification

This document records the verification evidence for CivicFlow Enterprise. It describes what has been tested automatically, what has been exercised over HTTP against a live PostgreSQL instance, and what remains to be verified. For the project overview and API reference, see the [README](../README.md). For the roadmap, see the [Delivery Roadmap](DELIVERY-ROADMAP.md).

---

## Table of Contents

- [Automated Test Coverage](#automated-test-coverage)
- [Over-HTTP Verification Results](#over-http-verification-results)
  - [Browser end-to-end verification](#browser-end-to-end-verification)
- [Required Local Verification Sequence](#required-local-verification-sequence)
- [Required Acceptance Evidence](#required-acceptance-evidence)
- [Port Note](#port-note)

---

## Automated Test Coverage

The automated suite contains 383 tests. Most use EF Core InMemory and a synthetic authentication handler; a subset exercises the real ASP.NET Core Identity cookie flow. Three PostgreSQL row-level-security tests are opt-in and report as skipped unless disposable connection strings are supplied. A five-test Playwright/Chromium suite exercises the citizen and staff tracking journeys against a disposable PostgreSQL database. The default `dotnet test` run reports 380 passed and 3 skipped.

### Authentication lifecycle

Real Identity cookie flow tests (with a cookie container and a capturing email sender) cover:
- Registration with email-confirmation gating
- Register → verify → login → `me`
- Wrong-password rejection
- Duplicate-email enumeration safety
- Lockout (`423`)
- Forgot-password/reset-password
- Change-password
- Logout

### Anti-forgery (CSRF)

Dedicated tests assert that unsafe requests without a valid `X-CSRF-TOKEN` are rejected (`400`) and that a valid token is accepted.

### Portal-scoped sign-in

Covered by automated integration tests against the real Identity cookie flow:
- A citizen account is accepted on `POST /api/auth/login` and rejected with `403` / `not_an_organization_account` on `POST /api/auth/login/organization`
- A staff account is rejected with `403` / `not_a_citizen_account` on the citizen endpoint and accepted on the organization endpoint
- A super administrator is rejected with `403` / `use_platform_admin_portal` on the organization endpoint and accepted with `200` on `POST /api/auth/login/platform-admin`
- Staff and citizen accounts are rejected on the platform endpoint with `not_a_platform_admin`
- A rejected attempt leaves no session behind (`GET /api/auth/me` answers `401`)
- The login response reports the session roles
- A staff account is denied on a citizen-only endpoint

### Personal access tokens

Covered by automated integration tests against the real bearer scheme:
- Anonymous create is rejected
- The plaintext token is returned exactly once and only a hash is persisted
- The token authenticates `me` and carries the owner's roles
- A citizen token receives `403` on a staff endpoint
- Revocation invalidates the credential immediately
- Unknown, tampered, and expired tokens are rejected

### Invitation lifecycle

Covered by automated integration tests:
- An administrator invitation stores only a SHA-256 token hash (the raw token is never persisted or returned)
- The emailed link resolves through `POST /api/auth/invitations/lookup`
- Acceptance with a policy-compliant password creates the account, grants the `staff` role, and creates the membership and staff record
- The redeemed link cannot be used a second time (`410` / `invitation_unavailable`)
- Expired and revoked invitations are rejected with the same code
- An unknown token discloses nothing
- A duplicate pending invitation and an invitation for an existing active member are rejected with `409`
- An attempt to invite `superadmin` is rejected
- Cross-organization invitation access is denied
- An invitation for an existing citizen links that account instead of duplicating it
- Staff created from an organization invitation can load `/api/staff` and `/api/cases?page=1&pageSize=20` after signing in
- Accepting an invitation reactivates an inactive membership and assigns the invitation's organization role
- Organization administrators cannot invite or promote another OrganizationAdmin
- Citizen intake returns active organizations and creates a case in the selected organization with submitter attribution and arbitrary file upload
- A citizen-submitted case is routed to an organization administrator: assigned to an active admin staff member when one exists, otherwise every active admin is notified and the case stays unassigned, and an organization with no administrator is a safe no-op
- A case status change notifies both the submitter and the staff member assigned to the case (`CaseWorker` and read-only `Viewer`)
- Assignment role changes between CaseWorker and Viewer are recorded in assignment history
- Staff case lists and identifier lookups are restricted to assigned cases; unassigned and another staff member's cases return no data
- CaseWorker assignments can update cases, while Viewer assignments are read-only; only organization administrators can assign cases
- Organization administrators can update case details and permanently delete cases only after confirmation; CaseWorkers cannot delete, and Viewer detail updates are forbidden
- CaseWorkers and Viewers can read their own organization activity without seeing other actors' events, including when attempting to filter by another actor ID
- `GET /api/organizations/me` returns only the caller's active memberships; non-member organization selection is forbidden
- Organization dashboard statistics reject an organization ID outside the caller's resolved membership context

### Super administrator bootstrap

Covered by automated integration tests:
- A fully configured seed creates the account and it signs in on the platform portal
- A partially configured seed (`email` without `password`, or the reverse) fails startup
- A password that violates the password policy fails startup
- An unconfigured seed skips creation
- An existing damaged account is repaired
- An existing account whose password was changed is not silently overwritten

### Citizen self-service profile

Covered by automated integration tests using the synthetic authentication handler:
- Anonymous requests return `401`
- Staff requests return `403`
- Unlinked identities return `401`
- Create/update behavior and duplicate validation are asserted
- Citizen routes remain available when `BusinessEndpoints:Enabled` is false
- Citizen case intake and own-case list/detail work with the gate disabled

### Unified profile

Covered by automated integration tests:
- Citizen/staff `GET/PUT /api/profile/me`
- Duplicate NID returns `409`
- Business-endpoint gating enforced
- Superadmin exclusion returns `403`
- Create-on-update for missing citizen profile

### Audit trail

Covered by automated tests:
- Audit events are persisted with the actor identity, category, action, organization, entity, and detail
- A failing audit write does not fail the audited operation
- The query service filters by category, action, free text, and date range, pages the results, and scopes organization reads to the requested organization

### Platform administration

Covered by automated integration tests:
- Organization deletion returns `204` for a super administrator, `404` for an unknown organization, `409` while the organization still holds cases, and `403` without the super-administrator role
- The platform users, staff, and overview endpoints answer only for super administrators
- The platform and organization audit endpoints enforce their role and scope rules
- The staff phone update accepts a valid number, rejects an invalid one with `400`, and persists the change

### Cases

Covered by automated integration tests:
- Case CRUD, status changes, assignments, paging

### Case documents

Covered by automated integration tests:
- Document upload/download

### Citizen case tracking

Covered by automated integration tests:
- The citizen list is paged, searchable by reference/title, filterable by status, and sortable by creation or update time, and returns only the caller's cases
- The detail projection contains the description, organization, status, citizen-visible status timeline, public updates, information requests, citizen responses, and citizen-visible documents, plus `canRespond` and `awaitingInformation`
- Internal notes and internal-only documents are filtered in the database query and never returned to a citizen response; status explanations are returned only when marked citizen-visible
- A citizen response is accepted only from the case owner, is rejected on closed/rejected cases, moves `AwaitingInformation` to `UnderReview` with a visible history entry, and notifies the assigned staff account
- Unknown and cross-citizen identifiers return `404`; citizen credentials receive `403` from staff endpoints
- Staff can add `PublicUpdate`, `InformationRequest`, and `InternalNote` entries; an information request transitions the case to `AwaitingInformation`, and an internal note produces no citizen-visible data or notification
- Staff uploads default to internal and become citizen-visible only when `visibleToCitizen` is true
- Invalid transitions and updates to closed cases return `400`

### Staff, organizations, memberships

Covered by automated integration tests:
- Staff CRUD, platform staff listing, phone update
- Organization CRUD, delete rules
- Membership CRUD, role changes

### PostgreSQL migrations and runtime-role restrictions

`CitizenCaseTrackingRlsTests` (3 tests) runs against a disposable PostgreSQL database when both connection strings are supplied. It applies the real EF Core migrations with an administrative connection, then probes the policies through a non-superuser, non-`BYPASSRLS` runtime login. Verified results:

- The migrations were applied and the citizen tracking migration entries (`AddCitizenCaseTrackingRls`, `AddCitizenCaseTrackingUpdates`, and `AddCitizenTrackingRls`) are present in `__EFMigrationsHistory`.
- RLS is enabled and forced on `cases`, `case_status_history`, `case_updates`, `case_documents`, and `staff_members`.
- A citizen context sees only its own case and its visible updates, visible documents, visible status entries, and the assigned staff row; internal notes and internal documents are invisible.
- The owning organization context sees the full tenant, including internal notes and internal documents, but no other organization.
- Citizen writes to an internal note, another citizen's case, or another citizen's history are rejected with PostgreSQL `42501`; a same-case visible write succeeds.
- The runtime role cannot create schema objects and is confirmed not to be a superuser or hold `BYPASSRLS`.

See [RLS-DESIGN.md](RLS-DESIGN.md) for the policy design. The suite is opt-in rather than part of the default test run; wiring the disposable PostgreSQL fixture into CI remains open.

---

## Over-HTTP Verification Results

The following were additionally exercised over HTTP against a local PostgreSQL 17 instance. A browser-level run of the citizen/staff tracking flows was also performed with Playwright and bundled Chromium against a disposable PostgreSQL 17 database; see [Browser end-to-end verification](#browser-end-to-end-verification).

### Full auth journey

Register → confirmation link in the API log → verify → login → `me` → create PAT → list → bearer `me` (`200`) → staff endpoint with a citizen PAT (`403`) → revoke (`204`) → bearer `me` (`401`).

Sign-in failures confirmed to return the machine-readable codes `email_not_confirmed` (`403`) and `invalid_credentials` (`401`).

### Portal/role matrix

| Account | Citizen portal | Organization portal | Platform portal |
|---|---|---|---|
| Citizen | `200`, `portal: citizen`, `roles: [citizen]` | `403`, `not_an_organization_account` | `403`, `not_a_platform_admin` |
| Super admin | `403`, `not_a_citizen_account` | `403`, `use_platform_admin_portal` | `200`, `portal: platformadmin`, `roles: [superadmin]` |

- A rejected attempt left no session (`401` on `me`)
- Role-gated endpoints: citizen received `403` from `/api/staff` and `/api/cases` but `200` from `/api/citizens/me`; super admin received `403` from `/api/citizens/me`
- CORS preflight for `http://localhost:5173` returned `Access-Control-Allow-Origin: http://localhost:5173` with `Access-Control-Allow-Credentials: true`

### Invitation lifecycle

- Platform administrator created an invitation (`201`, expiry 72 hours later, response carries no token)
- Emailed `/setup-password?token=…` link taken from the API log
- `POST /api/auth/invitations/lookup` returned the organization, name, email, role, and expiry
- `POST /api/auth/invitations/accept` with a policy-compliant password returned `200` (`succeeded`, `portal: organization`)
- Redeemed link then answered `410` / `invitation_unavailable`
- Invited account signed in on the organization portal (`200`, `roles: [staff]`) and was refused on the platform portal (`403` / `not_a_platform_admin`)
- `me` response reported `emailConfirmed: true` and the `organization.manage` permission
- Could read the organization-scoped staff directory
- Refused (`403`) from invitation administration (not an administrator of that organization)
- Revoked invitation answered `204` on revoke and `410` on redemption
- Invitation for an address that already had a membership answered `409`
- Attempt to invite `superadmin` answered `400`
- Unknown token answered `410` with no disclosure
- Rejected portal attempt left no session (`401` on `me`)
- Database confirms `account_invitations.TokenHash` stores a 64-character hash only
- Created account's password is an ASP.NET Core Identity hash

### Platform administration surface

- Migration `AddAuditTrailAndPhoneNumbers` applied cleanly
- Platform administrator signed in on the platform portal (`200`, `portal: platformadmin`, `roles: [superadmin]`)
- Created an organization (`201`)
- Saw the matching `organization.created` event in the organization-scoped audit (`totalCount: 1`) and in the platform audit filtered by the `Organization` category
- Deleted the organization (`204`), received `404` when repeating the delete
- `GET /api/platform/overview` returned counters (`userCount`, `staffCount`, `organizationCount`, `invitationCount`, and active/pending variants)
- `GET /api/platform/users` returned every account with roles, platform role, memberships, and citizen/staff flags
- `GET /api/platform/staff` returned every staff membership with organization, role, and status
- `PUT /api/platform/staff/{userId}` updated a staff account's full name and phone number and rejected an account with no organization membership with `409`
- `GET /api/organizations` returned all organizations for a super administrator, only active administered organizations for an organization administrator, and an empty list for other staff
- `GET /api/platform/citizens` returned citizen profiles; `PUT /api/platform/citizens/{userId}` updated the profile and account name, and duplicate NID values were rejected with `409`
- `POST /api/organizations/{orgId}/memberships/{membershipId}/transfer` moved a membership to an active destination organization, deactivated the source membership, and recorded `membership.transferred`
- A simulated email transport failure left the invitation valid with `deliveryFailed: true`; `Resend` cleared the failure state after delivery recovered

### Invitation-with-phone path

- Invitation carrying `+9779800001234` created (`201`, response carries the number but no token)
- Emailed link taken from the API log; body repeated the number ("CivicFlow will contact you on +9779800001234 about the cases you work on")
- `POST /api/auth/invitations/lookup` returned the number
- `POST /api/auth/invitations/accept` created the account
- Resulting staff record carried the invited number
- Account signed in on the organization portal (`200`, `roles: [staff]`)
- Invitation with `OrganizationAdmin` role produced an org admin who:
  - Updated a staff phone number (`200`, new number returned and persisted)
  - Received `400` for an invalid number ("A phone number must be between 6 and 30 characters")
  - Received `403` from `/api/platform/users`, `/api/platform/audit`, and `/api/platform/overview`
  - Organization-scoped audit answered `200` and showed the `staff.phone_updated` event with the previous and new number
- Verification accounts deactivated afterwards; audit trail left intact

---

### Browser end-to-end verification

The Playwright suite in `civicflow-web/e2e` runs five serial Chromium journeys against a disposable PostgreSQL database:

1. **Citizen submission and display** — a citizen signs in through the real cookie/CSRF flow, submits a request with an attachment to an active organization, sees the reference number, and opens the tracking detail with its status timeline and downloadable document.
2. **Staff case work** — an organization administrator signs in, finds the case, records a citizen-visible status explanation, observes an invalid transition error, posts a public update, posts a confidential internal note, requests information, uploads a citizen-visible and an internal document, and verifies the staff-visible history.
3. **Citizen confidentiality and response** — the citizen sees the public update, visible timeline, and visible document, never sees the internal note or internal document, downloads the visible attachment, answers the information request, observes the return to `UnderReview`, and exercises search, status filtering, sorting, and pagination across 12 generated requests.
4. **Cross-citizen isolation and session expiry** — a second citizen cannot search, read, or mutate the first citizen's case through the API (all return `404`), cannot call a staff endpoint (`403`), and is returned to the landing page after the session is invalidated (`401`).
5. **CSRF rejection** — a mutation with an invalid anti-forgery token returns `400` with the stable `antiforgery_invalid` code.

Result of the recorded run: **5 passed** (5.9 seconds). The browser suite seeds confirmed users, a demo organization, staff membership, and citizen profiles directly into the disposable database; it does not use production data or a real mail provider.

---

## Required Local Verification Sequence

Use the disposable local fixtures and local-only credentials:

```bash
cp .env.example .env
# Replace placeholders in .env; never commit .env.
docker compose up -d postgres

ConnectionStrings__CivicFlow="Host=localhost;Port=5432;Database=civicflow;Username=civicflow_migrator;Password=<local-migrator-password>" \
dotnet ef database update \
  --project PublicServiceCaseManagement.Api \
  --startup-project PublicServiceCaseManagement.Api

dotnet test PublicServiceCaseManagement.sln \
  --configuration Release \
  --no-restore
```

The Compose fixture initializes separate `civicflow_migrator` and `civicflow_runtime` PostgreSQL roles. Configure the API connection with the runtime role and use the migration role only for `dotnet ef database update`.

### Optional PostgreSQL RLS tests

```bash
export CIVICFLOW_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55433;Database=civicflow;Username=<admin>;Password=<local-admin-password>'
export CIVICFLOW_TEST_POSTGRES_RUNTIME='Host=127.0.0.1;Port=55433;Database=civicflow;Username=<runtime>;Password=<local-runtime-password>'
dotnet test PublicServiceCaseManagement.Tests/PublicServiceCaseManagement.Tests.csproj \
  --configuration Release --filter FullyQualifiedName~CitizenCaseTrackingRlsTests
```

The admin connection must be a superuser so it can seed rows while RLS is bypassed; the runtime connection must be a non-superuser role without `BYPASSRLS`. Point both at a disposable database only.

### Browser E2E tests

The Playwright configuration starts a Release API on `http://localhost:5001` and Vite on `http://localhost:5173`, or reuses already-running servers outside CI. The API must use the runtime database role with `BusinessEndpoints__Enabled=true`, `Security__RequireAntiforgery=true`, a writable `FileStorage__RootPath`, and a frontend origin allowed by CORS. The E2E global setup requires either `CIVICFLOW_E2E_DATABASE_URL` (PostgreSQL URL) or `CIVICFLOW_TEST_POSTGRES_ADMIN` (ADO.NET connection string) for its disposable database.

```bash
cd civicflow-web
npm ci
npx playwright install chromium chromium-headless-shell
export CIVICFLOW_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55433;Database=civicflow;Username=<admin>;Password=<local-admin-password>'
npm run test:e2e
```

`CIVICFLOW_E2E_API_URL` and `CIVICFLOW_E2E_WEB_URL` override the API and frontend URLs when the defaults differ. The suite seeds its own confirmed accounts and organization, uses a fixed disposable-only password, and must never be pointed at a persistent or production database. Generated reports and failure traces are gitignored.

Automated authentication tests replace `IEmailSender` with an in-memory capture sink. For a manual local or production run, configure `Smtp:Host`, `Smtp:Port`, `Smtp:FromAddress`, and `Smtp:UseSsl=true`; SMTP credentials are optional only when the relay uses network-level authentication. Outside Development, the API validates SMTP configuration at startup. The Development placeholder logs only that delivery is unavailable and throws; it never logs recipients, subjects, bodies, or action tokens.

> Do not run the migration command against a persistent or production database. Do not put database passwords, email credentials, or any tokens in the repository, test output, or documentation.

---

## Required Acceptance Evidence

- Registration cannot sign in until the email is confirmed; the confirmation link enables sign-in.
- Wrong credentials are rejected with `401`; unconfirmed email returns `403`; lockout returns `423`.
- Registration and password-recovery responses do not disclose whether an email exists.
- Sign-in failures return a machine-readable `code` (`email_not_confirmed`, `invalid_credentials`).
- Sign-in is portal-bound: a citizen account is accepted only on the citizen portal, staff accounts only on the organization portal, and platform super administrators only on the platform portal. A wrong-portal attempt returns `403` with `not_a_citizen_account` / `not_an_organization_account` / `use_platform_admin_portal` / `not_a_platform_admin` and leaves no session behind.
- Staff onboarding never transmits a password: an administrator sends an invitation, the API stores only a token hash and emails a single-use link, the invitee sets their own password, and the link cannot be redeemed twice, after expiry, or after revocation.
- The platform super administrator bootstrap fails fast on a partial or invalid configuration and never silently resets an existing password.
- Unsafe requests without a valid CSRF token are rejected; a valid token is accepted.
- A personal access token is shown once, stored only as a hash, authenticates as a bearer credential, carries the owner's roles, is rejected after revocation, and never grants more than the owning account's permissions.
- Staff/organization endpoints require the `staff` role; citizens and (where the policy is staff-only) platform super administrators receive `403`.
- An account cannot be granted `superadmin` through an invitation or through any public endpoint.
- Every authentication, account, invitation, membership, organization, staff, case, and access-token change is recorded in the audit trail with actor, organization, and detail; a platform administrator can read the platform-wide trail and an organization administrator can read only their own organization's trail.
- Deleting an organization is refused while it holds cases (`409`), succeeds otherwise (`204`), removes the organization's memberships, staff records, and pending invitations, and is refused without the super-administrator role (`403`).
- An optional staff phone number is validated, collected with the invitation, stored on a newly accepted account and staff record, never overwrites a number an existing account already had, and can be corrected by an organization administrator.
- Active application user, membership, and organization are required.
- A citizen list/detail request is owner-scoped: the server derives the citizen identity from the session and another citizen's case returns `404` without an existence disclosure.
- A citizen sees every status transition in the timeline but only explanations, updates, and documents marked citizen-visible; internal notes and internal-only documents never appear in a citizen response.
- An information request moves the case to `AwaitingInformation`; the citizen's response adds a visible timeline entry, returns the case to `UnderReview`, and notifies the assigned staff member.
- Citizen tracking lists are paged and support search, status filtering, and deterministic sorting.
- A browser journey verifies those flows through the real cookie/CSRF session, including session expiry and CSRF rejection.
- Cross-organization case and document access is denied without data disclosure.
- Case list/detail/update/delete operations remain organization scoped; delete confirmation and administrator authorization are independently asserted.
- PostgreSQL migrations apply to a clean disposable database using `civicflow_migrator`.
- Runtime database role can read application tables but cannot create schema objects or bypass the tested role boundary.
- Migration role is not used by the running API.
- Production staff/organization endpoint behavior is verified with `BusinessEndpoints:Enabled` both `false` and `true`; citizen self-service routes stay available in both modes.

---

## Port Note

The API listens on `http://localhost:5000` by default, but macOS's AirPlay Receiver can occupy that port and make the API fail to start. When that happens, run the API with `--urls http://localhost:5001` and point the frontend at it with `VITE_API_BASE_URL=http://localhost:5001` in `civicflow-web/.env.local` (restart the Vite dev server afterwards). The frontend origin (`http://localhost:5173`) must stay in the API's `Cors:AllowedOrigins`; the API port does not affect CORS.
