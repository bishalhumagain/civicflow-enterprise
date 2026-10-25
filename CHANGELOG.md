# Changelog

## Unreleased

### Added

- Case priority is now editable end to end. `PATCH /api/cases/{id}/priority` (organization-scoped, role-aware, audited as `case.priority_changed`) validates the `CasePriority` enum and is paired with a priority selector in case details, a complete `Critical`/`High`/`Normal`/`Low` filter set, and Critical priority styling. Organization and platform statistics now count `CriticalPriorityCases` alongside the existing priority buckets, and the priority mix chart includes Critical.
- Application-wide validation, normalization, and display formatting. Shared `TextNormalization`, `PhoneNumbers`, and custom validation attributes enforce trimming, whitespace collapsing, optional-blank handling, canonical email and identifier casing, phone shape, name bounds, and control-character rejection across authentication, profiles, citizens, staff, organizations, invitations, memberships, cases, updates, and platform administration. The `AddValidationConstraints` migration adds 27 PostgreSQL CHECK constraints for the critical bounds and includes a complete `Down` path.
- Frontend shared modules `utils/validation.ts` and `utils/format.ts` mirror the server rules and provide explicit `en-GB` locale, `Asia/Kathmandu` time zone, binary file sizes, upper-case references, and an accessible `—` fallback for missing values. Registration, sign-in, password reset and setup, profile, public intake, case details, staff, invitation, platform directory, audit, job, notification, and error views now use per-field accessible errors, preserved input, first-invalid focus, and explicit handling for `400`, `401`, `403`, `429`, CSRF, and network failures. Declared `application/pdf`, `image/png`, `image/jpeg`, and `image/gif` uploads now verify their magic signature; other declared media types remain allowed by the arbitrary-file contract.
- Validation coverage: `TextNormalizationTests`, `PhoneNumbersTryNormalizeTests`, `ValidationAttributesTests`, and `InputValidationIntegrationTests` exercise missing, blank, whitespace-only, padded, over-limit, Unicode, invalid-phone, out-of-range, normalized persistence, pagination-bound, and file-signature cases. A new Playwright `input-validation.spec.ts` covers accessible registration and intake validation plus normalized case text.
- Complete citizen case tracking: an owner-scoped list with `page`, `pageSize`, `search`, `status`, and `sort`; a detail view with the citizen-visible status timeline, public updates, information requests and responses, and citizen-visible documents. Internal notes, internal-only documents, and internal status explanations are filtered in the database projection and never reach a citizen response. New routes: `POST /api/citizens/cases/{id}/responses`, `GET /api/citizens/cases/{id}/documents`, and `GET /api/citizens/cases/{id}/documents/{documentId}`.
- `CaseUpdate` activity entries (`PublicUpdate`, `InformationRequest`, `InternalNote`, `CitizenResponse`) with staff update list/create endpoints and a `visibleToCitizen` flag on document upload. An information request moves a case to `AwaitingInformation`; a citizen response returns it to `UnderReview`, writes a citizen-visible history entry, and notifies the assigned staff account.
- Notification fan-out for tracking: citizen-visible public updates and information requests notify the submitter, citizen responses notify the assigned staff account, and internal notes never notify. Administrator triage routing and assignee status notifications remain in place.
- Migrations `AddCitizenCaseTrackingRls`, `AddCitizenCaseTrackingUpdates`, and `AddCitizenTrackingRls`: the first adds the citizen owner branch for cases and status history; the latter two add the updates table and force RLS on it, with citizen owner-scoped read/write policies for updates, status history, documents, and assigned-staff resolution. The migrations include full `Down` paths that restore the prior policies.
- Opt-in PostgreSQL suite `CitizenCaseTrackingRlsTests` (3 tests) that applies the real migrations and probes citizen visibility, write restrictions, and runtime-role behavior when `CIVICFLOW_TEST_POSTGRES_ADMIN` and `CIVICFLOW_TEST_POSTGRES_RUNTIME` point at a disposable database.
- Playwright browser suite (`civicflow-web/e2e`, nine serial Chromium journeys) with deterministic PostgreSQL seeding, covering citizen submission/tracking, staff updates and document visibility, cross-citizen isolation, session expiry, pagination, CSRF rejection, and accessible registration, password-recovery, and intake validation. Added `npm run test:e2e`, `test:e2e:headed`, and `typecheck:e2e`.
- README, user manual, API authorization matrix, RLS design, integration verification, and delivery roadmap now document the verified tracking behavior, test counts, commands, and remaining limitations.
- A newly submitted citizen case is triaged to an organization administrator via `CaseService.RouteToOrganizationAdministratorAsync` (called from `CitizenCasesController.Create` after intake). When an active administrator has a matching `StaffMember` row the case is assigned to the earliest such admin (deterministic by `CreatedAt`/`Id`) through the existing `AssignAsync`, which records assignment history and sends the admin a `CaseAssigned` notification; when no admin staff row exists it falls back to notifying every active `OrganizationAdmin` (idempotency key `case-triage:{caseId}:user:{userId}`) and leaves the case unassigned. Orgs with no administrator at all are a safe no-op.
- `CaseService.ChangeStatusAsync` now also notifies the assigned staff member — both `CaseWorker` and read-only `Viewer` assignees — when a case's status changes (idempotency key `case-status-staff:{historyId}:user:{userId}`), so watchers learn the status moved. Viewer read access already worked via assigned-staff matching in `GetByIdAsync`/`GetPageAsync`. Covered by new unit tests in `CaseServiceTests` and integration tests in `CaseRoutingNotificationIntegrationTests`.

