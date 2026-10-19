# CivicFlow Enterprise

CivicFlow Enterprise is a .NET public-service case-management platform for organization-scoped workflows. It is a portfolio project focused on secure multi-tenant design, explicit domain rules, auditable case changes, automated testing, and deployment readiness.

The project is currently a backend-first modular monolith with a reviewer-facing React dashboard. The API and persistence model are real and tested. A disposable PostgreSQL/Keycloak verification run covers migration execution, runtime-role restrictions, forced RLS, and real JWT validation; production enablement, citizen intake, and deployment remain future milestones.

## Project identity, ownership, and license

- Project: CivicFlow Enterprise
- Owner attribution: Bishal Humagain
- Repository: <https://github.com/bishalhumagain/civicflow-enterprise>
- Current API version metadata: `0.1.0`
- Machine-readable provenance: [`CIVICFLOW-PROVENANCE.json`](CIVICFLOW-PROVENANCE.json)

No `LICENSE` file is currently present. Copyright generally applies automatically, but publishing source without a license does not grant broad reuse permissions in the way a permissive open-source license does. The current repository therefore uses a rights-reserved posture by omission; the owner should choose explicitly between keeping the project unlicensed/rights-reserved, adding a proprietary license notice, or selecting an open-source license. This documentation is not legal advice.

Project identity and a future signed release are separate concerns. The provenance manifest identifies the project; it does not prevent copying. The documented detached-signature workflow verifies an official artifact only when an owner-controlled signing key and published public-key fingerprint are used. See [`docs/RELEASE-VERIFICATION.md`](docs/RELEASE-VERIFICATION.md).

## Goals

- Model public-service cases and their lifecycle with explicit domain rules.
- Keep tenant data isolated by authenticated organization membership.
- Record status and assignment history for operational traceability.
- Store supporting case documents without trusting client file names or paths.
- Demonstrate maintainable ASP.NET Core, EF Core, PostgreSQL, and testing practices.
- Make security decisions explicit instead of relying on Development-only shortcuts.
- Provide a credible foundation for a reviewer-facing case-management UI and deployment.

## Technology

| Area | Technology |
|---|---|
| API | ASP.NET Core Web API on .NET 10 |
| Persistence | Entity Framework Core with PostgreSQL 17/Npgsql |
| Authentication | Keycloak-compatible JWT Bearer authentication |
| API documentation | Swagger/OpenAPI in Development |
| Testing | xUnit and ASP.NET Core integration testing |
| Test database | EF Core InMemory for the current automated suite |
| Local infrastructure | Docker Compose for PostgreSQL and Keycloak |
| Frontend | React/TypeScript dashboard with `oidc-client-ts` Authorization Code + PKCE and API integration |

## Current architecture

```text
PublicServiceCaseManagement/
├── PublicServiceCaseManagement.Api/
│   ├── Domain/
│   │   ├── Cases/
│   │   ├── Organizations/
│   │   ├── Staff/
│   │   └── Users/
│   ├── Features/
│   │   ├── Cases/
│   │   ├── Health/
│   │   ├── Memberships/
│   │   ├── Organizations/
│   │   └── Staff/
│   ├── Infrastructure/
│   │   ├── Files/
│   │   ├── Organizations/
│   │   └── Persistence/
│   └── Migrations/
├── PublicServiceCaseManagement.Tests/
├── civicflow-web/
├── compose.yaml
└── README.md
```

The API is a modular monolith. Domain entities contain core invariants, feature services coordinate application workflows, controllers expose HTTP contracts, and infrastructure contains organization resolution, storage, and persistence concerns.

## Achieved capabilities

### Case workflow

- Create cases in Development through the existing staff API.
- Retrieve one case or paginated cases.
- Enforce status transitions in the domain model.
- Record status history.
- Assign active staff members within the same organization.
- Record assignment history.
- Prevent edits to closed or rejected cases.

### Organizations and access

