# CivicFlow Enterprise

CivicFlow Enterprise is a public-service case management platform with a .NET 10 ASP.NET Core Web API backend and a React 19 + TypeScript + Vite frontend. The system is designed for organization-scoped case management with Keycloak-based OIDC authentication and role/claim-aware authorization.

> **Repository**: [https://github.com/bishalhumagain/civicflow-enterprise.git](https://github.com/bishalhumagain/civicflow-enterprise.git)

## 1) Current implementation status
- **Implemented and verified**: Backend API structure, JWT Bearer auth against Keycloak (dev defaults), organization scoping, Cases/Staff/Organizations/Memberships features, citizen self-registration/profile (`/api/citizens/me` under the `CitizenClient` policy), EF Core migrations to PostgreSQL, file storage abstraction, integration/unit tests in `PublicServiceCaseManagement.Tests`, Docker Compose for Postgres/Keycloak, frontend modularized with landing page and in-app login UI.
- **Partially implemented**: Citizen portal (public intake UI and self-service profile exist; full end-to-end submission/tracking flows depend on auth/roles), admin UI actions gated by auth.
- **Planned/not verified**: CI/CD workflows (no GitHub Actions visible), advanced observability, production deployment hardening.
- **Not verified end-to-end in this environment**: Full Keycloak realm setup and live external auth flows (Keycloak service exists in compose but realm configuration must be supplied).

## 2) Problem statement and goals
Provide a secure, organization-scoped platform for public service teams to intake, track, assign, and resolve cases while maintaining audit history and clear separation between staff and citizen access.

## 3) Features implemented today
### Backend
- **AuthN/AuthZ**: JWT Bearer via Keycloak, policies for staff (`azp` = `civicflow-staff`) and citizens (`azp` = `civicflow-citizen`), fallback policy requires authenticated staff client. Claims mapped without inbound claim remapping.
- **Organization scoping**: Organization context resolved from authenticated user/membership; middleware/transaction context enforce scoping.
- **Cases**: CRUD-like operations, status changes with history, assignments with audit attribution, documents upload/download, pagination.
- **Staff**: Manage staff members (create/deactivate/reactivate).
- **Organizations & Memberships**: Create orgs, manage memberships/roles, toggle status.
- **Citizens**: Self-service citizen profile (`GET/POST/PUT /api/citizens/me`) with validation, duplicate detection (email/NID/citizenship), linked to the authenticated identity; citizen endpoints require the `CitizenClient` policy and business-endpoint enablement.
- **Persistence**: EF Core + Npgsql, migrations present, row-level security migration added.
- **Files**: Local file storage under `App_Data/uploads`.
- **Health**: Health controller present.

### Frontend
- **Modular UI**: Split into `src/components/` (CaseDetails, StaffDirectory, OrganizationAdministration, CitizenIntake, StatCard, StatusPill, LoginForm, Toast) and `src/pages/` (LandingPage, CitizenProfilePage). App composed from modules.
- **Landing**: Three entry points - Citizen Portal, Organization Portal, Platform Administration. Officer flow shows branded in-app login form; auth remains server-authoritative via OIDC.
- **Auth**: `oidc-client-ts` integration, session management, login/logout.
- **Workspace**: Cases/Staff/Organizations/Public intake/Citizen profile views, filters/search/pagination, case details with history/assignments/docs.
- **Notifications**: Toast system (success/error/info/warning).
- **Responsive**: Mobile/tablet breakpoints.

## 4) Technology stack (verified)
| Layer | Technology | Version (from files) |
|---|---|---|
| Backend | .NET / ASP.NET Core | `net10.0` |
| Backend packages | Microsoft.AspNetCore.Authentication.JwtBearer, EF Core Design, Npgsql EF Core, Swashbuckle | 10.0.13 / 10.0.0 / 10.0.0 / 10.3.0 |
| Database | PostgreSQL | 17 (Compose) |
| ORM | Entity Framework Core | 10.0.0 |
| Auth | Keycloak (OIDC) | 26.4.2 (Compose) |
| Frontend | React, TypeScript, Vite | 19.2.8, ~6.0.2, ^8.3.0 |
| Frontend auth | oidc-client-ts | ^3.5.0 |
| Testing | xUnit, ASP.NET Core TestHost, EF Core InMemory | 2.9.3, 10.0.0, 10.0.0 |

## 5) Repository structure
```
.
├── PublicServiceCaseManagement.Api/        # ASP.NET Core API
├── PublicServiceCaseManagement.Tests/     # xUnit tests
├── civicflow-web/                          # React + Vite frontend
├── compose.yaml                            # Postgres + Keycloak
├── infra/                                  # Infra configs (Keycloak realm, Postgres init)
├── scripts/                                # Utility scripts
├── docs/                                   # Docs
└── PublicServiceCaseManagement.sln         # Solution
```

## 6) Backend architecture
**Projects**: `PublicServiceCaseManagement.Api` (Web API), `PublicServiceCaseManagement.Tests` (tests). Dependencies: API references packages only (no internal project references).

**Layers (by folders)**:
- `Domain/` - Entities (`Case`, `CaseStatusHistory`, `CaseAssignmentHistory`, `CaseDocument`, `StaffMember`, `Organization`, `OrganizationMembership`, `ApplicationUser`, `CitizenProfile`, enums)
- `Features/` - Feature endpoints/services/controllers (Cases, Staff, Organizations, Memberships, Citizens, Health)
- `Infrastructure/` - Authz, Orgs (context/resolvers/middleware), Persistence (DbContext), Files (storage)
- `Migrations/` - EF Core migrations

**Startup (`Program.cs`)**: Configures auth (JWT Bearer), policies (`StaffClient`, `CitizenClient`, fallback), DbContext (Npgsql), DI for services, Swagger in dev, auth middleware, controllers. Defaults in dev point to local Keycloak/DB if not configured.

**Request flow**: HTTP → Auth middleware (JWT validation) → Authorization policies → Controllers → Services → DbContext/EF Core → DB → Response (JSON, enums serialized as strings).

## 7) Frontend architecture
**Entry**: `main.tsx` wraps `App` in `ToastProvider`. `App.tsx` manages auth/session, workspace state, data fetching, actions. Modular components/pages as above. API calls via `api.ts`/`api-client.ts` with Bearer tokens and org headers. Auth via `auth.ts` (oidc-client-ts).

## 8) Authentication, authorization, security
- **Provider**: Keycloak OIDC (dev defaults `http://localhost:8080/realms/civicflow`). Tokens validated via JWT Bearer.
- **Audiences/clients**: `civicflow-api` (aud), `civicflow-staff` (azp for staff policy), `civicflow-citizen` (azp for citizen policy).
- **Enforcement**: Policies and fallback require authenticated users with specific `azp` claims. Org scoping enforced server-side.
- **Frontend**: Branded landing with in-app login form; OIDC flow initiated securely (Authorization Code). Auth remains server-authoritative.
- **Secrets**: No secrets in repo. Use env vars/placeholders.

## 9) Database
**Engine**: PostgreSQL 17. **Migrations**: Multiple migrations in `Migrations/` (initial, cases, staff, orgs, memberships/users, identity, documents, assignment audit, RLS). **DbContext**: `CivicFlowDbContext`. Connection configured via `ConnectionStrings:CivicFlow` or env.

## 10) API (verified endpoints by controllers)
- `HealthController`: health checks
- `CasesController`: cases CRUD/operations, status, assignment, history, documents
- `StaffController`: staff management
- `OrganizationsController`: org management
- `OrganizationMembershipsController`: memberships/roles
- `CitizensController`: `GET/POST/PUT /api/citizens/me` (citizen self-service profile; `CitizenClient` policy)

Auth required per policies; exact routes/roles enforced as coded.

## 11) Local development setup
### Prerequisites
- .NET SDK 10.x
- Node.js (compatible with Vite 8/React 19)
- PostgreSQL 17 or Docker
- Keycloak (or use Compose)

### Environment configuration (do not commit secrets)
API config via appsettings/env. Frontend via `VITE_*` env vars (see auth/api usage). Compose requires `.env` with passwords for Postgres/Keycloak.

### Steps
1. Clone repo
2. Start deps: `docker compose up -d postgres keycloak` (set `.env` first)
3. API: `dotnet restore`, apply migrations `dotnet ef database update` (from API project), run `dotnet run` in `PublicServiceCaseManagement.Api`
4. Frontend: `cd civicflow-web && npm ci && npm run dev`

## 12) Tests
**Run**: `dotnet test` from solution root. Tests use WebApplicationFactory, InMemory/real deps as configured, test auth handler present. **Lint/Build (frontend)**: `npm run lint && npm run build` in `civicflow-web` (both pass currently).

## 13) Troubleshooting
- **Auth fails**: Check Keycloak authority/audience/client IDs and tokens.
- **DB connection**: Verify connection string and Postgres running.
- **CORS**: Configure as needed.
- **Migrations**: Apply via EF Core tools.

## 14) AI Developer Handoff
- **Entry points**: `PublicServiceCaseManagement.Api/Program.cs`, `civicflow-web/src/main.tsx`, `civicflow-web/src/App.tsx`
- **Contracts/Auth**: `Program.cs` policies, `Features/*` controllers/services, `auth.ts`, `api.ts`/`api-client.ts`
- **Boundaries**: Organization scoping enforced server-side; auth server-authoritative (Keycloak); citizen endpoints keyed to the authenticated `sub`/`iss` identity
- **Run**: API (`dotnet run`), Frontend (`npm run dev`), Tests (`dotnet test`)
- **Invariants**: Never trust client org/role; validate JWTs; preserve org isolation; never trust a client-supplied citizen user ID
- **Gaps**: Full Keycloak realm config, citizen portal completion (submission/tracking), CI/CD
- **Next task**: Configure Keycloak realm/clients and test end-to-end auth; ensure UI role enforcement matches backend policies

*This README reflects verified code as of inspection. Areas marked unverified require runtime/config validation.*