- Added a platform-owned `OrganizationType` catalogue: `Organization.OrganizationTypeId` FK (Restrict delete), unique `Code`, seeded immutable `Uncategorized` type (`WellKnownOrganizationTypes.Uncategorized`), and the `AddOrganizationTypes` migration with a SQL backfill that reassigns existing rows to `Uncategorized`. Organization create/update requests accept an optional `OrganizationTypeId`; missing or inactive values fall back to `Uncategorized`. List and detail responses now carry `organizationTypeId` and `organizationTypeName`.
- Added SuperAdmin-only `OrganizationTypesController` at `/api/platform/organization-types` (GET/GET-by-id/POST/PUT/PATCH deactivate/reactivate/DELETE). `OrganizationTypeService` enforces unique codes, refuses deletion when any organization still references the type, and hard-blocks delete or deactivate on the seeded `Uncategorized` type. Five audit actions (`OrganizationTypeCreated/Updated/Activated/Deactivated/Deleted`) record every change.
- Frontend: removed the top-level *Organization types* navigation entry and restructured the *Organizations* view into three inner tabs — *Organization types* (catalogue CRUD with a protected `Uncategorized` row), *Organizations* (organization CRUD with a Type dropdown), and *Members & invitations* (organization-type + organization filter with Apply/Reset for platform admins, direct access for organization admins). The tab strip follows the design already used by the Platform directory. `OrganizationTypeAdministration.tsx` was merged into `OrganizationAdministration.tsx` and deleted.

### Fixed

