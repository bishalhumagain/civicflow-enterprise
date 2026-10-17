# CivicFlow Enterprise

**A public-service case management platform built with .NET.**

CivicFlow Enterprise is an in-progress portfolio project demonstrating practical software engineering through structured case management, controlled workflows, persistent audit history, automated testing, database migrations, and continuous integration.

The project is being developed as a modular monolith with a focus on maintainability, correctness, security, and realistic public-sector workflows.

## Project Status

🚧 **Under active development**

The current implementation includes a .NET 10 ASP.NET Core Web API, PostgreSQL persistence through Entity Framework Core, case creation and retrieval, pagination, validated status transitions, persistent status history, and automated unit and HTTP integration tests.

Authentication, staff assignment, secure organization-based authorization, the frontend, AI-assisted triage, and production deployment remain future work unless explicitly marked complete below.

## Goals

- Provide a structured workflow for submitting and managing public-service cases.
- Enforce business rules in the domain layer.
- Maintain an auditable history of case status changes.
- Design for secure separation of data between organizations.
- Demonstrate automated testing, relational database design, and CI/CD practices.
- Document architectural decisions and engineering trade-offs.

## Technology Stack

| Area | Technology |
|---|---|
| Language | C# |
| Backend | .NET 10, ASP.NET Core Web API |
| Database | PostgreSQL 17 |
| Data access | Entity Framework Core, Npgsql |
| API documentation | Swagger / OpenAPI |
| Testing | xUnit, ASP.NET Core integration testing |
| Test persistence | EF Core InMemory provider for selected tests |
| Local infrastructure | Docker Compose |
| CI | GitHub Actions |
| Planned frontend | React, TypeScript |

## Current Features

- Create a case through the REST API.
- Retrieve a case by ID.
- List cases using pagination.
- Validate case details and enforce domain rules.
- Apply permitted case status transitions.
- Reject invalid status transitions.
- Prevent editing cases that are closed or rejected.
- Persist case status changes and their associated history records.
- Retrieve a case's status history through the API.
- Validate API requests and return appropriate HTTP responses.
- Run automated domain, service, and HTTP integration tests.
- Manage database schema changes through EF Core migrations.

## Case Workflow

New cases begin in the `Submitted` state. Permitted transitions are enforced by the domain model rather than being left entirely to the API layer.

The implemented workflow includes review, assignment-related status, progress, requests for information, resolution, closure, and rejection. Actual staff assignment functionality is not yet implemented.

Status history records the previous status, new status, timestamp, case ID, and organization ID. The current API does not yet establish authenticated user identity for audit attribution.

## Architecture

CivicFlow follows a **modular monolith** approach. This keeps deployment and local development straightforward while separating business rules, API features, and technical infrastructure.

```text
PublicServiceCaseManagement/
├── PublicServiceCaseManagement.Api/
│   ├── Domain/
│   │   └── Cases/
│   ├── Features/
│   │   └── Cases/
│   ├── Infrastructure/
│   │   └── Persistence/
│   ├── Migrations/
│   └── Program.cs
├── PublicServiceCaseManagement.Tests/
├── compose.yaml
├── PublicServiceCaseManagement.sln
└── README.md
```

- `Domain/` — entities, business rules, and domain concepts.
- `Features/` — API endpoints, requests, responses, and application services.
- `Infrastructure/` — database context, entity mappings, and persistence configuration.
- `Migrations/` — versioned database schema changes.
- `PublicServiceCaseManagement.Tests/` — automated tests.

The architecture will evolve with the requirements. Additional abstractions and services will be introduced when they solve a concrete problem.

## Prerequisites

Install:

- .NET 10 SDK
- Docker Desktop with Docker Compose
- Git

## Run Locally

### 1. Clone the repository

```bash
git clone https://github.com/the17biss/civicflow-enterprise.git
cd civicflow-enterprise
```

### 2. Configure the database