- Model organizations, application users, memberships, staff, organization roles, and platform roles.
- Resolve the organization from the authenticated external identity, `iss`, `sub`, and `X-Organization-Id`.
- Require active application identity, active membership, and active organization for organization-scoped operations.
- Support Viewer, CaseWorker, OrganizationAdmin, and SuperAdmin behavior.
- Allow SuperAdmin membership administration across organizations without granting automatic case access.
- Restrict OrganizationAdmin membership administration to the current organization.
- Use the authenticated organization resolver in Development as well as other environments.
- Declare the `StaffClient` authorization policy on protected business controllers.
- Require an active application user in addition to active membership and organization for authenticated case mutations.

### Supporting documents

The current case API supports document attachments through:

- `POST /api/cases/{caseId}/documents` using `multipart/form-data`
- `GET /api/cases/{caseId}/documents`
- `GET /api/cases/{caseId}/documents/{documentId}`

The document implementation:

- Stores metadata in PostgreSQL through `CaseDocument`.
- Stores file bytes outside the web root through `IFileStorage` and `LocalFileStorage`.
- Uses organization/case-scoped random storage keys instead of client file names.
- Rejects path traversal and unsafe file names.
- Allows only PDF, JPEG, PNG, DOC, and DOCX extensions with matching content types.
- Enforces a 10 MB upload limit.
- Rejects empty files.
- Records file size and a SHA-256 hash.
- Cleans up a stored file if metadata persistence fails.
- Applies the same organization boundary to metadata and downloads.

The current document endpoint is available to the existing authorized staff case workflow and remains behind the explicit business-endpoint gate. Public citizen case submission with attachments is not yet implemented; it follows real infrastructure security verification because it requires an approved citizen intake and organization-resolution model.

### Database

EF Core migrations currently cover:

- Cases
- Status history
- Staff members
- Case assignments
- Organizations
- Application users and memberships
- External identities
- Case documents

The application does not automatically run migrations at startup. Apply migrations deliberately against an approved environment.

### Staff frontend

The React dashboard provides a reviewer-facing staff interface backed by the configured API and Keycloak authentication. With `VITE_KEYCLOAK_AUTHORITY`, `VITE_KEYCLOAK_CLIENT_ID`, `VITE_KEYCLOAK_REDIRECT_URI`, `VITE_API_BASE_URL`, and `VITE_CIVICFLOW_ORGANIZATION_ID` configured, staff can sign in with Authorization Code + PKCE, search and filter cases, open case details, create cases, change status, inspect status and assignment history, assign staff members, manage staff and organization memberships, and upload or download supporting documents where authorized. The API remains the authority for permissions. The current frontend does not populate fabricated demo cases: it displays configuration, authentication, loading, error, and empty states as appropriate. Full browser end-to-end behavior remains unverified.

The dashboard also includes a separated public-intake foundation with accessible validation and an explicit unavailable state. It does not submit citizen requests: the API currently exposes no dedicated citizen intake contract, citizen-to-organization resolution policy, or citizen document authorization. This is intentionally not implemented by reusing staff endpoints.

For local frontend development, create `civicflow-web/.env.local` with `VITE_KEYCLOAK_AUTHORITY`, `VITE_KEYCLOAK_CLIENT_ID`, `VITE_KEYCLOAK_REDIRECT_URI`, `VITE_API_BASE_URL`, and `VITE_CIVICFLOW_ORGANIZATION_ID`. Use the Keycloak realm URL for the authority, the configured public client ID, a redirect URI registered in Keycloak, the API base URL, and the organization ID that matches an active membership for the signed-in user. Keep `.env.local` untracked and never put a client secret in frontend environment variables. From `civicflow-web`, run `npm install`, `npm run dev`, and open the local URL printed by Vite. Run `npm run lint` for linting and `npm run build` for TypeScript and production-build verification. Without the required live configuration, the dashboard explains the configuration or authentication state; it does not fall back to fabricated case data.

## Authorization model

All protected API requests require the configured staff client through the explicit `StaffClient` policy. Role and organization checks remain database-backed application checks.

| Role | Permissions |
|---|---|
| Viewer | Read permitted case data and history; cannot create, change status, assign, or administer memberships |
| CaseWorker | Perform permitted case workflow operations; cannot administer organization memberships |
| OrganizationAdmin | Case workflow plus membership administration within the own organization |
| SuperAdmin | Platform organization and cross-organization membership administration; no automatic case access |

