# CivicFlow Enterprise

**A multi-organization public-service case management API built with .NET.**

CivicFlow Enterprise is an actively developed portfolio project exploring how to build maintainable, testable, and security-conscious software for public-service workflows. It focuses on domain-driven business rules, auditable case history, relational persistence, identity integration, and organization-scoped access.

The project is being developed as a modular monolith, with an emphasis on correctness, clear architectural boundaries, automated testing, and practical engineering trade-offs.

> **Project status:** Under active development. The current implementation includes case-management workflows, PostgreSQL persistence, EF Core migrations, Keycloak JWT authentication configuration, authenticated organization resolution, and automated tests. The API is not yet production-ready.

## Contents

- [Project goals](#project-goals)
- [Technology stack](#technology-stack)
- [Implemented capabilities](#implemented-capabilities)
- [Architecture](#architecture)
- [Authentication and organization isolation](#authentication-and-organization-isolation)
- [Prerequisites](#prerequisites)
- [Run locally](#run-locally)
- [API overview](#api-overview)
- [Testing](#testing)
- [Continuous integration](#continuous-integration)
- [Security and production readiness](#security-and-production-readiness)
- [Roadmap](#roadmap)

## Project Goals

- Model public-service cases and their lifecycle with explicit domain rules.
- Maintain persistent status history for traceability.
- Support organization-scoped access and membership validation.
- Separate business logic from API and persistence concerns.
- Use relational database migrations to evolve the schema safely.
- Verify behavior with unit, service, and HTTP integration tests.
- Demonstrate secure identity integration and practical .NET engineering.
- Establish a foundation for a user interface and AI-assisted case triage.

## Technology Stack

| Area | Technology |
|---|---|
| Language | C# |
| Runtime and framework | .NET 10, ASP.NET Core Web API |
| Database | PostgreSQL 17 |
| ORM and data access | Entity Framework Core, Npgsql |
| Authentication | Keycloak, JWT Bearer |
| API documentation | Swagger / OpenAPI |
| Testing | xUnit, ASP.NET Core integration testing |
| Test database | EF Core InMemory provider for selected tests |
| Local infrastructure | Docker Compose |
| Version control | Git, GitHub |
| CI | GitHub Actions workflow; verify current status in Actions |
| Planned frontend | React, TypeScript |
| Planned AI capabilities | AI-assisted case triage with human oversight |

## Implemented Capabilities

### Case management

- Create cases through the REST API.
- Retrieve individual cases by ID.
- List cases with pagination.
- Validate request data and enforce domain rules.
- Apply permitted case-status transitions.
- Reject invalid status transitions.
- Prevent changes to cases that are closed or rejected.
- Persist status changes and their associated history records.
- Retrieve case status history through the API.

### Persistence and database design

- Persist application data through Entity Framework Core and PostgreSQL.
- Manage schema evolution through EF Core migrations.
- Model application users, organizations, organization memberships, cases, and case history.
- Associate users with external identities using issuer and subject identifiers.
- Enforce a unique mapping for an external identity.

### Identity and organization resolution

- Configure JWT Bearer authentication against a Keycloak realm.
- Validate token issuer, audience, lifetime, and signing key.
- Resolve an authenticated user's organization membership using the token issuer, subject, and requested organization identifier.
- Reject organization resolution when the identity is missing, the organization header is invalid, or an active membership cannot be established.
- Exercise the organization-resolution path with automated tests.

These capabilities establish an authentication and organization-resolution foundation. They do not, by themselves, establish that every API endpoint is production-ready or that the full deployment has passed end-to-end security testing.

### Automated testing

The latest reported full test run completed with **111 passed, 0 failed, and 0 skipped**.

The suite includes domain and service tests, HTTP integration tests, and tests for authenticated organization resolution. Some tests use EF Core InMemory rather than PostgreSQL.

## Case Workflow

New cases begin in the `Submitted` state. Permitted status transitions are enforced by the domain model instead of being left entirely to controller logic.

The implemented workflow includes review, progress, requests for information, resolution, closure, and rejection states, along with assignment-related status. Actual staff assignment and reassignment workflows remain under development.

Status history stores information such as the case, previous status, new status, timestamp, and organization. Production-grade audit attribution to the authenticated staff member still requires further implementation and verification.

## Architecture

CivicFlow uses a **modular monolith**. This keeps deployment and local development straightforward while separating business rules, application features, and technical infrastructure.

The principal project structure is:

```text
PublicServiceCaseManagement/
├── PublicServiceCaseManagement.Api/
│   ├── Domain/
│   ├── Features/
│   ├── Infrastructure/
│   │   ├── Organizations/
│   │   └── Persistence/
│   ├── Migrations/
│   └── Program.cs
├── PublicServiceCaseManagement.Tests/
├── compose.yaml
├── PublicServiceCaseManagement.sln
└── README.md
```

- `Domain/` — domain entities, business rules, and core concepts.
- `Features/` — API endpoints, application services, requests, and responses.
- `Infrastructure/Organizations/` — organization context and authenticated membership resolution.
- `Infrastructure/Persistence/` — database context, entity configuration, and persistence concerns.
- `Migrations/` — versioned database schema changes.
- `PublicServiceCaseManagement.Tests/` — automated unit and integration tests.

The architecture will evolve as requirements become clearer. Abstractions should be introduced when they address a real design need rather than for the sake of adding layers.

## Authentication and Organization Isolation

CivicFlow integrates with Keycloak as its identity provider and uses JWT Bearer authentication configuration in the API.

The authenticated organization resolver:

1. Requires an authenticated HTTP identity.
2. Reads the token issuer (`iss`) and subject (`sub`).
3. Reads the requested organization from the `X-Organization-Id` header.
4. Looks up the application user by external issuer and subject.
5. Verifies that the user has an active membership in the requested active organization.
6. Returns the organization identifier only when the membership check succeeds.

The organization header identifies the organization being requested; it is **not proof of membership**. Membership must be validated against the authenticated identity and application database.

The current development configuration still uses a fixed development organization context. Case-controller endpoints also retain Development-only guards. The automated integration tests exercise the authenticated organization resolver through a test authentication handler and an in-memory database; they are not a substitute for an end-to-end test with a real Keycloak-issued token and PostgreSQL.

Before exposing these endpoints in a production environment, the application needs a deliberate production endpoint policy, comprehensive authorization coverage, and end-to-end verification.

## Prerequisites

Install the following tools:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) with Docker Compose
- [Git](https://git-scm.com/)
- An IDE such as [JetBrains Rider](https://www.jetbrains.com/rider/) or Visual Studio Code

Keycloak is required when running the local identity integration. PostgreSQL and Keycloak can be started using the repository's Docker Compose configuration if both services are defined there.

## Run Locally

Commands below assume you are in the repository root.

### 1. Clone the repository

```bash
git clone https://github.com/bishalhumagain/civicflow-enterprise.git
cd civicflow-enterprise
```

### 2. Configure local environment variables

Review `compose.yaml` to identify the environment variables required by the local services.

Create a local `.env` file with strong development-only credentials. If an `.env.example` file is provided, use it as a template. Never commit `.env`, passwords, access tokens, or other secrets.

Check that local configuration files containing credentials are excluded by `.gitignore`.

### 3. Start local infrastructure

Start PostgreSQL:

```bash
docker compose up -d postgres
```

If you are testing Keycloak integration, start the configured Keycloak service as well:

```bash
docker compose up -d keycloak
```

The service name must match the one declared in `compose.yaml`. If it differs, use the actual service name from that file.

Check service status:

```bash
docker compose ps
```

### 4. Configure the API

Configure the `CivicFlow` connection string using your local development settings or an environment variable.

For local identity integration, configure the Keycloak authority and API audience to match the realm and client configuration. The local development setup uses the `civicflow` realm and an API audience of `civicflow-api`.

Do not copy real credentials into tracked configuration files. Never publish a real JWT or a client secret in this README.

### 5. Restore and build

```bash
dotnet restore PublicServiceCaseManagement.sln
dotnet build PublicServiceCaseManagement.sln
```

### 6. Apply database migrations

Ensure the database is running and the API has the correct local connection string. Then apply the migrations:

```bash
dotnet ef database update \
  --project PublicServiceCaseManagement.Api \
  --startup-project PublicServiceCaseManagement.Api
```

If the `dotnet ef` command is unavailable, install or invoke the appropriate EF Core command-line tooling for your local development environment.

### 7. Run the API

Run the API using Rider's Development configuration or:

```bash
dotnet run \
  --project PublicServiceCaseManagement.Api \
  --no-launch-profile \
  --urls http://localhost:5080
```

Use the URL printed by the application if your local configuration selects a different address. When enabled in Development, Swagger is available at `/swagger` on that address.

**Development limitation:** Several case endpoints are intentionally guarded to run only in Development. This setup is for local development and testing, not a production deployment.

## API Overview

The case API currently includes these routes:

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/cases` | Create a case |
| `GET` | `/api/cases/{id}` | Retrieve a case |
| `GET` | `/api/cases?page=1&pageSize=20` | List cases with pagination |
| `PATCH` | `/api/cases/{id}/status` | Change case status |
| `GET` | `/api/cases/{id}/history` | Retrieve status history |

Example case-creation request:

```http
POST /api/cases
Content-Type: application/json
```

```json
{
  "title": "Road maintenance request",
  "description": "A damaged section of road requires inspection and repair."
}
```

Example status-change request:

```http
PATCH /api/cases/{id}/status
Content-Type: application/json
```

```json
{
  "newStatus": "UnderReview"
}
```

These examples illustrate the request shape; inspect Swagger and the actual request/response types for required fields, supported enum values, and validation rules. Replace `{id}` with a real case identifier when calling an endpoint.

## Testing

Run the complete test suite:

```bash
dotnet test PublicServiceCaseManagement.sln
```

Build and test without repeating restore operations when dependencies are already restored:

```bash
dotnet build PublicServiceCaseManagement.sln --no-restore
dotnet test PublicServiceCaseManagement.sln --no-build --no-restore
```

For Release configuration:

```bash
dotnet test PublicServiceCaseManagement.sln --configuration Release
```

The latest reported test result is **111 passed, 0 failed, 0 skipped**. Run the test command again to verify the current checkout after making changes.

The test suite includes domain rules, service behavior, HTTP API workflows, and authenticated organization-resolution scenarios. Selected tests use EF Core InMemory. Real PostgreSQL integration tests and a full Keycloak-to-PostgreSQL end-to-end security test remain important next steps.

## Continuous Integration

GitHub Actions is part of the project's CI setup. Check the repository's [Actions tab](https://github.com/bishalhumagain/civicflow-enterprise/actions) for the workflow configuration and latest execution result.

A successful local build and test run does not automatically prove that the remote CI workflow is passing. CI status should be reported based on an actual workflow run.

## Security and Production Readiness

CivicFlow is **not production-ready**.

Before handling real public-service or personal data, the project must complete and verify work including:

- Define and enable the intended production endpoint policy.
- Apply authentication and authorization consistently across all exposed endpoints.
- Verify organization membership and tenant isolation for every data-access path.
- Implement staff assignment authorization and audit attribution.
- Add PostgreSQL-backed integration tests and end-to-end identity tests.
- Review input validation, error handling, rate limiting, and abuse prevention.
- Establish secure secret management and production configuration.
- Add appropriate logging, monitoring, and operational alerts.
- Review data protection, retention, backup, recovery, and deployment practices.
- Perform a security review before exposing the service to real users.

The development organization context is scaffolding, not a production security boundary. Organization identifiers supplied by clients must never be trusted without server-side membership validation.

## Roadmap

- [x] Initial API and domain model
- [x] Case creation and retrieval
- [x] Paginated case listing
- [x] Domain-level status transition rules
- [x] Persistent case status history
- [x] EF Core migrations
- [x] Unit, service, and HTTP integration tests
- [x] Keycloak JWT Bearer configuration
- [x] Authenticated organization-resolution logic
- [x] Automated organization-resolution tests
- [ ] Verify latest GitHub Actions result
- [ ] Add real PostgreSQL integration tests
- [ ] Add end-to-end tests using Keycloak-issued tokens
- [ ] Finalize production endpoint and authorization policies
- [ ] Implement staff assignment and reassignment
- [ ] Add comments, internal notes, deadlines, and reporting
- [ ] Build the React and TypeScript user interface
- [ ] Add notifications and background processing
- [ ] Explore AI-assisted case triage with human oversight
- [ ] Add deployment documentation, screenshots, and a public demonstration

## Author

**Bishal Humagain**

- GitHub: [bishalhumagain](https://github.com/bishalhumagain)
- LinkedIn: [Bishal Humagain](https://www.linkedin.com/in/bishalhumagain)
- Repository: [CivicFlow Enterprise](https://github.com/bishalhumagain/civicflow-enterprise)
