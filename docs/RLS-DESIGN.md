# PostgreSQL Row-Level Security design and implementation

RLS is now enabled for `cases`, `case_status_history`, `case_assignment_history`, `case_documents`, and `staff_members` by migration `20261019150000_AddTenantRowLevelSecurity`. The migration enables and forces RLS, so the table owner is subject to the policy and the runtime role cannot bypass it.

The required design is:

1. Authenticate and resolve the application user and active membership in application code.
2. Set `civicflow.organization_id` with `set_config(..., true)` only after that resolution succeeds.
3. Use policies that read the transaction-local value, never the raw request header.
4. Execute tenant queries and writes inside the same request transaction.
5. Commit or roll back the transaction in `OrganizationTransactionMiddleware`; pooled connections do not retain the `SET LOCAL` value.
6. Ensure the runtime role cannot bypass RLS or alter policies.
7. Automate missing context, invalid context, cross-organization reads/writes, and connection-pool reuse tests.

The applied policy shape is:

```sql
ALTER TABLE cases ENABLE ROW LEVEL SECURITY;

CREATE POLICY cases_organization_isolation
ON cases
USING (current_setting('civicflow.organization_id', true) = "OrganizationId"::text)
WITH CHECK (current_setting('civicflow.organization_id', true) = "OrganizationId"::text);
```

The application-level organization predicates remain an additional defense. Organization and membership resolution intentionally occurs before the tenant transaction because those tables establish the trusted identity-to-organization relationship; they require separate review if they are later placed behind RLS.

The live disposable verification confirmed:

- RLS is enabled and forced on all five tenant-owned operational tables.
- The runtime role sees no rows without a context or with a different organization context.
- A same-organization API case create/status update succeeds.
- A cross-organization case request returns 403 without protected data.
- The runtime role cannot create schema objects.

The remaining gap is a repeatable automated PostgreSQL test harness; these checks are currently recorded as disposable live verification rather than CI tests.