Business endpoints are controlled by the explicit `BusinessEndpoints:Enabled` setting. Development enables them for local workflow testing; non-Development environments remain disabled unless this setting is deliberately set to `true`. Authentication, staff-client policy, active identity, membership, organization, and role checks still apply when the gate is enabled. Production-environment tests verify both the disabled default and explicit opt-in behavior.

`GET /api/health` is anonymous process liveness. `GET /api/health/ready` is anonymous and checks database connectivity, but it does not verify Keycloak readiness.

## API routes

### Cases

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/cases` | Create a case when business endpoints are enabled |
| GET | `/api/cases` | List organization cases |
| GET | `/api/cases/{id}` | Retrieve a case |
| PATCH | `/api/cases/{id}/status` | Change status |
| GET | `/api/cases/{id}/history` | Read status history |
| PATCH | `/api/cases/{id}/assignment` | Assign a staff member |
| GET | `/api/cases/{id}/assignment-history` | Read assignment history |
| POST | `/api/cases/{id}/documents` | Upload a supporting document |
| GET | `/api/cases/{id}/documents` | List document metadata |
| GET | `/api/cases/{id}/documents/{documentId}` | Download a document |

### Administration

- `/api/organizations` — platform organization administration
- `/api/organizations/{organizationId}/memberships` — organization membership administration
- `/api/staff` — organization staff administration
- `/api/health` — anonymous liveness response
- `/api/health/ready` — anonymous database-readiness response

## Local development

Prerequisites:

- .NET 10 SDK
- Docker Desktop with Docker Compose
- PostgreSQL 17 or the repository Compose service
- Keycloak for real identity integration

Configure local-only credentials through ignored environment or development configuration. Never commit passwords, access tokens, client secrets, or production connection strings.

For Docker Compose, copy `.env.example` to `.env` and replace every placeholder with local-only values. The copied `.env` is ignored by Git. Compose initializes separate `civicflow_migrator` and `civicflow_runtime` roles; use the runtime role for the API and the migrator role only for EF migrations. For API connection strings or non-default authentication settings, use environment variables or .NET user secrets; the API project declares a `UserSecretsId` for this purpose. Example:

```bash
dotnet user-secrets --project PublicServiceCaseManagement.Api set \
  ConnectionStrings:CivicFlow "Host=localhost;Database=civicflow;Username=civicflow_app;Password=<local-password>"