- Closed remaining validation inconsistencies found in the follow-up audit. Case title and description minimums (3 and 10 characters) are now enforced by the DTOs, the `Case` domain invariant, and the new `AlignCaseTextMinimums` migration; case and citizen request contracts use the shared whitespace and trimmed-length attributes so padding cannot satisfy a minimum or bypass a maximum, and an empty organization identifier is rejected. Citizen case search collapses repeated whitespace before matching. The password-recovery form now shows associated inline email errors, moves focus to the invalid field, announces success or failure, and submits the canonical lower-case address; case edit and update forms focus the first invalid control. Regression coverage was added at the domain, API integration, and Playwright levels.
- Public-intake action buttons keep focus stable during pointer-down, so the validation message for a focused field cannot render above the button and swallow the click. Previously the first submit or Track this request activation could be lost.
- The SPA now clears the anti-forgery token cached before sign-in after a successful login, so the first mutation requests a token bound to the authenticated principal instead of replaying the anonymous token (which was rejected with `400 antiforgery_invalid`).
- `POST /api/auth/invitations/accept` no longer returns 500 on tenant tables. Redemption inserts a `StaffMember`, and `staff_members` is `FORCE ROW LEVEL SECURITY` with a WITH CHECK clause keyed off `civicflow.organization_id`. The accept path now opens the tenant transaction explicitly (`InvitationService` injects `IOrganizationTransactionContext` and calls `BeginAsync(invitation.OrganizationId, ...)` before the membership/staff writes), so PostgreSQL admits the row. Regression coverage in `InvitationApiIntegrationTests` uses a spy `IOrganizationTransactionContext` to assert the transaction is opened; the reset-password flow is covered by a paired `forgot-password`/`reset-password` test to keep it working.
- `ErrorLogger.LogErrorAsync` now detaches every tracked entity before adding the `ApplicationError` row. Previously, an unhandled exception raised after a failed `SaveChangesAsync` (for example a rejected RLS insert) left the poisoned change tracker in place; the error logger's retry replayed the same rejected write and silently swallowed the log, hiding the true cause from `application_errors`. The failure path now also surfaces through `ILogger<ErrorLogger>` so operators see it even when the row cannot be persisted.
- Signing out of one account and signing in as another no longer traps the workspace on "The anti-forgery token is missing or invalid." on the first action. The SPA's auto-recovery never fired because `looksLikeCsrfFailure` matched the string `antiforgery`/`csrf`, while the middleware returned the hyphenated message `anti-forgery`, so a stale cached token (carried across the session switch) surfaced as a hard error. The anti-forgery rejection now also emits a stable, machine-readable `code` (`AntiforgeryErrorCodes.InvalidToken` = `antiforgery_invalid`) that `AntiforgeryMiddleware` returns alongside the human message; `looksLikeCsrfFailure` keys off that code (with hyphenated/non-hyphenated text fallback), and `login()` clears any token cached from a previous user before signing in. Regression coverage in `AntiforgeryIntegrationTests` asserts the stable code and that refreshing the token and replaying the request recovers automatically.

### Security

- Focused hardening pass. Existing controls (RLS tenant isolation, per-request active-membership/role resolution, constant-time personal-access-token checks, hardened local file storage, non-leaky global error handler, gitignored secrets) were verified and left intact; the changes below close the real gaps.
- **Dynamic development ports.** The CORS policy was pinned to explicit Vite ports, so login broke the moment Vite hopped to the next free port (`5173`→`5175`+). In Development the API now trusts any first-party loopback origin (`http://localhost:<port>` and `http://127.0.0.1:<port>`) plus anything in `Cors:AllowedOrigins`; this rule is never consulted outside Development, and Production keeps the strict explicit allowlist. Cookies are host-scoped on `localhost` (shared across ports), which is why the port must not be pinned. Regression tests assert a `5175` origin is reflected with credentials and that a public origin is never reflected.
- **HSTS.** `UseHsts()` is now enabled outside Development, alongside the existing HTTPS redirection.
- **Immediate session revocation.** Identity's security stamp is revalidated on every request (`SecurityStampValidatorOptions.ValidationInterval = TimeSpan.Zero`) instead of the default 30-minute window, so a session stops the instant its stamp changes. `PlatformDirectoryService` now rotates the stamp when an account is deactivated or its platform role changes, so a disabled or demoted account loses its live session on the next request. `AuthService.ChangePasswordAsync` refreshes the acting sign-in so a self-service password change keeps the current browser usable while still revoking every other session (password reset via recovery link intentionally does not).
- **Absolute session lifetime.** New `SessionLifetimeMiddleware` enforces a hard cap (`Security:AbsoluteSessionLifetimeSeconds`, default 12 hours; `<= 0` disables) independent of the 30-minute sliding idle window, rejecting an over-aged cookie-authenticated session with `401 session_expired` and clearing it. It is deliberately separate from the cookie `OnValidatePrincipal` pipeline so it never interferes with stamp revalidation and leaves bearer/PAT requests untouched.
- **Token-minting throttle.** `POST /api/auth/tokens` now carries `[EnableRateLimiting("auth")]`. Creating a personal access token mints a long-lived bearer credential, and the endpoint previously had only `[Authorize]` with no rate limit, so a hijacked session or script could churn tokens (bounded only by the 25-active-per-user cap). Revocation is deliberately left unthrottled because blocking a legitimate user from revoking a leaked token is a worse failure mode. (`ExpiresInDays` already validated `Range(1, 3650)`.)
- New regression coverage lives in `SecurityHardeningIntegrationTests` (CORS dynamic origin + public-origin rejection, security-stamp and deactivation revocation, absolute-lifetime decision, and a token-creation burst that must succeed then yield `429`).

