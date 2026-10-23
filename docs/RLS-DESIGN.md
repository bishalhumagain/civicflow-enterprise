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
- [Remaining Gap](#remaining-gap)

---

## Overview

RLS is enabled for five tenant-owned operational tables by migration `20261019150000_AddTenantRowLevelSecurity`. The migration enables and forces RLS, so the table owner is subject to the policy and the runtime role cannot bypass it.

RLS is a **database-level enforcement layer** that ensures queries on tenant-owned tables only return rows matching the current organization context. It works alongside — not instead of — application-level authorization checks.

---

## RLS-Protected Tables

| Table | Purpose | RLS Policy |
|---|---|---|
| `cases` | Service requests/issues | `current_setting('civicflow.organization_id', true) = "OrganizationId"::text` |
| `case_status_history` | Status change audit trail | Same, on `OrganizationId` |
| `case_assignment_history` | Assignment audit trail | Same, on `OrganizationId` |
| `case_documents` | Uploaded case documents | Same, on `OrganizationId` |
| `staff_members` | Organization staff | Same, on `OrganizationId` |

All five tables carry an `OrganizationId` column that the RLS policy checks against the transaction-local `civicflow.organization_id` PostgreSQL setting.

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

---

## Design Principles

1. **Authenticate first** — Resolve the application user and active membership in application code.
2. **Set context after resolution** — Set `civicflow.organization_id` with `set_config(..., true)` only after authentication and membership resolution succeed.
3. **Read the transaction-local value** — Policies read the transaction-local setting, never the raw request header.
4. **Execute within the request transaction** — Tenant queries and writes execute inside the same request transaction.
5. **Clean up on commit/rollback** — `OrganizationTransactionMiddleware` completes the transaction context; pooled connections do not retain the `SET LOCAL` value.
6. **Runtime role cannot bypass** — The runtime role cannot bypass RLS or alter policies.
7. **Test edge cases** — Missing context, invalid context, cross-organization reads/writes, and connection-pool reuse must be tested.

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

The same policy shape applies to all five protected tables. `FORCE ROW LEVEL SECURITY` ensures the table owner is also subject to the policy.

---

## Application-Level Organization Predicates

The application-level organization predicates (in `AuthenticatedOrganizationResolver` and the service layer) remain an additional defense. They:
- Resolve the authenticated user from `ClaimTypes.NameIdentifier` and treat `X-Organization-Id` only as a requested membership selector
- Verify active membership and active organization
- Verify the application account is active
- Set the `civicflow.organization_id` transaction context via `OrganizationTransactionContext`
- Provide defense in depth alongside RLS

`GET /api/organizations/me` returns only the caller's active memberships and is the frontend's source for organization selection. Organization dashboard statistics are derived from this validated context; a supplied organization ID is rejected when it does not match.

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

## Verification Results

The live disposable verification confirmed:

- RLS is enabled and forced on all five tenant-owned operational tables.
- The runtime role sees no rows without a context or with a different organization context.
- A same-organization API case create/status update succeeds.
- A cross-organization case request returns `403` without protected data.
- The runtime role cannot create schema objects.

---

## Remaining Gap

A repeatable automated PostgreSQL test harness is not yet implemented. These checks are currently recorded as disposable live verification rather than CI tests. Converting them to repeatable integration tests is tracked in the [Delivery Roadmap](DELIVERY-ROADMAP.md) (item 2 and item 3).
