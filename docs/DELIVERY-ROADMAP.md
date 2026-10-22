# CivicFlow Enterprise Delivery Roadmap

This is the implementation-first roadmap. Work proceeds in dependency order, with security and a demonstrable end-to-end staff journey ahead of cosmetic work. It describes the repository as it exists today; unverified integrations are not presented as complete.

## Current baseline

### Complete and verified

- .NET 10 ASP.NET Core API with EF Core/PostgreSQL persistence and migrations.
- Case creation, organization-scoped listing/detail access, status history, assignment history, and audit attribution.
- Database-backed application identity, active organization membership, active organization checks, and explicit role rules.
- SuperAdmin cross-organization membership administration without automatic case access.
- Explicit `BusinessEndpoints:Enabled` gate; production business endpoints remain disabled by default.
- Case document metadata and local file-storage abstraction with filename/path, size, content-type, and hash validation.
- xUnit domain, service, authorization, tenant-boundary, production-gate, and document integration tests.
- Disposable Compose fixtures for PostgreSQL roles.
- React/TypeScript dashboard with first-party cookie-based authentication (ASP.NET Core Identity) and CSRF protection.
- Personal access tokens for non-browser API clients: create/list/revoke endpoints, SHA-256 hash at rest, one-time plaintext, optional expiry, per-token revocation, and bearer authentication that resolves the same Identity roles and policies as the cookie session. Covered by integration tests.
- Portal-scoped authentication: three exclusive sign-in endpoints (citizen, organization, platform administration), each bound to the roles it serves, with wrong-portal attempts rejected and the issued session removed. Covered by integration tests and an over-HTTP role matrix.
- Role-specific frontend workspaces: a citizen dashboard for citizen accounts, the organization workspace for staff accounts, and platform administration for platform super administrators, with navigation and data requests derived from the roles the API reports.
- Invitation-based staff onboarding: single-use, expiring invitation links (only a token hash is stored), self-selected password at redemption, atomic account/membership/staff creation, resend and revoke. Covered by integration tests and an over-HTTP run.
- Hardened Super Admin bootstrap: fail-fast on a partially configured or policy-violating seed, repair of an existing platform account, and no silent password reset (an explicit `SeedAdmin:ResetPassword` switch is required). Covered by integration tests.
- Persisted audit trail: every authentication, account, invitation, membership, organization, staff, case, and access-token event is written to `audit_events` with actor, organization, entity, and detail. Writes are asynchronous and fail-safe. Covered by unit tests and an over-HTTP run.
- Audit read surfaces: platform-wide audit for super administrators, an organization-scoped activity log for organization administrators, and a filterable (category, action, text, date range) paged UI.
- Platform directory: overview counters, every account with roles and memberships, and every staff membership, built on memberships so the organization RLS scope does not hide rows. Covered by unit tests and an over-HTTP run.
- Organization administration for super administrators: rename, activate/deactivate, and delete, with deletion refused while the organization holds cases and otherwise removing memberships, staff records, and pending invitations inside the organization transaction context.
- Optional phone numbers on staff and invitations: collected with the invitation, repeated in the invitation email, stored on a newly accepted account and staff record, filled in for an existing account only when it has none, and correctable by organization administrators.

### Implemented but limited

- The frontend now loads live cases and staff, pages/searches/filters cases, creates cases, changes status, reads status and assignment history, submits assignments, browses/administers the active staff directory, manages invitations (send/resend/revoke), and integrates organization/membership administration when configured with a valid authenticated session. Demo data remains available without live configuration.
- Local file storage is suitable for development only. Object storage, malware scanning, retention, deletion, and download audit policy are not production-ready.
- Most automated API tests use EF Core InMemory and synthetic authentication; a subset exercises the real Identity cookie flow. Live PostgreSQL checks are currently disposable manual probes rather than CI integration tests.

### Implemented but limited (continued)