### Added

- Added a complete light/dark theme with a persisted, system-aware preference.
- Applied the CivicFlow teal-blue, moonstone, powder-blue, magic-mint, white-smoke, and warm-beige palette across public and authenticated surfaces.
- Centralized brand and footer components; branding and the copyright footer now use `Application:Name` exposed through the anonymous `GET /api/config/public` endpoint.
- Refined action icons, form/dropdown containment, responsive layouts, and shared status/error states.

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

### Added (background jobs, notifications, and administration)
- EF Core/PostgreSQL persistent background jobs, attempt history, transactional in-app notification outbox, lease-based hosted worker, bounded exponential retries, stale-lease recovery, retention cleanup, and SuperAdmin-only audited job inspection/retry/cancel APIs.
- Authenticated-user notification APIs and a workspace notification panel; platform administrators now have a status-filtered background-job monitor. Case submission, assignment, and status changes notify only the authenticated submitter or active assigned staff account.
- Migrations `AddPersistentBackgroundJobsAndNotifications` and `AddBackgroundJobConcurrencyVersion`, worker configuration defaults, and integration tests for outbox idempotency, notification recipient isolation/read state, job state transitions, and platform job authorization.
- Assigned CaseWorkers can now edit case title and description; only organization administrators can permanently delete cases, after explicit confirmation. Case deletion removes its assignment/status history and document metadata, records an audit event, and deletes the associated stored files.
- Organization staff can now use the activity screen without receiving a forbidden response: organization administrators retain organization-wide activity visibility, while CaseWorkers and Viewers are constrained server-side to events performed by their own authenticated account.
- Regression tests cover staff audit actor isolation, CaseWorker detail edits, Viewer read-only behavior, and organization-admin-confirmed deletion.
- Citizen intake now submits multipart case requests to an active organization selected from the authenticated citizen API, stores the submitting user, and supports an optional arbitrary file attachment (10 MB limit). Staff-created cases continue to use the authenticated organization context.
- Case assignment now records a case-specific `CaseWorker` or `Viewer` role in the case and assignment history. Organization administrators alone can assign or reassign cases; staff list and detail APIs return only cases assigned to them, and Viewer assignments are read-only. Existing assignments are migrated as `CaseWorker`.
- Authenticated organization selector: `GET /api/organizations/me` returns only the current active account's active organization memberships and their organization roles. The SPA no longer uses a build-time organization ID; it resolves memberships after session restoration and supports switching among authorized organizations.
- Organization administrator workspace membership controls for role changes and membership activation/deactivation, without the ability to grant OrganizationAdmin. Non-admin staff see a read-only staff directory; phone numbers are omitted from non-admin staff-list responses.
- Regression coverage for invitation acceptance followed by organization-admin staff/case workspace access, membership-derived organization selection, and organization dashboard cross-tenant selection denial.
- Invitation-based staff onboarding: `AccountInvitation` entity + `AddAccountInvitations` migration, `InvitationService`, and `InvitationsController` (`POST/GET /api/organizations/{organizationId}/invitations`, `POST {id}/resend`, `DELETE {id}`). Invitations store only a SHA-256 token hash, expire after `Invitations:ExpiryHours` (default 72, clamped 1–720), are single-use, and can be resent or revoked. No temporary password is generated or emailed.
- Anonymous invitation redemption endpoints: `POST /api/auth/invitations/lookup` and `POST /api/auth/invitations/accept`. Acceptance creates or links the Identity account, grants the `staff` role, creates/reactivates the membership and staff record, and requires a password chosen by the invitee (validated against the Identity password policy before anything is mutated).
- Dedicated platform-administrator portal: `POST /api/auth/login/platform-admin` and `SignInPortal.PlatformAdmin`. `superadmin` is now rejected by both the citizen and organization login endpoints (`use_platform_admin_portal`), and staff/citizens are rejected by the platform endpoint (`not_a_platform_admin`).
- Frontend: `/super-platform-admin` sign-in page (intentionally unlinked), `/setup-password` invitation-redemption page, invitation management (send/resend/revoke + status list) in the staff directory and platform organization administration, and portal-aware navigation/guards for the platform administrator workspace.
- Tests: invitation lifecycle/integration tests (hashing at rest, lookup, password setup, reuse/expiry/revocation, duplicates, membership conflicts, role escalation, cross-organization denial, existing-account linking), super-admin seeding tests (fresh seed, weak/partial configuration failure, skip, repair, password-change preservation), and cross-portal sign-in tests.
- Persisted audit trail: `AuditEvent` entity + `AddAuditTrailAndPhoneNumbers` migration, `audit_events` table (category, action, organization, actor id/email, entity type/id, details, occurred-at) with indexes on occurred-at, category+occurred-at, and organization+occurred-at. Authentication, account, invitation, membership, organization, staff, case, and access-token flows now write structured audit events; the writes are asynchronous and fail-safe, so a failing audit write never rolls back the business operation.
- Audit read API: `GET /api/platform/audit` (platform-wide, super administrators only), `GET /api/organizations/{organizationId}/audit` (scoped to one organization, for its administrators), and `GET /api/platform/audit/categories`. Queries support category, action, free-text search, and date-range filters with paging.
- Platform administration API: `GET /api/platform/overview` (account/staff/organization/invitation counters), `GET /api/platform/users` (accounts with roles, platform role, phone number, and organization memberships), and `GET /api/platform/staff` (staff memberships with organization, role, and status). The directory is membership-based and therefore unaffected by the organization row-level-security scope.
- Organization management for super administrators: `PUT /api/organizations/{id}` (rename, activate/deactivate) and `DELETE /api/organizations/{id}` (`204`, `404` when unknown, `409` while the organization still holds cases). Deletion removes the memberships, staff records, pending invitations, and the organization inside the organization's transaction context.
- Optional phone numbers on staff and invitations: `PhoneNumber` on `StaffMember` and `AccountInvitation`, accepted on invitation creation and on `PATCH /api/staff/{id}/phone` (organization administrators only). The number is validated (6–30 characters, at least 6 digits) and stored on the account and the staff record when the invitation is accepted.
- Frontend: platform directory screen (overview counters, accounts, staff memberships), platform audit-log screen, organization activity log, organization rename/activate/delete actions, phone collection on the invitation form, and inline phone display and editing in the staff directory. The shared `AuditLog` component filters by category, action, free text, and date range and pages through the results.
- Tests: platform administration integration tests (organization deletion `204`/`404`/`409` and the forbidden case, platform directory and overview, platform and organization audit endpoints, staff phone update), plus audit persistence/querying, organization deletion, platform directory, and phone validation unit tests.

