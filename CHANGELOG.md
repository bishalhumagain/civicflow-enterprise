# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Personal access tokens (PAT) subsystem: `PersonalAccessToken` entity + `AddPersonalAccessTokens` migration, `PersonalAccessTokenService`, and `PersonalAccessTokensController` (`GET/POST/DELETE /api/auth/tokens`). Tokens are shown once, stored only as a SHA-256 hash, support an optional expiry, and can be revoked individually.
- `PersonalAccessTokenAuthenticationHandler` plus a `CivicFlowSmart` policy scheme so `Authorization: Bearer civicflow_pat_…` authenticates alongside the session cookie and resolves to the same Identity roles and authorization policies.
- Access-token management UI (`AccessTokensPage`) in the workspace, and `list/create/revoke` client functions in `civicflow-web/src/auth.ts`.
- Machine-readable error codes on sign-in failures (`email_not_confirmed`, `invalid_credentials`) and a "resend the verification email" action in the sign-in form.

### Changed
- `LoggingEmailSender` now logs the message body — and therefore the email-confirmation and password-reset links — at `Information` level instead of `Debug`, so the links are visible in the development log with the default configuration.
- Personal access token creation and revocation are recorded through the audit log as `PRIVILEGE_CHANGE` events, so they are visible under the default audit log level.

### Added (auth)
- ASP.NET Core Identity authentication replacing Keycloak: `ApplicationUser : IdentityUser<Guid>`, `CivicFlowDbContext : IdentityDbContext`, cookie sessions, and role-based authorization
- Auth API (`/api/auth`): `csrf`, `register`, `login`, `logout`, `verify-email`, `resend-verification`, `forgot-password`, `reset-password`, `change-password`, `me`
- Anti-forgery (CSRF) protection for unsafe `/api/*` methods with the `X-CSRF-TOKEN` header
- Account lifecycle: email confirmation, password recovery/change, lockout (5 attempts / 15 minutes), and rate limiting on auth/password endpoints
- Audit logging (`AUTH_SUCCESS`/`AUTH_FAILURE`/`ACCOUNT_LIFECYCLE`/`PRIVILEGE_CHANGE`), security headers middleware, and enumeration-safe auth responses
- Staff account provisioning (`POST /api/staff/accounts`) creating the Identity user, role, membership, and staff record atomically
- Frontend cookie-authentication flows and pages (citizen login/register, verify email, forgot/reset password, staff login)
- Identity migration (`MigrateToAspNetCoreIdentity`) with data backfill for previously Keycloak-managed users
- Auth lifecycle, anti-forgery, and personal access token integration tests

### Removed
- Keycloak and all OIDC/JWT integration (backend packages, `infra/keycloak`, `scripts/verify-keycloak-token.sh`, compose service, `VITE_KEYCLOAK_*` config, and `oidc-client-ts` frontend dependency)

### Added (prior)
- Citizen self-registration/profile: `CitizenProfile` domain entity with EF Core migration (`AddCitizenProfiles`)
- Citizen API endpoints (`GET/POST/PUT /api/citizens/me`) secured by the `CitizenClient` policy
- Citizen profile UI page and navigation entry, integrated with the citizen portal flow
- `civicflow-web/src/api/citizens.ts` API client using the authenticated cookie session
- Separate **Sign in** and **Register** actions in the Citizen Portal
- Dedicated staff/platform sign-in page served at `/admin` (separate URL, no longer part of the public landing page)
- Lightweight path routing with citizen entry points `/citizen/login` and `/citizen/register`
- Citizen profile domain tests (validation, trimming, uniqueness constraints) and API integration tests (auth policy, create/update, duplicates, business-endpoint gating)
- Professional documentation system (README, USER_MANUAL, documentation workflow)
- Comprehensive repository audit and verified documentation
- Toast notification system for user feedback
- In-app branded login form (Government Officer flow)
- Responsive UI improvements
- Modular frontend architecture with extracted components and pages

### Changed
- Refactored `App.tsx` into modular components and pages
- Improved auth flow UX while maintaining server-authoritative security
- Enhanced landing page with clear journey separation
- `CitizensController` resolves the caller via `IAuthenticatedOrganizationResolver` (async, no sync-over-async) and enforces business-endpoint gating
- Citizen profile page surfaces explicit sign-in and not-authorized states
- Landing page reduced to two public portals (Citizen, Organization); platform administration moved behind the `/admin` URL

### Security
- Citizen endpoints never accept a client-supplied user ID; the profile is keyed to the authenticated Identity user
- Sessions are HttpOnly cookies with CSRF protection; no tokens or signing keys are exposed to the browser

### Fixed
- Lint and build issues (unused variables, Fast Refresh compatibility)
- TypeScript compilation errors

### Documentation
- Complete README rewrite with verified architecture and setup
- New User Manual with end-user workflows
- Documentation workflow and maintenance process

## [0.1.0] - 2026-10-21

### Added (Initial)
- .NET 10 ASP.NET Core Web API
- PostgreSQL 17 with EF Core migrations
- React 19 + TypeScript + Vite frontend
- Organization-scoped case management
- Cases, Staff, Organizations, Memberships features
- Document upload/download with local storage
- Docker Compose setup (Postgres)
- Integration and unit tests (xUnit)
