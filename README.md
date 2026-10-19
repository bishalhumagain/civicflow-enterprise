# CivicFlow Enterprise

CivicFlow Enterprise is a .NET public-service case-management platform for organization-scoped workflows. It is a portfolio project focused on secure multi-tenant design, explicit domain rules, auditable case changes, automated testing, and deployment readiness.

The project is currently a backend-first modular monolith. The API and persistence model are real and tested; production endpoint enablement, real Keycloak/PostgreSQL end-to-end verification, public citizen intake, and the frontend remain future milestones.

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
| Frontend | React/TypeScript starter project; application UI not yet implemented |

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

The current document endpoint is available to the existing authorized staff case workflow and remains Development-guarded. Public citizen case submission with attachments is not yet implemented; it is the next product milestone because it requires an approved citizen intake and organization-resolution model.

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

## Authorization model

All protected API requests require the configured staff client through the explicit `StaffClient` policy. Role and organization checks remain database-backed application checks.

| Role | Permissions |
|---|---|
| Viewer | Read permitted case data and history; cannot create, change status, assign, or administer memberships |
| CaseWorker | Perform permitted case workflow operations; cannot administer organization memberships |
| OrganizationAdmin | Case workflow plus membership administration within the own organization |
| SuperAdmin | Platform organization and cross-organization membership administration; no automatic case access |

Business controllers still contain Development-only guards. This is deliberate: production business endpoints have not yet been enabled. Production-environment tests verify that anonymous and citizen-client requests are rejected and that authenticated staff requests still receive the existing `404` guard response.

The health endpoint is explicitly anonymous and currently reports process liveness only. It does not yet verify PostgreSQL or Keycloak readiness.

## API routes

### Cases

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/cases` | Create a case in Development |
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

## Local development

Prerequisites:

- .NET 10 SDK
- Docker Desktop with Docker Compose
- PostgreSQL 17 or the repository Compose service
- Keycloak for real identity integration

Configure local-only credentials through ignored environment or development configuration. Never commit passwords, access tokens, client secrets, or production connection strings.

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
159 passed, 0 failed, 0 skipped
```

The test suite includes domain tests, service tests, HTTP integration tests, organization isolation tests, authorization tests, Production-environment guard tests, and case-document upload/download tests.

Current test limitations:

- The API integration suite uses EF Core InMemory rather than PostgreSQL.
- Authentication tests use a synthetic handler rather than real Keycloak-issued JWTs.
- No RLS or runtime least-privilege database test exists yet.
- No malware-scanning or content-signature validation pipeline exists yet.

CI is defined in `.github/workflows/ci.yml` and runs Release build/test commands. Remote CI status must be verified from an actual workflow run; this README does not claim a current remote result.

## Security and production limitations

CivicFlow is not production-ready.

Remaining security and operational work includes:

- Enable production business endpoints only after the authorization matrix is verified in a production-like environment.
- Add real PostgreSQL integration tests and migration verification.
- Add real Keycloak token and realm/client integration tests.
- Add PostgreSQL Row-Level Security or an equivalent defense-in-depth tenant boundary.
- Use separate migration and runtime database roles with least privilege.
- Replace local file storage with a durable object-storage adapter for production deployment.
- Add malware scanning, content-signature validation, retention, deletion, and download audit events for documents.
- Add structured logging, correlation IDs, ProblemDetails, metrics, dependency readiness, and operational alerts.
- Add rate limiting, request-size policy, abuse prevention, backups, recovery, and data-retention controls.
- Add a real frontend, screenshots, deployment documentation, and a reviewer-friendly demo.

## Next milestone

The next milestone is **public citizen case intake with secure document submission**.

It should define and implement, before enabling production endpoints:

1. Citizen authentication and the relationship between citizens and organizations.
2. A policy for whether citizens need explicit organization membership or use an approved public intake organization.
3. A dedicated public case-submission endpoint and DTO.
4. Multipart case creation with transactional case/document metadata handling.
5. File content-signature checks and malware-scanning integration points.
6. Document retention, deletion, download authorization, and audit events.
7. PostgreSQL-backed tenant and migration tests.
8. Real Keycloak-issued-token tests for staff and citizen clients.

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

The project still needs a finished UI, production-like infrastructure tests, public intake, deployment evidence, and operational documentation to represent a complete enterprise product.

## Author

**Bishal Humagain**

- GitHub: [bishalhumagain](https://github.com/bishalhumagain)
- LinkedIn: [Bishal Humagain](https://www.linkedin.com/in/bishalhumagain)
- Repository: [CivicFlow Enterprise](https://github.com/bishalhumagain/civicflow-enterprise)
