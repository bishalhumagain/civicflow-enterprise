# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
