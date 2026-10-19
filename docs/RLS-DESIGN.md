# PostgreSQL Row-Level Security design

RLS is not enabled yet. Enabling it before the API establishes a trusted per-request database context could either deny all application queries or incorrectly trust the caller-controlled `X-Organization-Id` header.

The required design is:

1. Authenticate and resolve the application user and active membership in application code.
2. Set a transaction-local organization value only after that resolution succeeds.
3. Use policies that read the transaction-local value, never the raw request header.
4. Execute tenant queries and writes inside the same transaction.
5. Ensure the runtime role cannot bypass RLS or alter policies.
6. Test missing context, invalid context, cross-organization reads, writes, and connection-pool reuse.

Illustrative policy shape, not an applied migration:

```sql
ALTER TABLE cases ENABLE ROW LEVEL SECURITY;

CREATE POLICY cases_organization_isolation
ON cases
USING ("OrganizationId" = current_setting('civicflow.organization_id', true)::uuid)
WITH CHECK ("OrganizationId" = current_setting('civicflow.organization_id', true)::uuid);
```

This example must not be applied until the API transaction/context mechanism and PostgreSQL privilege tests are implemented. The application-level organization predicates remain the active tenant boundary today.
