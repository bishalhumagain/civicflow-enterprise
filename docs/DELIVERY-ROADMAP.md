# CivicFlow Enterprise Delivery Roadmap

This is the implementation-first roadmap. Work proceeds in dependency order, with security and a demonstrable end-to-end staff journey ahead of cosmetic work. It describes the repository as it exists today; unverified integrations are not presented as complete.

For an overview of the project's capabilities, see the [README](../README.md). For verification evidence, see [Integration Verification](INTEGRATION-VERIFICATION.md).

---

## Table of Contents

- [Current Baseline](#current-baseline)
- [Active Delivery Sequence](#active-delivery-sequence)
- [External Prerequisites](#external-prerequisites)
- [Deferred Testing](#deferred-testing)
- [Working Rules](#working-rules)

---

## Current Baseline

### Complete and verified

**Backend infrastructure:**
- .NET 10 ASP.NET Core API with EF Core/PostgreSQL persistence and 27 migrations
- Case creation, organization-scoped listing/detail access, status history, assignment history, citizen-visible updates, information requests, and audit attribution
- Database-backed application identity, active organization membership, active organization checks, and explicit role rules
- SuperAdmin cross-organization membership administration without automatic case access
- Explicit `BusinessEndpoints:Enabled` gate; production business endpoints remain disabled by default, while citizen self-service routes stay available
- Case document metadata and local file-storage abstraction with filename/path, size, content-type, and hash validation; uploads accept any file type up to 10 MB
- Citizen case intake and full owner-scoped tracking: active organization selection, session-derived submitter attribution, optional arbitrary file attachments, paged/searchable/sortable own-case list, detail with citizen-visible timeline and documents, information requests and responses, and routing of the submitted case to an organization administrator for triage. Status changes notify the submitter and assigned staff; citizen-visible updates and information requests notify the submitter; citizen responses notify the assigned staff account
- Per-case assignment roles (CaseWorker/Viewer) persisted in cases and assignment history
- Organization administrators can invite staff but cannot grant OrganizationAdmin; platform administrators retain that provisioning privilege
- xUnit domain, service, authorization, tenant-boundary, production-gate, notification-routing, and document integration tests (492 tests; 489 run by default and 3 opt-in PostgreSQL RLS tests)
- Opt-in PostgreSQL RLS tests that apply the real migrations and probe tenant and citizen ownership policies with the runtime role
- Nine Playwright/Chromium browser journeys covering citizen submission/tracking, staff updates and information requests, confidentiality, pagination, cross-citizen isolation, session expiry, CSRF rejection, registration and intake validation, and password-recovery validation
- Disposable Compose fixtures for PostgreSQL roles

**Authentication & identity:**
- ASP.NET Core Identity with first-party cookie-based authentication and CSRF protection
- Portal-scoped authentication: three exclusive sign-in endpoints (citizen, organization, platform administration), each bound to the roles it serves, with wrong-portal attempts rejected and the issued session removed. Covered by integration tests and an over-HTTP role matrix
- Personal access tokens for non-browser API clients: create/list/revoke endpoints, SHA-256 hash at rest, one-time plaintext, optional expiry, per-token revocation, and bearer authentication that resolves the same Identity roles and policies as the cookie session. Covered by integration tests
- Invitation-based staff onboarding: single-use, expiring invitation links (only a token hash is stored), self-selected password at redemption, atomic account/membership/staff creation, resend and revoke. Covered by integration tests and an over-HTTP run
- Hardened Super Admin bootstrap: fail-fast on a partially configured or policy-violating seed, repair of an existing platform account, and no silent password reset (an explicit `SeedAdmin:ResetPassword` switch is required). Covered by integration tests

**Frontend:**
- React/TypeScript dashboard with first-party cookie-based authentication and CSRF protection
- Role-specific frontend workspaces: a citizen dashboard for citizen accounts, the organization workspace for staff accounts, and platform administration for platform super administrators, with navigation and data requests derived from the roles the API reports

**Audit & compliance:**
- Persisted audit trail: every authentication, account, invitation, membership, organization, staff, case, and access-token event is written to `audit_events` with actor, organization, entity, and detail. Writes are asynchronous and fail-safe. Covered by unit tests and an over-HTTP run
- Audit read surfaces: platform-wide audit for super administrators, an organization-scoped activity log for organization administrators, and a filterable (category, action, text, date range) paged UI

**Platform administration:**
- Platform directory: overview counters, every account with roles and memberships, and every staff membership, built on memberships so the organization RLS scope does not hide rows. Covered by unit tests and an over-HTTP run
- Organization administration for super administrators: rename, activate/deactivate, and delete, with deletion refused while the organization holds cases and otherwise removing memberships, staff records, and pending invitations inside the organization transaction context

**Dashboard & error tracking:**
- Platform-wide and organization-scoped dashboard statistics: total cases, per-status counts, per-priority counts, weekly/monthly created/resolved trends
- Application error tracking: unhandled exceptions persisted to `application_errors` with full context (stack trace, source, request path/method, user, org, additional data), paginated retrieval, and mark-as-handled by super administrators

**Unified profile:**
- Unified self-service profile for citizens and staff (`GET/PUT /api/profile/me`), replacing the separate citizen-only profile flow. Determines account type from role claims, resolves organization for staff, updates citizen profile or staff record, validates unique identifiers, and audits changes. Covered by integration tests

**Phone numbers:**
- Optional phone numbers on staff and invitations: collected with the invitation, repeated in the invitation email, stored on a newly accepted account and staff record, filled in for an existing account only when it has none, and correctable by organization administrators

### Implemented but limited

| Area | Limitation |
|---|---|
| **Frontend live data** | The frontend loads live cases and staff, pages/searches/filters cases, creates staff cases in the authenticated organization, changes status with citizen-visible explanations, reads status and assignment history, manages public updates/internal notes/information requests, sets document visibility, submits assignments with per-case roles, browses/administers the active staff directory, manages invitations, and provides citizen submission plus full request tracking. Demo data remains available without live configuration. |
| **File storage** | Local file storage is suitable for development only. Object storage, malware scanning, retention, deletion, and download audit policy are not production-ready. |
| **Test infrastructure** | Most automated API tests use EF Core InMemory and synthetic authentication; a subset exercises the real Identity cookie flow. PostgreSQL RLS tests and Playwright browser journeys exist but are opt-in and require a disposable database; they are not yet wired into CI. |
| **PAT scopes** | Personal access tokens are deliberately scope-less: a token inherits the full role set of the account that created it. Fine-grained token scopes, per-token expiry policies enforced at a stricter default, and token listing visibility for administrators are not implemented. |
| **Browser-level verification** | A nine-test Playwright suite now verifies the citizen tracking and staff update journeys, cross-citizen isolation, session expiry, CSRF rejection, registration, password-recovery, and intake field validation, normalized payloads, and click stability in Chromium against disposable PostgreSQL. Portal sign-in, invitation redemption, platform administration, and the PAT UI still rely on integration tests, HTTP runs, TypeScript compilation, lint, and a production build. |
| **Audit trail** | Append-only and read-only through the API; retention, export, and alerting policies are not implemented. The audit event table is intentionally not RLS-protected (platform reads are gated by the super-administrator check, organization reads are scoped in the query service). |
| **Multi-organization** | An account that holds both a citizen and a staff role may use both of those portals; a super administrator may only use the platform portal. There is no per-organization sign-in selection yet, so a multi-organization staff member still picks the active organization through `X-Organization-Id`. |
| **Email delivery** | Invitation delivery uses the development `LoggingEmailSender`; a real SMTP/provider implementation is still required for production delivery, and the emailed link is logged at `Information` level locally. |

### Unstarted or blocked by a security design dependency

- CI wiring for the opt-in PostgreSQL RLS suite, Playwright browser suite, and a production security review
- Production-grade object storage and malware-scanning integration
- Production endpoint enablement, deployment, backups, observability, and operational recovery

---

## Active Delivery Sequence

### 1. Finish the staff product loop

**Status:** Implemented for the current API workflow; browser verification remains.

The API contracts support the core staff workflow and the frontend now provides explicit authenticated/unauthorized states, API error handling, case detail refresh, status and assignment history, assignment, document upload/download controls, paging, staff and membership administration views, and a separated non-submitting citizen intake foundation. Keep the UI small and operational; do not expand into reporting or visual polish.

**Acceptance:**
- A configured staff user can sign in, list cases, open a case, inspect status history, create a case, change an allowed status, and assign an active staff member.
- A viewer or unauthorized session receives a clear denied state and cannot be made privileged by frontend configuration.
- Demo mode never implies that a live API request succeeded.

### 1b. Complete the invitation lifecycle in production delivery

**Status:** Implemented and verified over HTTP; production email delivery remains.

The invitation path removes administrator-handled passwords entirely: the API emails a single-use link, the invitee chooses their own password, and acceptance creates or links the account, membership, and staff record atomically. Still required for production: a real `IEmailSender` implementation, a documented invitation expiry policy per organization, and operational handling for expired or revoked invitations (for example a verified resend path for the invitee rather than only for administrators).

**Acceptance:**
- No invitation is redeemable twice, after expiry, or after revocation.
- A redeeming account can only obtain the role the invitation granted; `superadmin` is never invitable.
- A replayed or leaked token discloses nothing about the invitee beyond the invitation itself.
- Production email delivery and bounce handling are exercised against a real provider.

### 2. Establish and verify the database tenant boundary

**Status:** Implemented against the disposable PostgreSQL stack; an opt-in automated suite now covers the tenant and citizen tracking policies. CI wiring remains.

The API now derives organization context only after authentication and active membership resolution, sets it transaction-locally, cleans it up through request commit/rollback, and enables forced RLS for tenant-owned operational tables. Citizens use a separate transaction-local owner context. `CitizenCaseTrackingRlsTests` converts the disposable checks into repeatable opt-in integration tests; wiring them into CI and reviewing whether identity/membership tables require a separate policy design remain. See [RLS-DESIGN.md](RLS-DESIGN.md).

**Acceptance:**
- Missing context denies access.
- Same-organization reads and permitted writes succeed.
- Cross-organization reads and writes return no protected data.
- Context cannot be selected by a caller-controlled header without server-side membership validation.
- Runtime role cannot alter schemas, policies, or bypass RLS.
- PostgreSQL tests cover connection reuse and transaction cleanup.

### 3. Convert live security probes into repeatable integration tests

**Status:** Opt-in PostgreSQL harness implemented for tenant/citizen tracking; CI wiring and a broader real-login matrix remain.

The opt-in harness uses disposable services or clearly configured external services, and credentials stay outside the repository. It covers the tenant boundary and citizen ownership policies through the real runtime role. The real Identity auth lifecycle and CSRF flows are already covered by automated integration tests. Remaining: wire the harness into CI and extend it to real-login lockout/session-revocation permutations where practical.

**Acceptance:**
- At least one valid real staff login flow is automated.
- Unconfirmed-email, wrong-password, lockout, revoked-session, and missing/invalid CSRF rejection are automated.
- Viewer, CaseWorker, OrganizationAdmin, and SuperAdmin behavior is tested against the approved policy.
- Tests do not run against persistent or production databases.

### 4. Implement citizen intake safely

**Status:** Implemented and verified. Citizens submit to an active organization selected from the public-intake directory, and full owner-scoped tracking, information requests, responses, and visibility controls are available.

The citizen profile and submission contracts derive identity from the authenticated session. Submission supports a multipart document, records the submitter, and routes the case to an organization administrator. Tracking lists are paged/searchable/sortable; detail, updates, status history, and documents are filtered to citizen-visible rows. Remaining product work is limited to post-submission editing/attachments and production-grade storage/malware scanning.

**Acceptance:**
- Citizen users can submit only through the approved intake policy.
- Staff-only workflow mutations remain unavailable to citizen clients.
- Documents are validated, associated with the correct organization, and inaccessible across organizations.
- Object storage and malware-scanning interfaces are explicit; local storage is not represented as production storage.

### 5. Production readiness and deployment

**Status:** Unstarted.

Complete ProblemDetails/error contracts, structured logs, correlation IDs, readiness dependencies, rate limits, audit review, secret manager integration, backups/recovery procedures, production HTTPS cookie/email configuration, CI integration-test stages, deployment manifests, and a non-production demonstration.

**Acceptance:**
- Production business endpoints are enabled only after the security gates pass.
- Deployment uses separate migration/runtime database identities and no committed secrets.
- Operational failure modes and recovery steps are documented and tested where practical.

---

## External Prerequisites

Independent development should continue before requesting these actions:

1. Docker Desktop or equivalent local container runtime, required for disposable PostgreSQL tests.
2. A disposable PostgreSQL database, required for RLS and privilege tests.
3. An SMTP/provider email account, required before production email verification/reset delivery is exercised.
4. An approved object-storage and malware-scanning provider/configuration, required before production document handling.
5. Deployment environment, TLS, secret management, and monitoring decisions, required before production endpoint enablement.

No production credentials, persistent database changes, paid services, or external account actions are assumed by this roadmap.

---

## Deferred Testing

These tests remain deferred selectively; existing security assertions must not be removed or weakened.

| Test | Risk | Reason | Trigger to complete |
|---|---|---|---|
| Exhaustive real-login permutation matrix (lockout, revocation, CSRF) | Medium | Requires a repeatable disposable database and email sink | Automated real-service harness is available |
| Browser-level run of the invitation redemption flow | Medium | The Playwright runner exists but the onboarding journey is not yet automated | The `/setup-password` contract is stable and CI has a disposable database |
| Full browser automation for login and all frontend workflows | Medium | The current nine-test suite covers citizen tracking, staff updates, registration, password recovery, and intake validation, not every surface | Remaining admin/onboarding workflows are prioritized for automation |
| Visual regression and responsive screenshot suite | Low | Does not protect authorization or data correctness | Core workflow and deployment demo are stable |
| Load/stress testing | Medium | Requires representative deployment topology and traffic model | Production-like environment and performance targets exist |
| Malware scanner outage/retry/recovery scenarios | High | Provider and failure contract are not selected | Scanner adapter and operational policy are approved |
| CI execution of the PostgreSQL RLS matrix | High | The opt-in suite exists, but CI does not yet provision the disposable database and roles | A CI service container and migration step are added |

---

## Working Rules

- Inspect the current diff before editing.
- Keep each batch focused and run the narrowest relevant checks immediately.
- Never use Development configuration to grant platform or organization roles implicitly.
- Do not enable production business endpoints while the database and real-identity boundaries remain incomplete.
- Report verified, prepared, deferred, and blocked work separately.
- Update documentation when functionality changes — see [Documentation Workflow](DOCUMENTATION_WORKFLOW.md).