### Fixed
- One-time verification, reset, and invitation links are no longer written to application logs. The Development email placeholder reports non-delivery and fails explicitly; non-Development uses a TLS-required SMTP sender configured through runtime settings.
- Application error listing now bounds page/pageSize inputs, honors request cancellation, and returns `404` when marking an unknown error handled; identifier routes require GUIDs.
- Organization administrators can invite staff with the Staff portal role and cannot invite, add, or promote another organization administrator. Platform administrators retain organization-admin provisioning.
- Organization workspace `403` caused by the frontend sending a static `VITE_CIVICFLOW_ORGANIZATION_ID` that could differ from an invitee's organization. The server continues to validate `X-Organization-Id` against active membership; the frontend now resolves valid organizations from the authenticated session.
- Organization dashboard statistics now use the server-resolved membership context and reject a supplied organization ID that does not match it.
- When an invitation reactivates an inactive membership, the membership is assigned the role carried by the new invitation instead of silently retaining its former role.
- Super administrator bootstrap: `IdentityDataSeeder` now fails startup loudly on a partially configured or policy-violating seed instead of leaving an unusable account, and repairs an existing account's platform role, confirmation state, active state, normalized email, username, and role membership. `SeedAdmin:ResetPassword=true` provides an explicit one-time password sync; otherwise an existing password is never overwritten.
- `CapturingEmailSender.ExtractToken` stops at whitespace/newlines, so invitation links followed by a newline parse correctly in tests.
- Invitation acceptance now propagates the collected phone number to the account: a new account receives it, and an existing account only has it filled in when the account has no number of its own — a number the person set themselves is never overwritten. The invitation email repeats the collected number.