- Personal access tokens are deliberately scope-less: a token inherits the full role set of the account that created it. Fine-grained token scopes, per-token expiry policies enforced at a stricter default, and token listing visibility for administrators are not implemented.
- The personal access token feature has no browser-level end-to-end run in this environment; the backend flow was verified over HTTP against a local PostgreSQL instance, and the UI is covered by TypeScript compilation and lint only.
- The portal-scoped sign-in, the invitation redemption flow, and the role-specific workspaces have no browser-level end-to-end run in this environment; the backend behaviour was verified over HTTP against a local PostgreSQL instance, and the UI is covered by TypeScript compilation, lint, and a production build only.
- The platform administration surface (organization create/rename/delete, platform directory, platform and organization audit logs, staff phone update) has no browser-level end-to-end run either; it was verified over HTTP against a local PostgreSQL instance and covered by integration tests, while the UI is covered by TypeScript compilation, lint, and a production build only.
- The audit trail is append-only and read-only through the API; retention, export, and alerting policies are not implemented, and the audit event table is intentionally not row-level-security protected (platform reads are gated by the super-administrator check, organization reads are scoped in the query service).
- An account that holds both a citizen and a staff role may use both of those portals; a super administrator may only use the platform portal, because organization and platform administration are separated. There is no per-organization sign-in selection yet, so a multi-organization staff member still picks the active organization through `X-Organization-Id`.
- Invitation delivery uses the development `LoggingEmailSender`; a real SMTP/provider implementation is still required for production delivery, and the emailed link is therefore logged at `Information` level locally.

### Unstarted or blocked by a security design dependency

- Repeatable PostgreSQL RLS/privilege integration tests and production security review.
- Public citizen intake and the citizen-to-organization policy. A non-submitting public-intake UI foundation now exists, but it deliberately does not call staff endpoints.
- Production-grade object storage and malware-scanning integration.
- Production endpoint enablement, deployment, backups, observability, and operational recovery.

## Active delivery sequence

### 1. Finish the staff product loop

Status: implemented for the current API workflow; browser verification remains.

The API contracts support the core staff workflow and the frontend now provides explicit authenticated/unauthorized states, API error handling, case detail refresh, status and assignment history, assignment, document upload/download controls, paging, staff and membership administration views, and a separated non-submitting citizen intake foundation. Keep the UI small and operational; do not expand into reporting or visual polish.

Acceptance:

- A configured staff user can sign in, list cases, open a case, inspect status history, create a case, change an allowed status, and assign an active staff member.
- A viewer or unauthorized session receives a clear denied state and cannot be made privileged by frontend configuration.
- Demo mode never implies that a live API request succeeded.

### 1b. Complete the invitation lifecycle in production delivery

Status: implemented and verified over HTTP; production email delivery remains.

The invitation path removes administrator-handled passwords entirely: the API emails a single-use link, the invitee chooses their own password, and acceptance creates or links the account, membership, and staff record atomically. Still required for production: a real `IEmailSender` implementation, a documented invitation expiry policy per organization, and operational handling for expired or revoked invitations (for example a verified resend path for the invitee rather than only for administrators).

Acceptance:

- No invitation is redeemable twice, after expiry, or after revocation.
- A redeeming account can only obtain the role the invitation granted; `superadmin` is never invitable.
- A replayed or leaked token discloses nothing about the invitee beyond the invitation itself.
- Production email delivery and bounce handling are exercised against a real provider.

### 2. Establish and verify the database tenant boundary

Status: implemented against the disposable PostgreSQL stack; automated coverage remains.

The API now derives organization context only after authentication and active membership resolution, sets it transaction-locally, cleans it up through request commit/rollback, and enables forced RLS for tenant-owned operational tables. Convert the disposable checks into repeatable integration tests and review whether identity/membership tables require a separate policy design.

Acceptance:

- Missing context denies access.
- Same-organization reads and permitted writes succeed.
- Cross-organization reads and writes return no protected data.
- Context cannot be selected by a caller-controlled header without server-side membership validation.
- Runtime role cannot alter schemas, policies, or bypass RLS.
- PostgreSQL tests cover connection reuse and transaction cleanup.

### 3. Convert live security probes into repeatable integration tests

Status: next security milestone.