Create a local `.env` file using the variable names required by `compose.yaml` and set a strong local PostgreSQL password.

Keep `.env` out of source control. Use `.env.example` to document required variables without real credentials.

Start PostgreSQL:

```bash
docker compose up -d postgres
```

### 3. Configure the API

Set the `CivicFlow` connection string in your local development configuration or through an appropriate environment variable.

Ensure the connection string uses the same database name, username, password, host, and port configured for PostgreSQL.

Never commit real credentials.

### 4. Restore and build

```bash
dotnet restore PublicServiceCaseManagement.sln
dotnet build PublicServiceCaseManagement.sln
```

### 5. Apply database migrations

After confirming the local connection string and EF Core design-time configuration, run:

```bash
dotnet ef database update \
  --project PublicServiceCaseManagement.Api \
  --startup-project PublicServiceCaseManagement.Api
```

### 6. Run the API

Run the API project using Rider's Development configuration or:

```bash
dotnet run --project PublicServiceCaseManagement.Api
```

Use the actual listening URL reported by the application. Swagger is enabled in the Development environment and is available at `/swagger` on that URL.

## API Endpoints

The current case API includes:

| Method | Endpoint | Purpose |
|---|---|---|
| `POST` | `/api/cases` | Create a case |
| `GET` | `/api/cases/{id}` | Retrieve a case |
| `GET` | `/api/cases?page=1&pageSize=20` | List cases |
| `PATCH` | `/api/cases/{id}/status` | Change case status |
| `GET` | `/api/cases/{id}/history` | Retrieve status history |

Example request to create a case:

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

Use Swagger to inspect the response schemas, available enum values, and validation behavior.

**Development limitation:** The current controller uses a fixed organization ID and restricts endpoints to the Development environment. This is temporary scaffolding, not production authorization or a complete multi-tenant security implementation.

## Run Tests

Run the full test suite:

```bash
dotnet test PublicServiceCaseManagement.sln
```

For a Release configuration:

```bash
dotnet test PublicServiceCaseManagement.sln --configuration Release
```

The suite currently covers domain rules, service behavior, status-history persistence behavior, and selected HTTP API workflows. Some service and integration tests use the EF Core InMemory provider; they do not replace tests against a real PostgreSQL database.

## Continuous Integration

GitHub Actions is intended to restore dependencies, build the solution, and run tests on pushes to `main` and pull requests targeting `main`.

Check the repository's **Actions** tab to verify the current workflow and its latest result.

## Security and Production Readiness

CivicFlow is not production-ready.

Before exposing real case data, the project must implement and verify:

- Authentication and role-based authorization.
- Organization membership and tenant isolation derived from authenticated identity.
- Staff identity and assignment authorization.
- Production-grade validation, error handling, and rate limiting where appropriate.
- Secure secret management and production configuration.
- Appropriate logging, monitoring, and audit attribution.
- PostgreSQL integration testing and deployment hardening.

The fixed organization identifier is currently used only as development scaffolding. It must not be treated as a security boundary.

## Roadmap

- [x] Initial API and domain model
- [x] Case creation and retrieval
- [x] Paginated case listing
- [x] Domain-level status transition rules
- [x] Persistent case status history
- [x] EF Core migration for status history
- [x] Initial unit, service, and HTTP integration tests
- [ ] Verify GitHub Actions CI status
- [ ] Add real PostgreSQL integration tests
- [ ] Implement staff assignment and reassignment
- [ ] Add authentication and organization-based authorization
- [ ] Add comments, internal notes, deadlines, and reporting
- [ ] Build React and TypeScript user interface
- [ ] Add notifications and background processing
- [ ] Explore AI-assisted case triage with human oversight
- [ ] Add deployment documentation, screenshots, and a public demonstration

## Author

**Bishal Humagain**

- GitHub: https://github.com/the17biss
- LinkedIn: https://www.linkedin.com/in/bishal-humagain-29b3631a6/