### Changed
- Portal-scoped sign-in: `POST /api/auth/login` now serves the citizen portal and the new `POST /api/auth/login/organization` serves the organization portal. The API binds each attempt to one portal, so the same credentials can never open both dashboards. Supported by the new `SignInPortal`, `LoginFailureReason`, and `LoginOutcome` contracts.
- Citizen dashboard (`CitizenDashboard`) in the frontend with self-service actions for public intake, the citizen's own profile, and access tokens.
- Role-scoped sign-in integration tests covering citizen/staff/superadmin acceptance on the matching portal, wrong-portal rejection with a stable code, session cleanup after a rejected attempt, and role-gated endpoint access.
- Personal access tokens (PAT) subsystem: `PersonalAccessToken` entity + `AddPersonalAccessTokens` migration, `PersonalAccessTokenService`, and `PersonalAccessTokensController` (`GET/POST/DELETE /api/auth/tokens`). Tokens are shown once, stored only as a SHA-256 hash, support an optional expiry, and can be revoked individually.
- `PersonalAccessTokenAuthenticationHandler` plus a `CivicFlowSmart` policy scheme so `Authorization: Bearer civicflow_pat_…` authenticates alongside the session cookie and resolves to the same Identity roles and authorization policies.
- Access-token management UI (`AccessTokensPage`) in the workspace, and `list/create/revoke` client functions in `civicflow-web/src/auth.ts`.
- Machine-readable error codes on sign-in failures (`email_not_confirmed`, `invalid_credentials`) and a "resend the verification email" action in the sign-in form.

### Fixed
- Staff account provisioning (`POST /api/staff/accounts`) now works end to end. The request contract carried its validation metadata on the generated record properties instead of the primary constructor parameters, which made ASP.NET Core reject the request with a `500` before the action ran; the metadata now sits on the constructor parameters like every other request record.
- Provisioned staff accounts are confirmed up front, so the temporary password can be used to sign in on the organization portal right away instead of being blocked by the email confirmation rule.
- Staff account provisioning no longer opens a second transaction on the same connection. The organization transaction owned by the request pipeline is used instead, which keeps the write inside the row-level-security scope of the organization.

### Changed
- `IAuthService.LoginAsync` takes the target portal and returns a `LoginOutcome` (replacing `SignInResult`), so the controller can answer with a stable status code and machine-readable code. A successful login response now reports the `portal` and the session `roles`.
- Frontend workspace restructured around the roles the API reports: the navigation entries, the active view, and the data requests are derived from them, a citizen session opens the citizen dashboard and a staff/superadmin session opens the organization workspace, and sign-in redirects to the dedicated `/workspace` route.
- `LoggingEmailSender` now logs the message body — and therefore the email-confirmation and password-reset links — at `Information` level instead of `Debug`, so the links are visible in the development log with the default configuration.
- Personal access token creation and revocation are recorded through the audit log as `PRIVILEGE_CHANGE` events, so they are visible under the default audit log level.

### Security
- A wrong-portal sign-in attempt is rejected with `403` (`not_a_citizen_account` / `not_an_organization_account`), and the session cookie that the password check had already issued is removed again, so a rejected attempt never leaves a usable session behind.
- The frontend re-validates an issued session against the portal it signed in through and signs out again when the roles do not match, so a dashboard is never rendered for the wrong portal.