Add an opt-in integration-test harness that starts from disposable services or clearly configured external services. Keep credentials outside the repository. Cover real login, lockout, email confirmation, password recovery, session expiry/revocation, cookie, anti-forgery, user, membership, role, and tenant cases (the Identity auth lifecycle and CSRF flows are already covered by automated integration tests). Keep the existing synthetic tests as fast regression coverage.

Acceptance:

- At least one valid real staff login flow is automated.
- Unconfirmed-email, wrong-password, lockout, revoked-session, and missing/!invalid CSRF rejection are automated.
- Viewer, CaseWorker, OrganizationAdmin, and SuperAdmin behavior is tested against the approved policy.
- Tests do not run against persistent or production databases.

### 4. Implement citizen intake safely

Status: citizen self-service profile is implemented (`/api/citizens/me`, `CitizenClient` policy). Case submission intake remains unstarted and depends on the tenant boundary and an explicit citizen organization policy.

A citizen profile contract now exists (create/update own profile keyed to the authenticated identity, with duplicate detection). Still required: a dedicated citizen submission contract and service rather than reusing staff mutation authorization. Define whether a citizen selects an approved public intake organization or is associated through an existing relationship. Add multipart case/document creation with transactional metadata cleanup and anti-enumeration behavior.

Acceptance:

- Citizen users can submit only through the approved intake policy.
- Staff-only workflow mutations remain unavailable to citizen clients.
- Documents are validated, associated with the correct organization, and inaccessible across organizations.
- Object storage and malware-scanning interfaces are explicit; local storage is not represented as production storage.

### 5. Production readiness and deployment

Status: unstarted.

Complete ProblemDetails/error contracts, structured logs, correlation IDs, readiness dependencies, rate limits, audit review, secret manager integration, backups/recovery procedures, production HTTPS cookie/email configuration, CI integration-test stages, deployment manifests, and a non-production demonstration.

Acceptance:

- Production business endpoints are enabled only after the security gates pass.
- Deployment uses separate migration/runtime database identities and no committed secrets.
- Operational failure modes and recovery steps are documented and tested where practical.

## External prerequisites

Independent development should continue before requesting these actions:

1. Docker Desktop or equivalent local container runtime, required for disposable PostgreSQL tests.
2. A disposable PostgreSQL database, required for RLS and privilege tests.
3. An SMTP/provider email account, required before production email verification/reset delivery is exercised.
4. An approved object-storage and malware-scanning provider/configuration, required before production document handling.
5. Deployment environment, TLS, secret management, and monitoring decisions, required before production endpoint enablement.

No production credentials, persistent database changes, paid services, or external account actions are assumed by this roadmap.

## Deferred Testing

These tests remain deferred selectively; existing security assertions must not be removed or weakened.

| Test | Risk | Reason | Trigger to complete |
|---|---|---|---|
| Exhaustive real-login permutation matrix (lockout, revocation, CSRF) | Medium | Requires a repeatable disposable database and email sink | Automated real-service harness is available |
| Browser-level run of the invitation redemption flow | Medium | No browser runner is available in this environment | A browser runner is selected and the `/setup-password` contract is stable |
| Full browser automation for login and all frontend workflows | Medium | Adds browser/runtime setup before the staff UI contract stabilizes | Staff workflow reaches acceptance and a browser runner is selected |
| Visual regression and responsive screenshot suite | Low | Does not protect authorization or data correctness | Core workflow and deployment demo are stable |
| Load/stress testing | Medium | Requires representative deployment topology and traffic model | Production-like environment and performance targets exist |
| Malware scanner outage/retry/recovery scenarios | High | Provider and failure contract are not selected | Scanner adapter and operational policy are approved |
| Repeatable full PostgreSQL RLS matrix | High | Current evidence is a disposable live probe, not CI coverage | PostgreSQL integration harness is available |

## Working rules for future batches

- Inspect the current diff before editing.
- Keep each batch focused and run the narrowest relevant checks immediately.
- Never use Development configuration to grant platform or organization roles implicitly.
- Do not enable production business endpoints while the database and real-identity boundaries remain incomplete.
- Report verified, prepared, deferred, and blocked work separately.