```

Do not put the replacement value in tracked files or paste it into issues, logs, or documentation. Development has safe non-secret defaults for Keycloak endpoint/client identifiers, but it still requires a configured database connection string.

Restore and build:

```bash
dotnet restore PublicServiceCaseManagement.sln
dotnet build PublicServiceCaseManagement.sln --configuration Release
```

Run tests:

```bash
dotnet test PublicServiceCaseManagement.sln --configuration Release
```

Swagger is enabled only in Development. The business API is intentionally not enabled for production use yet.

## Verification status

The latest verified local Release test run completed with:

```text
164 passed, 0 failed, 0 skipped
```

The test suite includes domain tests, service tests, HTTP integration tests, organization isolation tests, authorization tests, active-identity regression tests, production endpoint-gate tests, and case-document upload/download tests.

The Release solution build also completed with 0 errors. An isolated Compose project was verified without stopping or modifying the existing local services. Existing EF migrations applied successfully to the disposable PostgreSQL database using `civicflow_migrator`. The `civicflow_runtime` role can read application tables but cannot create schema objects and is not a superuser. Forced RLS is enabled on cases, case histories, case documents, and staff; the application transaction context is set only after database-backed membership resolution.

The isolated Keycloak realm issued a staff token with the expected issuer, API audience, and staff-client `azp`. Against the Development API configured explicitly for that disposable stack, the token returned 200 for its active organization, while a different organization, the citizen client, and an inactive membership returned 403. A tampered signature returned 401. These are manual disposable-stack probes, not committed automated tests; wrong-issuer, wrong-audience, and expiry cases still need automated real-realm coverage.

Current test limitations:

- The API integration suite uses EF Core InMemory rather than PostgreSQL.
- Most automated API tests use a synthetic handler rather than real Keycloak-issued JWTs.
- RLS is verified manually on the disposable database, but the checks are not yet repeatable CI integration tests.
- The real Keycloak probes are not yet encoded as repeatable CI integration tests.
- No malware-scanning or content-signature validation pipeline exists yet.
- A disposable Keycloak realm fixture, PostgreSQL role-init script, token smoke script, and maintained PKCE browser flow are present. Live verification requires local Docker services and disposable test identities.

CI is defined in `.github/workflows/ci.yml` and runs Release build/test commands. Remote CI status must be verified from an actual workflow run; this README does not claim a current remote result.

## Security and production limitations

CivicFlow is not production-ready.

Remaining security and operational work includes:

- Enable `BusinessEndpoints:Enabled` only after the authorization matrix and production-like authentication are verified; this milestone does not enable production business endpoints automatically.
- Add repeatable real PostgreSQL integration tests and migration/privilege verification to CI.
- Add automated real Keycloak issuer, audience, signature, expiry, client, role, membership, and tenant-boundary tests.
- Convert the implemented transaction-scoped RLS boundary into repeatable CI integration tests and review whether identity/membership tables need an additional policy design.
- Tighten database grants further after the RLS design is implemented and tested.
- Replace local file storage with a durable object-storage adapter for production deployment.
- Add malware scanning, content-signature validation, retention, deletion, and download audit events for documents.
- Add structured logging, correlation IDs, ProblemDetails, metrics, dependency readiness, and operational alerts.
- Add rate limiting, request-size policy, abuse prevention, backups, recovery, and data-retention controls.
- Add live case mutations and richer unauthorized/error states to the React dashboard.
- Add screenshots, deployment documentation, and a reviewer-friendly live demo.

## Next milestone

The single recommended next milestone is **repeatable PostgreSQL/Keycloak integration tests and production security review**. It should:

1. Derive the database organization context only after JWT validation and active membership resolution.
2. Set and clear the context inside a transaction-safe request boundary that works with connection pooling.
3. Test the implemented RLS boundary for cases, histories, documents, staff, and other tenant-owned operational tables.
4. Prove missing context, cross-organization access, inactive identities, and runtime-role non-bypass behavior against PostgreSQL.
5. Run repeatable real Keycloak tests for issuer, audience, signature, expiry, client, role, membership, and tenant boundaries.

Public citizen intake should follow this milestone, not precede it.

This sequence keeps the existing security rules intact instead of allowing a public upload path to bypass organization resolution.

## Portfolio summary

A hiring manager can currently verify:

- .NET 10 ASP.NET Core API development
- EF Core relational modeling and migrations
- Domain-driven case status rules
- Organization-scoped authorization
- Explicit role and platform-role behavior
- Auditable status and assignment history
- Securely constrained document metadata and local storage abstraction
- xUnit domain, service, and HTTP integration testing
- CI configuration and documented production limitations

The project still needs repeatable production-like infrastructure tests, trusted database tenant enforcement, public intake, deployment evidence, and operational documentation to represent a complete enterprise product.

## Phased roadmap

### Priority 1 — Trusted PostgreSQL tenant boundary and repeatable Keycloak verification (next milestone)

Convert the implemented trusted transaction-scoped organization context and forced RLS boundary into repeatable PostgreSQL/Keycloak integration tests. Acceptance requires denied cross-organization access at both application and database boundaries, clean-schema migration verification, real JWT claim validation, and documented runtime/migration roles.

### Priority 2 — Public citizen intake with secure document submission

Define the citizen-to-organization model, add a dedicated citizen submission contract, support transactional case/document creation, add content-signature and malware-scanning integration points, and test tenant boundaries with authenticated citizen identities. Acceptance requires explicit citizen authorization, no anonymous cross-tenant disclosure, and durable metadata/file cleanup behavior.

### Priority 3 — Production authorization enablement

Enable the explicit production gate only after the authorization matrix is covered in production-like tests. Acceptance requires authenticated staff and citizen-client behavior, role/membership coverage, consistent anti-enumeration responses, and no implicit Development privilege path.

### Priority 4 — Database defense in depth

Add or validate Row-Level Security and separate migration/runtime database roles. Acceptance requires denied cross-organization access at the database boundary and documented privilege evidence.

### Priority 5 — Auditability and operational hardening

Attribute changes to application users, add concurrency handling, ProblemDetails, correlation IDs, structured logs, readiness checks, rate limits, and document audit events. Acceptance requires traceable security-sensitive mutations and tests for concurrent updates and operational failure paths.

### Priority 6 — Production demo and deployment evidence

Add screenshots, deploy a non-production demonstration, and document CI/CD and recovery procedures. Acceptance requires a reproducible demo with no secrets and evidence-based deployment documentation.

The implementation-first delivery sequence is maintained in [`docs/DELIVERY-ROADMAP.md`](docs/DELIVERY-ROADMAP.md). The single recommended next security milestone is **repeatable PostgreSQL/Keycloak integration tests and production security review**. This comes before citizen intake or production enablement because it turns the implemented database defense-in-depth boundary into regression evidence.

## AI Development Handover

Important decisions:

- Organization-scoped operations require an authenticated application user, active membership, and active organization.
- SuperAdmin administers memberships across organizations but does not automatically access cases.
- OrganizationAdmin administers memberships only in the selected organization.
- Development-only business access remains convenient, while production business access requires explicit `BusinessEndpoints:Enabled=true`.
- Health liveness and database readiness are anonymous; Keycloak readiness is not checked yet.
- Current document storage is local filesystem storage through `IFileStorage`; it is not production object storage.
- No migrations are applied automatically at startup.
- Tests use EF Core InMemory and synthetic authentication; do not describe them as PostgreSQL/Keycloak end-to-end tests.
- The latest local Release verification is 164 passing tests; this is not evidence of real Keycloak or PostgreSQL execution.

Important files:

- `PublicServiceCaseManagement.Api/Program.cs` — authentication, authorization policies, database, and dependency injection.
- `PublicServiceCaseManagement.Api/Infrastructure/Organizations/AuthenticatedOrganizationResolver.cs` — authenticated tenant resolution.
- `PublicServiceCaseManagement.Api/Features/Cases/CasesController.cs` — current Development-guarded case and document routes.
- `PublicServiceCaseManagement.Api/Infrastructure/Files/LocalFileStorage.cs` — local upload validation and storage.
- `PublicServiceCaseManagement.Api/Infrastructure/Authorization/BusinessEndpointAccess.cs` — explicit production endpoint gate.
- `PublicServiceCaseManagement.Tests/CivicFlowApiFactory.cs` — explicit test identities, roles, memberships, and test environment.
- `PublicServiceCaseManagement.Tests/ProductionAuthorizationIntegrationTests.cs` — production-environment guard behavior.
- `civicflow-web/src/App.tsx` — reviewer-facing dashboard with live API workflow and explicit configuration/authentication/empty states; no fabricated demo cases.
- `civicflow-web/src/auth.ts` — optional browser Authorization Code + PKCE flow.
- `civicflow-web/src/api.ts` — bearer-token API request helper.
- `infra/keycloak/civicflow-realm.json` — disposable public-client realm fixture.
- `infra/postgres/init/001-create-application-roles.sh` — disposable migration/runtime role setup.
- `scripts/verify-keycloak-token.sh` — token claim and readiness smoke test without token output.
- `docs/RLS-DESIGN.md` — implemented RLS context/policy design and remaining automated verification.
- `CIVICFLOW-PROVENANCE.json` and `docs/RELEASE-VERIFICATION.md` — identity and release-verification design.

Safe verification commands:

```bash
dotnet build PublicServiceCaseManagement.Api/PublicServiceCaseManagement.Api.csproj --configuration Release --no-restore
dotnet test PublicServiceCaseManagement.sln --configuration Release --no-restore
git diff --check
```

Future agents must preserve existing uncommitted work, avoid persistent database migrations and container changes unless explicitly authorized, never expose secret values, and distinguish verified behavior from planned work. Review `git status` before editing and inspect the final diff for unrelated changes.

## Author

**Bishal Humagain**

- GitHub: [bishalhumagain](https://github.com/bishalhumagain)
- LinkedIn: [Bishal Humagain](https://www.linkedin.com/in/bishalhumagain)
- Repository: [CivicFlow Enterprise](https://github.com/bishalhumagain/civicflow-enterprise)