### Added (auth)
- ASP.NET Core Identity authentication replacing Keycloak: `ApplicationUser : IdentityUser<Guid>`, `CivicFlowDbContext : IdentityDbContext`, cookie sessions, and role-based authorization
- Auth API (`/api/auth`): `csrf`, `register`, `login`, `logout`, `verify-email`, `resend-verification`, `forgot-password`, `reset-password`, `change-password`, `me`
- Anti-forgery (CSRF) protection for unsafe `/api/*` methods with the `X-CSRF-TOKEN` header
- Account lifecycle: email confirmation, password recovery/change, lockout (5 attempts / 15 minutes), and rate limiting on auth/password endpoints
- Audit logging (`AUTH_SUCCESS`/`AUTH_FAILURE`/`ACCOUNT_LIFECYCLE`/`PRIVILEGE_CHANGE`), security headers middleware, and enumeration-safe auth responses
- Staff account provisioning (`POST /api/staff/accounts`) creating the Identity user, role, membership, and staff record atomically
- Frontend cookie-authentication flows and pages (citizen login/register, verify email, forgot/reset password, staff login)
- Identity migration (`MigrateToAspNetCoreIdentity`) with data backfill for previously Keycloak-managed users
- Auth lifecycle, anti-forgery, and personal access token integration tests

### Removed
- `POST /api/staff/accounts`, `IStaffAccountService`, `StaffAccountService`, and `ProvisionStaffAccountContracts`. Staff accounts are now created only by redeeming an invitation, so no administrator ever handles or transmits another person's password.
- The tracked `PublicServiceCaseManagement.Api/appsettings.json`. The file is now gitignored (keep your local copy) and `appsettings.template.json` documents the configuration keys with placeholders.
- Keycloak and all OIDC/JWT integration (backend packages, `infra/keycloak`, `scripts/verify-keycloak-token.sh`, compose service, `VITE_KEYCLOAK_*` config, and `oidc-client-ts` frontend dependency)

### Added (prior)
- Citizen self-registration/profile: `CitizenProfile` domain entity with EF Core migration (`AddCitizenProfiles`)
- Citizen API endpoints (`GET/POST/PUT /api/citizens/me`) secured by the `CitizenClient` policy
- Citizen profile UI page and navigation entry, integrated with the citizen portal flow
- `civicflow-web/src/api/citizens.ts` API client using the authenticated cookie session
- Separate **Sign in** and **Register** actions in the Citizen Portal
- Dedicated staff/platform sign-in page served at `/admin` (separate URL, no longer part of the public landing page)
- Lightweight path routing with citizen entry points `/citizen/login` and `/citizen/register`
- Citizen profile domain tests (validation, trimming, uniqueness constraints) and API integration tests (auth policy, create/update, duplicates, business-endpoint gating)
- Professional documentation system (README, USER_MANUAL, documentation workflow)
- Comprehensive repository audit and verified documentation
- Toast notification system for user feedback
- In-app branded login form (Government Officer flow)
- Responsive UI improvements
- Modular frontend architecture with extracted components and pages

### Changed
- Refactored `App.tsx` into modular components and pages
- Improved auth flow UX while maintaining server-authoritative security
- Enhanced landing page with clear journey separation
- `CitizensController` resolves the caller via `IAuthenticatedOrganizationResolver` (async, no sync-over-async) and enforces business-endpoint gating
- Citizen profile page surfaces explicit sign-in and not-authorized states
- Landing page reduced to two public portals (Citizen, Organization); platform administration moved behind the `/admin` URL

### Security
- Citizen endpoints never accept a client-supplied user ID; the profile is keyed to the authenticated Identity user
- Sessions are HttpOnly cookies with CSRF protection; no tokens or signing keys are exposed to the browser

### Fixed
- Lint and build issues (unused variables, Fast Refresh compatibility)
- TypeScript compilation errors

### Documentation
- Complete README rewrite with verified architecture and setup
- New User Manual with end-user workflows
- Documentation workflow and maintenance process

## [0.1.0] - 2026-10-21

### Added (Initial)
- .NET 10 ASP.NET Core Web API
- PostgreSQL 17 with EF Core migrations
- React 19 + TypeScript + Vite frontend
- Organization-scoped case management
- Cases, Staff, Organizations, Memberships features
- Document upload/download with local storage
- Docker Compose setup (Postgres)
- Integration and unit tests (xUnit)
