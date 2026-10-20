# PostgreSQL and Keycloak integration verification

## Current evidence

The automated suite still uses EF Core InMemory and a synthetic authentication handler for most API tests. A disposable live-stack verification has additionally been completed: migrations applied with `civicflow_migrator`, `civicflow_runtime` could read application tables but could not create schema objects, and a real Keycloak-issued staff token was accepted only for its active organization and staff client. Cross-organization access, citizen-client access, and inactive membership returned 403; a tampered signature returned 401.

Citizen self-service profile endpoints (`/api/citizens/me`) are covered by automated integration tests using the synthetic authentication handler: anonymous requests return 401, staff-client requests return 403, unlinked identities return 401, create/update behavior and duplicate validation are asserted, and the endpoints honour `BusinessEndpoints:Enabled` gating. These tests use EF Core InMemory, not a live database.

The verification used an isolated Compose project on alternate local ports and did not stop or modify the existing services or persistent data. RLS remains intentionally disabled.

## Required local verification sequence

Use the disposable local fixtures and local-only credentials:

```bash
cp .env.example .env
# Replace placeholders in .env; never commit .env.
docker compose up -d postgres keycloak

# Create a disposable Keycloak test user through the local admin UI, then set:
# KEYCLOAK_TEST_USERNAME, KEYCLOAK_TEST_PASSWORD, and
# CIVICFLOW_ORGANIZATION_ID for scripts/verify-keycloak-token.sh.

ConnectionStrings__CivicFlow="Host=localhost;Port=5432;Database=civicflow;Username=civicflow_migrator;Password=<local-migrator-password>" \
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
- PostgreSQL migrations apply to a clean disposable database using `civicflow_migrator`.
- Runtime database role can read application tables but cannot create schema objects or bypass the tested role boundary.
- Migration role is not used by the running API.
- Production endpoint behavior is verified with `BusinessEndpoints:Enabled` both `false` and `true`.
