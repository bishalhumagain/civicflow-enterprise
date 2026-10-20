# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Citizen self-registration/profile: `CitizenProfile` domain entity with EF Core migration (`AddCitizenProfiles`)
- Citizen API endpoints (`GET/POST/PUT /api/citizens/me`) secured by the `CitizenClient` policy
- Citizen profile UI page and navigation entry, integrated with the Keycloak citizen flow
- `getAccessToken` helper and `civicflow-web/src/api/citizens.ts` API client
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

### Security
- Citizen endpoints never accept a client-supplied user ID; the profile is keyed to the authenticated `sub`/`iss` identity

### Fixed
- Lint and build issues (unused variables, Fast Refresh compatibility)
- TypeScript compilation errors

### Documentation
- Complete README rewrite with verified architecture and setup
- New User Manual with end-user workflows
- Documentation workflow and maintenance process

## [0.1.0] - 2026-10-21

### Added (Initial)
- .NET 10 ASP.NET Core Web API with Keycloak OIDC JWT auth
- PostgreSQL 17 with EF Core migrations
- React 19 + TypeScript + Vite frontend
- Organization-scoped case management
- Cases, Staff, Organizations, Memberships features
- Document upload/download with local storage
- Docker Compose setup (Postgres + Keycloak)
- Integration and unit tests (xUnit)
