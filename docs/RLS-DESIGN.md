# PostgreSQL Row-Level Security Design and Implementation

This document describes the row-level security (RLS) design that enforces organization-scoped data isolation in CivicFlow Enterprise. For the project overview, see the [README](../README.md).

The complete endpoint inventory and role/resource authorization matrix are
maintained in [API Contract and Authorization Matrix](API-AUTHORIZATION.md).

---

## Table of Contents

- [Overview](#overview)
- [RLS-Protected Tables](#rls-protected-tables)
- [Unprotected Tables](#unprotected-tables)
- [Design Principles](#design-principles)
- [Policy Shape](#policy-shape)
- [Application-Level Organization Predicates](#application-level-organization-predicates)
- [Database Roles](#database-roles)
- [Verification Results](#verification-results)
- [Automated Coverage](#automated-coverage)

---

## Overview

RLS is enabled and forced for six tenant-owned operational tables. Migration `20261019150000_AddTenantRowLevelSecurity` established the baseline for cases, status/assignment history, documents, and staff. Migration `20261024123000_AddCitizenCaseTrackingRls` added the first citizen owner branch for cases and status history. Migration `20261025070100_AddCitizenTrackingRls` adds `case_updates` and extends the status-history, document, and staff policies so the citizen tracking projection and response flow remain owner-scoped. The table owner is subject to the policies, and the runtime role cannot bypass them.

RLS is a **database-level enforcement layer** that ensures queries on tenant-owned tables only return rows matching the current organization context. It works alongside — not instead of — application-level authorization checks.

---

## RLS-Protected Tables

| Table | Purpose | RLS Policy |
|---|---|---|
| `cases` | Service requests/issues | Staff organization context, or the submitting citizen's own case |
| `case_status_history` | Status change audit trail | Staff organization context, or the submitting citizen's own case |
| `case_assignment_history` | Assignment audit trail | Same, on `OrganizationId` |
| `case_documents` | Uploaded case documents | Staff organization context, or the submitting citizen's own case when the document is citizen-visible |
| `staff_members` | Organization staff | Staff organization context; the submitting citizen may read only the staff row assigned to their own case |
| `case_updates` | Public updates, information requests, internal notes, and citizen responses | Staff organization context, or the submitting citizen's own case when the update is citizen-visible |

All six tables carry an `OrganizationId` column that the RLS policy checks against the transaction-local `civicflow.organization_id` PostgreSQL setting. Citizen self-service additionally sets `civicflow.user_id`; the owner-scoped clauses compare that setting with `cases.SubmittedByUserId`. For `case_updates` and `case_documents`, the row must also be marked `IsVisibleToCitizen`; internal notes and internal-only documents are invisible to the citizen even within their own case.

---

## Unprotected Tables

The following tables are **not** RLS-protected, by design:

| Table | Reason |
|---|---|
| `organizations` | Establishes the trusted identity-to-organization relationship; requires separate review if placed behind RLS |
| `organization_memberships` | Same — establishes trusted identity-to-organization mapping |
| `account_invitations` | Redemption is anonymous; RLS would block the lookup/accept flow |
| `application_users` | Identity table; access controlled by ASP.NET Core Identity and authorization policies |
| `citizen_profiles` | Scoped to authenticated user identity, not organization |
| `personal_access_tokens` | Scoped to authenticated user identity, not organization |
| `audit_events` | Platform reads gated by super-administrator check; organization reads scoped in the query service |
| `application_errors` | Platform reads gated by super-administrator check |
| Identity tables | Managed by ASP.NET Core Identity framework |

Organization and membership resolution intentionally occurs before the tenant transaction because those tables establish the trusted identity-to-organization relationship; they require separate review if they are later placed behind RLS.

**Anonymous flows that write to tenant tables** — `account_invitations` is exempt from RLS so redemption can find the pending row, but redeeming an invitation also inserts a `staff_members` row, and `staff_members` IS tenant protected. `InvitationService.AcceptAsync` therefore opens the tenant transaction explicitly via `IOrganizationTransactionContext.BeginAsync(invitation.OrganizationId, ...)` before `EnsureOrganizationMembershipAsync` and `SaveChangesAsync`; without that call PostgreSQL rejects the insert with `42501` and the accept endpoint returns `500`. Any future anonymous write to a tenant table must follow the same pattern — resolve the target organization from the request payload, then wrap the writes in `BeginAsync`.

---

## Design Principles

1. **Authenticate first** — Resolve the application user and active membership in application code.
2. **Set context after resolution** — Set `civicflow.organization_id` with `set_config(..., true)` only after authentication and membership resolution succeed.
3. **Read the transaction-local value** — Policies read the transaction-local setting, never the raw request header.
4. **Use a distinct citizen context** — Citizen self-service sets `civicflow.user_id` transaction-locally and derives the value from the authenticated principal, never from a request parameter or header.
5. **Execute within the request transaction** — Tenant queries and writes execute inside the same request transaction.
6. **Clean up on commit/rollback** — `OrganizationTransactionMiddleware` completes the transaction context; pooled connections do not retain the `SET LOCAL` values.
7. **Runtime role cannot bypass** — The runtime role cannot bypass RLS or alter policies.
8. **Test edge cases** — Missing context, invalid context, cross-organization and cross-citizen reads/writes, and connection-pool reuse must be tested.

---

## Policy Shape

```sql
ALTER TABLE cases ENABLE ROW LEVEL SECURITY;
ALTER TABLE cases FORCE ROW LEVEL SECURITY;

CREATE POLICY cases_organization_isolation
ON cases
USING (current_setting('civicflow.organization_id', true) = "OrganizationId"::text)
WITH CHECK (current_setting('civicflow.organization_id', true) = "OrganizationId"::text);
```

The same organization-isolation shape applies to the baseline tables. `FORCE ROW LEVEL SECURITY` ensures the table owner is also subject to the policy.

The citizen tracking tables add an owner-scoped branch. For example:

```sql
ALTER TABLE case_updates ENABLE ROW LEVEL SECURITY;
ALTER TABLE case_updates FORCE ROW LEVEL SECURITY;

CREATE POLICY case_updates_organization_isolation
ON case_updates
USING (
  current_setting('civicflow.organization_id', true) = "OrganizationId"::text
  OR (
    current_setting('civicflow.user_id', true) IS NOT NULL
    AND "IsVisibleToCitizen"
    AND EXISTS (
      SELECT 1
      FROM cases citizen_case
      WHERE citizen_case."Id" = "case_updates"."CaseId"
        AND citizen_case."SubmittedByUserId"::text =
            current_setting('civicflow.user_id', true)
    )
  )
)
WITH CHECK (
  current_setting('civicflow.organization_id', true) = "OrganizationId"::text
  OR (
    current_setting('civicflow.user_id', true) IS NOT NULL
    AND "IsVisibleToCitizen"
    AND "AuthorUserId"::text = current_setting('civicflow.user_id', true)
    AND EXISTS (
      SELECT 1
      FROM cases citizen_case
      WHERE citizen_case."Id" = "case_updates"."CaseId"
        AND citizen_case."SubmittedByUserId"::text =
            current_setting('civicflow.user_id', true)
    )
  )
);
```

A citizen context can therefore insert and read only its own visible rows. `case_status_history` allows the owner's own case so the automatic `AwaitingInformation` → `UnderReview` transition can be written; `case_documents` restricts the owner branch to `IsVisibleToCitizen`; and the `staff_members` owner branch is read-only and limited to the staff row assigned to the citizen's own case.

---

## Application-Level Organization Predicates

The application-level organization predicates (in `AuthenticatedOrganizationResolver` and the service layer) remain an additional defense. They:
- Resolve the authenticated user from `ClaimTypes.NameIdentifier` and treat `X-Organization-Id` only as a requested membership selector
- Verify active membership and active organization
- Verify the application account is active
- Set the `civicflow.organization_id` transaction context via `OrganizationTransactionContext`
- Provide defense in depth alongside RLS

`GET /api/organizations/me` returns only the caller's active memberships and is the frontend's source for organization selection. Organization dashboard statistics are derived from this validated context; a supplied organization ID is rejected when it does not match.

Citizen tracking routes resolve the application user from `ClaimTypes.NameIdentifier` and call `BeginCitizenAsync(userId)` before querying tenant tables. The list, detail, response, and document queries are constrained to `SubmittedByUserId = caller`; the controller performs no client-supplied user lookup. A case owned by another citizen is indistinguishable from a missing case (`404`). Citizen routes do not accept `X-Organization-Id`.

Case queries add resource-level authorization after organization resolution: organization administrators can query every case in their active organization, while staff list/detail/history/document endpoints are filtered to cases assigned to the authenticated user's active staff record. Assignment management is restricted to organization administrators. A CaseWorker assignment may update case details and status and upload documents; a Viewer assignment is read-only. Only an organization administrator can delete a case, and the endpoint requires explicit confirmation. These checks complement organization predicates and RLS, and identifier-based lookups use the same assigned-case filter to avoid leaking unassigned or co-worker cases.

Organization audit reads use the active organization selected by the server-validated membership context. Organization administrators see that organization's events; staff with an active CaseWorker or Viewer membership see only events whose actor ID matches their authenticated application account. A client-supplied `actorUserId` is an additional filter and cannot override or widen this mandatory actor scope.

---

## Database Roles

Two PostgreSQL roles are created by `infra/postgres/init/001-create-application-roles.sh`:

| Role | Privileges | Used by |
|---|---|---|
| `civicflow_migrator` | CREATE (schema objects) | EF Core migrations only |
| `civicflow_runtime` | DML only (no DDL) | The running API |

The runtime role cannot:
- Create schema objects (tables, indexes, policies)
- Alter or drop RLS policies
- Bypass RLS (`FORCE ROW LEVEL SECURITY` applies to the owner)

---

## Validation CHECK Constraints

RLS governs which rows are visible. The separate `AddValidationConstraints` migration governs what values a visible row may contain. It adds 27 PostgreSQL CHECK constraints for critical bounds, including citizen age and identifier length, case title and description length, case status and priority values, document size, update kind and message length, status-history values and explanation length, organization name and code, and account, staff, and invitation name, email, phone, and role fields.

These constraints are defense in depth, not a replacement for application validation or authorization. They protect against direct database writes, future code paths that bypass the domain, and accidental data corruption. The Down path drops all 27 constraints. When RLS tests apply the full migration chain, they also exercise this migration.

---

## Verification Results

The live disposable verification confirmed:

- RLS is enabled and forced on all six tenant-owned operational tables, including `case_updates`.
- The runtime role sees no rows without a context or with a different organization context.
- A same-organization API case create/status update succeeds.
- A cross-organization case request returns `403` without protected data.
- A citizen context sees only visible updates and documents on its own case; internal notes and internal-only documents return no rows.
- A citizen context cannot read another citizen's case rows and cannot insert an update attributed to another author or into another citizen's case.
- The runtime role cannot create schema objects or bypass the policies.

---

## Automated Coverage

`CitizenCaseTrackingRlsTests` is an opt-in PostgreSQL suite (3 tests) that applies the real migrations and probes the policies with the admin and runtime roles. It is skipped unless both disposable connection strings are supplied:

```bash
export CIVICFLOW_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55433;Database=civicflow;Username=civicflow_admin;Password=<local-admin-password>'
export CIVICFLOW_TEST_POSTGRES_RUNTIME='Host=127.0.0.1;Port=55433;Database=civicflow;Username=civicflow_runtime;Password=<local-runtime-password>'
dotnet test PublicServiceCaseManagement.Tests/PublicServiceCaseManagement.Tests.csproj \
  --configuration Release --filter FullyQualifiedName~CitizenCaseTrackingRlsTests
```

The suite asserts that migrations apply cleanly, that RLS is enabled and forced on the citizen tracking tables, that the citizen owner sees only visible rows, that the owner cannot write internal or cross-citizen rows, and that the runtime role cannot create schema objects. It targets a disposable database only and is not pointed at a shared or production instance. The remaining work is to wire the disposable PostgreSQL fixture into CI; until then the suite is opt-in and reported as skipped in the default `dotnet test` run.
