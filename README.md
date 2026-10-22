# CivicFlow Enterprise

CivicFlow Enterprise is a public-service case management platform with a .NET 10 ASP.NET Core Web API backend and a React 19 + TypeScript + Vite frontend. The system provides organization-scoped case management with first-party ASP.NET Core Identity authentication (cookie-based sessions), CSRF protection, and role/organization-aware authorization.

> **Repository**: [https://github.com/bishalhumagain/civicflow-enterprise.git](https://github.com/bishalhumagain/civicflow-enterprise.git)

## 1) Current implementation status
- **Implemented**: Backend API, ASP.NET Core Identity authentication with HttpOnly cookie sessions and anti-forgery (CSRF) protection, portal-scoped sign-in (separate citizen and organization sign-in endpoints), personal access tokens (PATs) for non-browser clients, organization scoping, Cases/Staff/Organizations/Memberships features, citizen self-registration/profile (`/api/citizens/me` under the citizen policy), EF Core migrations to PostgreSQL, local file storage, and integration/unit tests in `PublicServiceCaseManagement.Tests`.
- **Implemented (frontend)**: Cookie-based authentication flows (register, email verification, login, logout, password recovery, password change), portal-scoped sign-in (citizen portal vs. organization portal), session restoration, role-specific workspaces (citizen dashboard vs. organization workspace), access-token management UI, and CSRF token handling.
- **Verified in this repository**: `dotnet test` (213 tests), `dotnet format --verify-no-changes`, and `civicflow-web` `npm run lint` + `npm run build` pass. The API flows (registration → email confirmation → login → PAT creation → bearer authentication → revocation) and the portal/role matrix (citizen and super administrator sign-in through the wrong portal, session cleanup after a rejected attempt, role-gated endpoint access) were additionally exercised against a local PostgreSQL instance over HTTP.
- **Not verified end-to-end in this environment**: live browser + PostgreSQL + SMTP end-to-end runs, and production HTTPS cookie behavior (the `__Host-` cookie prefix requires HTTPS).

## 2) Problem statement and goals
Provide a secure, organization-scoped platform for public service teams to intake, track, assign, and resolve cases while maintaining audit history and clear separation between staff and citizen access.

## 3) Features implemented today
### Backend
- **AuthN**: ASP.NET Core Identity with first-party cookie authentication. Sign-in uses `SignInManager.PasswordSignInAsync`; the session is an HttpOnly, SameSite=Lax cookie (authoritative on the server).
- **AuthN (portal-scoped sign-in)**: `POST /api/auth/login` accepts only accounts with the `citizen` role; `POST /api/auth/login/organization` accepts only `staff`/`superadmin`. A wrong-portal attempt is rejected with `403` and a machine-readable code (`not_a_citizen_account` / `not_an_organization_account`), and the session cookie that the password check had already issued is dropped again, so a rejected attempt never leaves a usable session behind.
- **AuthN (API clients)**: personal access tokens (PATs) — `Authorization: Bearer civicflow_pat_<id>_<secret>` — minted by the signed-in user, stored as a SHA-256 hash, and revoked per token. PATs resolve to the same roles and policies as the cookie session.
- **AuthZ**: Role-based policies — `CitizenClient` (`citizen`), `StaffClient` (`staff`/`superadmin`), `SuperAdmin`. The fallback policy requires an authenticated `staff` or `superadmin` user.
- **Account lifecycle**: registration, email confirmation, login, logout, resend verification, forgot/reset password, change password. Account lockout (5 attempts / 15 minutes). Password policy: minimum 12 chars with upper, lower, digit, non-alphanumeric, and 6 unique characters. `SignIn.RequireConfirmedEmail = true`.
- **Personal access tokens**: `POST/GET/DELETE /api/auth/tokens`. A token is created for the signed-in user, is shown in plaintext exactly once, is stored only as a SHA-256 hash, and can carry an expiry (or never expire). A token is rejected when unknown, tampered with, expired, revoked, or owned by a deactivated user. Maximum 25 active tokens per user.
- **Security hardening**: CSRF via ASP.NET Core anti-forgery (`X-CSRF-TOKEN` header, middleware validates unsafe `/api/*` methods), fixed-window rate limiting on auth/password endpoints, enumeration-safe registration/recovery responses, audit logging (`AUTH_SUCCESS`/`AUTH_FAILURE`/`ACCOUNT_LIFECYCLE`/`PRIVILEGE_CHANGE`), security headers middleware, and CORS restricted to configured origins with credentials.
- **Organization scoping**: Organization context resolved from the authenticated Identity user (`ClaimTypes.NameIdentifier`) plus membership and the `X-Organization-Id` header; scoping enforced server-side.
- **Cases**: status changes with history, assignments with audit attribution, document upload/download, pagination.
- **Staff**: manage staff members; provision staff accounts (Identity user + membership + staff record) atomically.
- **Organizations & Memberships**: create orgs, manage memberships/roles, toggle status.
- **Citizens**: self-service citizen profile (`GET/POST /api/citizens/me`) with validation and duplicate detection, linked to the authenticated identity.
- **Persistence**: EF Core + Npgsql; migrations include the ASP.NET Core Identity migration with a data backfill for previously Keycloak-managed users.
- **Files**: Local file storage under `App_Data/uploads`.

### Frontend
- **Cookie authentication**: All API calls use `credentials: 'include'`; no tokens or secrets are stored in JavaScript-accessible storage. State-changing requests attach the anti-forgery token from `GET /api/auth/csrf`.
- **Pages**: `LandingPage`, `StaffLoginPage` (`/admin`), `CitizenLoginPage`, `CitizenRegisterPage`, `VerifyEmailPage`, `ForgotPasswordPage`, `ResetPasswordPage`, `CitizenDashboard`, `CitizenProfilePage`, `AccessTokensPage`.
- **Routes**: `/` (public landing), `/admin` (staff/platform sign-in), `/citizen/login`, `/citizen/register`, `/citizen/verify-email`, `/forgot-password`, `/reset-password`, `/workspace` (the signed-in workspace of whichever portal the account belongs to).
- **Workspaces**: Role-specific. A citizen session opens the citizen dashboard (public intake, own profile, access tokens); a staff/superadmin session opens the organization workspace (cases, staff directory, organizations for super admins, access tokens). Navigation entries, the active view, and the API calls are derived from the roles the API reports, so a citizen is never offered an organization surface. Session restored on load via `GET /api/auth/me`; the portal of a restored session is re-derived from the account roles.
- **Notifications**: Toast system (success/error/info/warning).

## 4) Technology stack (verified)
| Layer | Technology | Version (from files) |
|---|---|---|
| Backend | .NET / ASP.NET Core | `net10.0` |
| Backend packages | Microsoft.AspNetCore.Identity.EntityFrameworkCore, EF Core Design, Npgsql EF Core, Swashbuckle | 10.0.0 / 10.0.0 / 10.0.0 / 10.3.0 |
| Database | PostgreSQL | 17 (Compose) |
| ORM | Entity Framework Core | 10.0.0 |
| Auth | ASP.NET Core Identity (cookie sessions + CSRF) | 10.0.0 |
| Frontend | React, TypeScript, Vite | 19.2.8, ~6.0.2, ^8.3.0 |
| Frontend auth | First-party fetch + cookies (no third-party auth library) | — |
| Testing | xUnit, ASP.NET Core TestHost, EF Core InMemory | 2.9.3, 10.0.0, 10.0.0 |

## 5) Repository structure
```
.
├── PublicServiceCaseManagement.Api/        # ASP.NET Core API
├── PublicServiceCaseManagement.Tests/     # xUnit tests
├── civicflow-web/                          # React + Vite frontend
├── compose.yaml                            # PostgreSQL (Docker Compose)
├── infra/                                  # Infra configs (Postgres init)
├── docs/                                   # Docs
└── PublicServiceCaseManagement.sln         # Solution
```

## 6) Backend architecture
**Projects**: `PublicServiceCaseManagement.Api` (Web API), `PublicServiceCaseManagement.Tests` (tests).

**Layers (by folders)**:
- `Domain/` — entities (`Case`, `CaseStatusHistory`, `CaseAssignmentHistory`, `CaseDocument`, `StaffMember`, `Organization`, `OrganizationMembership`, `ApplicationUser`, `CitizenProfile`, enums)
- `Features/` — feature endpoints/services/controllers (Auth, Cases, Staff, Organizations, Memberships, Citizens, Health)
- `Infrastructure/` — `Identity/` (roles, anti-forgery, security headers, seeder, email, audit log), `Organizations/` (context/resolvers/middleware), `Persistence/` (`CivicFlowDbContext`), `Files/` (storage), `Authorization/`
- `Migrations/` — EF Core migrations

**Startup (`Program.cs`)**: Configures Identity core (`AddIdentityCore` + roles + EF stores + token providers), cookie authentication (`AddIdentityCookies`), the personal access token scheme and a policy scheme that routes bearer-PAT requests to it (`CivicFlowSmart`), authorization policies, anti-forgery, rate limiting, CORS, DI for services, security headers, and controller mapping.

**Request flow**: HTTP → security headers → rate limiter → CORS → authentication (cookie, or personal access token when `Authorization: Bearer civicflow_pat_…` is present) → authorization policies → anti-forgery (unsafe `/api/*`) → organization transaction → controllers → services → DbContext/EF Core → DB → response (JSON, enums serialized as strings).

## 7) Frontend architecture
**Entry**: `main.tsx` wraps `App` in `ToastProvider`. `App.tsx` manages session state, workspace data, and actions. API calls go through `auth.ts` (`authFetch`, CSRF + cookie handling), `api.ts`, and `api-client.ts`. There are no bearer tokens in the browser session; personal access tokens exist only as server-issued credentials for external clients and are never persisted by the web app.

## 8) Authentication, authorization, security
- **Provider**: ASP.NET Core Identity (first-party). Passwords are hashed with the Identity default (PBKDF2). No external identity provider is required.
- **Session**: HttpOnly cookie. In Development the cookie name is `civicflow.auth` with `SameSite=Lax` and `SecurePolicy.SameAsRequest`; outside Development it uses the `__Host-civicflow` prefix with `SecurePolicy.Always` (requires HTTPS). Sliding expiration with an 8-hour lifetime.
- **Personal access tokens**: `Authorization: Bearer civicflow_pat_<publicId>_<secret>`. Only a SHA-256 hash of the token is stored (`personal_access_tokens` table); the plaintext is shown once at creation. The public id is used to look the row up, the hash is compared in constant time, and expiry/revocation/owner-activity are re-checked on every request. PATs carry the owning user's Identity roles, so the same authorization policies apply. They are a credential for non-browser clients only — the web app never stores them.
- **CSRF**: anti-forgery cookie (`civicflow.csrf` / `__Host-civicflow-csrf`) and the `X-CSRF-TOKEN` request header. The middleware validates unsafe methods under `/api/*`.
- **Roles**: `citizen`, `staff`, `superadmin`. Policies: `CitizenClient`, `StaffClient`, `SuperAdmin`; the fallback policy requires `staff` or `superadmin`.
- **Portal separation**: sign-in is bound to one portal — the citizen portal accepts `citizen`, the organization portal accepts `staff`/`superadmin`. The check runs server-side after the password is verified, the rejected attempt's session cookie is removed, and the frontend re-validates the issued session against the portal before it renders a workspace. An account holding both a citizen and a staff/superadmin role may use both portals.
- **Enforcement**: Organization scoping and least privilege are enforced server-side. Public registration can only create `citizen` accounts; staff provisioning (staff/organization-admin gated) can never create `superadmin`.
- **Secrets**: None in the repository. The optional seed superadmin is configured via `SeedAdmin:*` (environment/user-secrets), never hard-coded.

## 9) Database
**Engine**: PostgreSQL 17. **Migrations**: in `PublicServiceCaseManagement.Api/Migrations/`, including the `MigrateToAspNetCoreIdentity` migration that backfills existing users (`UserName`/`NormalizedEmail`, `EmailConfirmed = TRUE`, role seeding, and `citizen`/`superadmin` assignment). **DbContext**: `CivicFlowDbContext` (an `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`). Connection configured via `ConnectionStrings:CivicFlow`.

> The Identity migration assigns existing users either `citizen` or `superadmin` (based on their prior platform role) and leaves `PasswordHash` unset, so pre-existing accounts must set a password through the password-recovery flow.

## 10) API (endpoints by controller)
- `AuthController` (`/api/auth`): `GET csrf`, `POST register`, `POST login` (citizen portal), `POST login/organization` (organization portal), `POST logout`, `POST verify-email`, `POST resend-verification`, `POST forgot-password`, `POST reset-password`, `POST change-password`, `GET me`.
- `PersonalAccessTokensController` (`/api/auth/tokens`): `GET` list own tokens, `POST` create a token (returns the plaintext once), `DELETE {id}` revoke a token.
- `CasesController`: cases CRUD/operations, status, assignment, history, documents.
- `StaffController`: staff management and account provisioning (`POST /api/staff/accounts`).
- `OrganizationsController`: organization management.
- `OrganizationMembershipsController`: memberships/roles.
- `CitizensController`: `GET/POST /api/citizens/me` (citizen self-service profile).
- `HealthController`: health checks.

Login status codes: `200` success (the response reports the `portal` and the session `roles`), `401` invalid credentials (`code: invalid_credentials`), `403` email not confirmed (`code: email_not_confirmed`), `403` wrong portal (`code: not_a_citizen_account` or `code: not_an_organization_account`), `423` locked out (`code: account_locked`). Missing/expired sessions return `401`; insufficient role returns `403`. The `code` field is a stable, machine-readable reason the frontend uses (for example, to offer a "resend the verification email" action).

## 11) Local development setup
### Prerequisites
- .NET SDK 10.x
- Node.js (compatible with Vite 8 / React 19)
- PostgreSQL 17 (or Docker)

### Environment configuration (do not commit secrets)
- **API**: `appsettings.json` provides development defaults. Override `ConnectionStrings:CivicFlow`, `Frontend:BaseUrl`, `Cors:AllowedOrigins`, and (optionally) `SeedAdmin:Email`/`SeedAdmin:Password`/`SeedAdmin:FullName` via environment variables or .NET user-secrets.
- **Frontend**: `VITE_API_BASE_URL` and `VITE_CIVICFLOW_ORGANIZATION_ID` (see `civicflow-web/.env.example`).
- **Compose**: `.env` with `POSTGRES_PASSWORD`, `CIVICFLOW_RUNTIME_PASSWORD`, `CIVICFLOW_MIGRATOR_PASSWORD`.

### Steps
1. Clone the repository.
2. Start PostgreSQL: `docker compose up -d postgres` (create `.env` first from `.env.example`).
3. API: `dotnet restore`, apply migrations `dotnet ef database update --project PublicServiceCaseManagement.Api`, then `dotnet run --project PublicServiceCaseManagement.Api`.
4. Frontend: `cd civicflow-web && npm ci && npm run dev`.

> **Port note**: the API listens on `http://localhost:5000` by default. macOS ships an AirPlay Receiver that also binds port `5000`; when it is enabled the API fails to start with "address already in use". Either disable it (System Settings → General → AirDrop & Handoff → AirPlay Receiver) or run the API on another port and point the frontend at it, for example `dotnet run --project PublicServiceCaseManagement.Api --urls http://localhost:5001` together with `VITE_API_BASE_URL=http://localhost:5001` in `civicflow-web/.env.local`. The frontend origin (`http://localhost:5173`) must stay in `Cors:AllowedOrigins`; the API port does not affect CORS.
>
> `dotnet ef` does not load .NET user-secrets, so when the connection string is kept in user-secrets pass it explicitly: `dotnet ef database update --project PublicServiceCaseManagement.Api -- --connection "<connection string>"`.

### Email delivery
Verification and password-reset emails are sent via `IEmailSender`. The default `LoggingEmailSender` logs the message — including the verification/reset link — at `Information` level, so the link is visible in the API log during local development. Replace it with a real SMTP/provider implementation for production.

### Getting an organization (staff) login
Public registration only creates citizen accounts, so an organization login has to be provisioned. Every unsafe call needs the anti-forgery header (`X-CSRF-TOKEN` from `GET /api/auth/csrf`).

1. **Super administrator** — seed one with `SeedAdmin:Email`/`SeedAdmin:Password`/`SeedAdmin:FullName` (environment variables: `SeedAdmin__Email=…`, `SeedAdmin__Password=…`, `SeedAdmin__FullName=…`) before the first API start, then sign in at `/admin`. The seeder only creates the account when it does not exist yet; it never resets the password of an existing account.
2. **Organization** — `POST /api/organizations` with `{ "name": "…", "code": "…" }` (super administrator only).
3. **Organization administrator membership** — `POST /api/organizations/{organizationId}/memberships` with `{ "userId": "<user id from GET /api/auth/me>", "role": "OrganizationAdmin" }`. A platform super administrator may administer any organization.
4. **Staff login** — `POST /api/staff/accounts` with the header `X-Organization-Id: {organizationId}` and the body `{ "fullName": "…", "email": "…", "temporaryPassword": "…", "role": "CaseWorker" }`. The caller must be an organization administrator of that organization; the role is one of `Viewer`, `CaseWorker`, `OrganizationAdmin`.
5. The new account signs in at `/admin` (`POST /api/auth/login/organization`) with the temporary password.

Set `VITE_CIVICFLOW_ORGANIZATION_ID` to the created organization id so the workspace loads that organization's data.

## 12) Tests
- **Backend**: `dotnet test` from the solution root. Includes unit tests and integration tests using `WebApplicationFactory`. The auth lifecycle tests exercise the real Identity cookie flow (with a cookie container and a capturing email sender), dedicated anti-forgery tests verify CSRF rejection and acceptance, the role-scoped sign-in tests cover citizen/staff/superadmin acceptance on the matching portal, wrong-portal rejection with a stable code, session cleanup after a rejected attempt, and role-gated endpoint access, and the personal access token tests cover creation, hashing at rest, bearer authentication, role propagation, least privilege, tampering, expiry, and revocation.
- **Frontend**: `cd civicflow-web && npm run lint && npm run build`.

## 13) Troubleshooting
- **Sign-in fails**: confirm the email is verified and the API can reach PostgreSQL. The API log contains the confirmation link (`Email queued for …`).
- **"The account is not permitted to sign in"**: the email address is not confirmed yet. The sign-in form offers a "Resend the verification email" action.
- **"This account is not a citizen account" / "This account is not an organization account"**: the account was signed in through the wrong portal. Citizens sign in through the Citizen Portal (`/citizen/login`), staff and super administrators through `/admin`. The rejected attempt leaves no session behind.
- **404 from the frontend**: `VITE_API_BASE_URL` is missing or points at the Vite origin. The frontend calls the API directly, so it must be the API base URL (for example `http://localhost:5001`), and the Vite dev server must be restarted after changing it.
- **401/403 in the workspace**: the session cookie may be missing/expired (401) or the account lacks the required role for the organization (403).
- **CSRF 400 on unsafe requests**: the frontend must call `GET /api/auth/csrf` and send `X-CSRF-TOKEN`.
- **Cross-origin cookies**: add the frontend origin to `Cors:AllowedOrigins`; ensure `credentials: 'include'` (already set in `auth.ts`).
- **Migrations**: apply via EF Core tools from the API project.
- **API will not start ("address already in use")**: another process holds port 5000 (commonly the macOS AirPlay Receiver). See the port note above.

## 14) AI Developer Handoff
- **Entry points**: `PublicServiceCaseManagement.Api/Program.cs`, `civicflow-web/src/main.tsx`, `civicflow-web/src/App.tsx`.
- **Contracts/Auth**: `Features/Auth/*`, `Infrastructure/Identity/*`, `civicflow-web/src/auth.ts`, `api.ts`, `api-client.ts`.
- **Boundaries**: Organization scoping and authorization are enforced server-side; the client never supplies its own role or organization identity.
- **Invariants**: Never trust client org/role; preserve organization isolation; keep credentials out of the browser; keep CSRF protection enabled for unsafe methods.

*This README reflects the verified code state. Items marked unverified require runtime/config validation.*
