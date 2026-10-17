# CivicFlow Enterprise

**A public service case management platform built with .NET.**

CivicFlow is an in-progress portfolio project designed to demonstrate production-minded software engineering through case management, controlled workflows, audit history, multi-organization data isolation, automated testing, and continuous integration.

## Project Status

🚧 **Under active development**

The current implementation includes a .NET 10 ASP.NET Core API, PostgreSQL persistence through Entity Framework Core, case creation and retrieval, paginated case listing, domain-level status transition rules, and automated domain tests.

Features such as persistent status history, authentication, organization membership, the React frontend, AI-assisted triage, and production deployment are planned or under development. They should not be considered complete until implemented and verified.

## Goals

- Provide a clear workflow for submitting and managing public service cases.
- Enforce business rules in the domain rather than relying only on API validation.
- Record important case changes for accountability and traceability.
- Design for secure separation of data between organizations.
- Demonstrate automated testing, database migrations, and CI.
- Document architectural decisions and trade-offs.

## Technology Stack

| Area | Technology |
|---|---|
| Backend | C#, .NET 10, ASP.NET Core Web API |
| Database | PostgreSQL 17 |
| Data access | Entity Framework Core, Npgsql |
| API documentation | OpenAPI / Swagger |
| Testing | xUnit |
| Local infrastructure | Docker Compose |
| CI | GitHub Actions |
| Planned frontend | React, TypeScript |

## Current Features

- Create a case through the API.
- Retrieve a case by ID.
- List cases with pagination.
- Validate case details in the domain.
- Apply allowed case status transitions.
- Prevent editing cases that are closed or rejected.
- Run automated domain tests.

See the source code and test suite for the current implementation.

## Planned Features

- Persistent status history and audit records.
- Staff assignment, comments, and internal notes.
- Authentication and organization-based authorization.
- Service-level deadlines and reporting.
- React and TypeScript user interface.
- Background processing and notifications.
- Optional AI-assisted case triage with human oversight.
- Deployment, screenshots, and a public demonstration.

## Architecture

CivicFlow is being developed as a **modular monolith**. The initial implementation keeps the deployment simple while organizing code around business capabilities.

- `Features/` — API features and application logic.
- `Domain/` — business entities, rules, and domain concepts.
- `Infrastructure/` — persistence and external technical concerns.
- `PublicServiceCaseManagement.Tests/` — automated tests.

The architecture will evolve as the application gains real requirements; additional services and abstractions will be introduced only when justified.

## Prerequisites

Install the following before running the project:

- .NET 10 SDK
- Docker Desktop
- Git

## Run Locally

Clone the repository:

```bash
git clone https://github.com/the17biss/civicflow-enterprise.git
cd civicflow-enterprise
```

Start PostgreSQL:

```bash
docker compose up -d postgres
```

Configure the local database password in `.env` according to `compose.yaml`. Keep `.env` out of source control; use `.env.example` to document required variable names without real secrets.

Restore and build:

```bash
dotnet restore PublicServiceCaseManagement.sln
dotnet build PublicServiceCaseManagement.sln
```

Apply database migrations after confirming the project's EF Core design-time configuration:

```bash
dotnet ef database update \
  --project PublicServiceCaseManagement.Api \
  --startup-project PublicServiceCaseManagement.Api
```

Run the API:

```bash
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://127.0.0.1:5081 \
dotnet run --project PublicServiceCaseManagement.Api
```

Open Swagger at:

`http://127.0.0.1:5081/swagger`

If port `5081` is occupied, stop the conflicting process or choose another available local port.

## Run Tests

```bash
dotnet test PublicServiceCaseManagement.sln --configuration Release
```

## Continuous Integration

GitHub Actions is intended to restore dependencies, build the solution, and run tests on pushes to `main` and pull requests targeting `main`.

Check the repository's **Actions** tab for the current workflow status.

## Security Notes

This project is under development and is not yet ready for production use. Authentication, organization membership, tenant isolation, authorization policies, and production secret management must be implemented and verified before exposing sensitive case data publicly.

## Roadmap

- [x] Initial API and domain model
- [x] Case creation and retrieval
- [x] Paginated case listing
- [x] Initial automated domain tests
- [ ] Verify GitHub Actions CI
- [ ] Persistent case status history
- [ ] Authentication and organization authorization
- [ ] Frontend and user workflows
- [ ] Deployment and portfolio documentation

## Author

**Bishal Humagain**

GitHub: [@the17biss](https://github.com/the17biss)
