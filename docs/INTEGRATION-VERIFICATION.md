# PostgreSQL and Keycloak integration verification

## Current evidence

The repository currently has automated tests with EF Core InMemory and a synthetic authentication handler. Those tests verify authorization and tenant-boundary behavior at the application level, but they do not prove PostgreSQL behavior or Keycloak-issued-token validation.

During the current handover, Docker Compose was available but the Docker daemon was not reachable. No containers were started, no database was migrated, and no records or privileges were changed. Real integration verification remains incomplete.

## Required local verification sequence

Use the disposable local fixtures and local-only credentials:

```bash
cp .env.example .env
# Replace placeholders in .env; never commit .env.
docker compose up -d postgres keycloak

# Create a Keycloak test user through the local admin UI, then set:
# KEYCLOAK_TEST_USERNAME, KEYCLOAK_TEST_PASSWORD, and
# CIVICFLOW_ORGANIZATION_ID for scripts/verify-keycloak-token.sh.

dotnet ef database update \
  --project PublicServiceCaseManagement.Api \
  --startup-project PublicServiceCaseManagement.Api

dotnet test PublicServiceCaseManagement.sln \
  --configuration Release \
  --no-restore
```

The Compose fixture imports `infra/keycloak/civicflow-realm.json` and initializes separate `civicflow_migrator` and `civicflow_runtime` PostgreSQL roles. Configure the API connection with the runtime role and use the migration role only for `dotnet ef database update`.

Run the token smoke test after the API is running and the test user is linked to an application user and active organization membership:

```bash
scripts/verify-keycloak-token.sh
```

The fixture does not create application users or organization memberships automatically. Those records must be seeded deliberately for the test identity before API authorization can succeed. The token smoke script checks issuer/client claims and calls the readiness endpoint without printing the token; it does not replace full API authorization tests. RLS remains intentionally disabled until the trusted transaction context described in [`RLS-DESIGN.md`](RLS-DESIGN.md) is implemented.

Do not run the migration command against a persistent or production database. Do not put Keycloak credentials, database passwords, client secrets, or tokens in the repository, test output, or documentation.

## Required acceptance evidence

- Keycloak-issued staff token succeeds only for the configured staff client.
- Citizen token cannot access staff endpoints.
- Expired, wrong-issuer, wrong-audience, and wrong-client tokens are rejected.
- Active application user, membership, and organization are required.
- Cross-organization case and document access is denied without data disclosure.
- PostgreSQL migrations apply to a clean disposable database.
- Runtime database role cannot apply migrations or alter schema.
- Migration role is not used by the running API.
- Production endpoint behavior is verified with `BusinessEndpoints:Enabled` both `false` and `true`.
