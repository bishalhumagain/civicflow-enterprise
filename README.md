# CivicFlow Enterprise

**A secure, multi-tenant public-service case management platform.**

CivicFlow Enterprise helps government and public-service organizations intake, track, assign, and resolve citizen-submitted cases. It is built with a .NET 10 ASP.NET Core Web API backend, a React 19 + TypeScript + Vite frontend, and PostgreSQL 17 for persistence. The system uses first-party ASP.NET Core Identity for authentication (cookie-based sessions with CSRF protection), supports personal access tokens for API clients, and enforces organization-scoped data isolation through server-side authorization and PostgreSQL row-level security.

> **Repository**: [github.com/bishalhumagain/civicflow-enterprise](https://github.com/bishalhumagain/civicflow-enterprise.git)
> **Owner**: Bishal Humagain
> **Version**: 0.1.0
> **Status**: In active development — see [Implementation Status](#implementation-status)

---

## Table of Contents

- [Overview](#overview)
- [Who Is This For?](#who-is-this-for)
- [Key Capabilities](#key-capabilities)
- [Technology Stack](#technology-stack)
- [Repository Structure](#repository-structure)
- [Architecture](#architecture)
  - [Backend Architecture](#backend-architecture)
  - [Frontend Architecture](#frontend-architecture)
  - [Authentication & Authorization](#authentication--authorization)
  - [Database & Data Isolation](#database--data-isolation)
  - [Security Model](#security-model)
  - [Background Jobs & Notifications](#background-jobs--notifications)
  - [API Reference](#api-reference)
- [Getting Started](#getting-started)
- [Testing](#testing)
- [Deployment & CI](#deployment--ci)
- [Documentation](#documentation)
- [Troubleshooting](#troubleshooting)
- [Implementation Status](#implementation-status)
- [Contributing](#contributing)
- [License](#license)

---

## Overview

CivicFlow Enterprise is a public-service case management platform designed for government departments and public-service agencies. It provides three separate, secure portals:

1. **Citizen Portal** — Citizens register, verify their email, maintain a civil-registry profile, submit public service requests, and track their own requests with search, filters, paging, a citizen-visible timeline, public updates, information requests and responses, and permitted documents.
2. **Organization Portal** — Staff members manage cases within their organization: create cases, change status through a state machine, assign to staff, upload documents, manage staff directories, and send/revoke invitations.
3. **Platform Administration** — Super administrators manage all organizations, view the platform directory, review the audit trail, track application errors, and monitor dashboard statistics.

The platform is built around three core principles:
- **Security by design** — No secrets in the browser, no temporary passwords, CSRF protection, rate limiting, audit everything.
- **Organization scoping** — All case data is tenant-scoped with server-side enforcement and PostgreSQL row-level security.
- **Role-based access** — Three server-validated platform roles (`citizen`, `staff`, `superadmin`) with strict portal separation, plus organization-scoped `Viewer`, `CaseWorker`, and `OrganizationAdmin` memberships.

---

## Who Is This For?

| Audience | What you'll find useful |
|---|---|
| **New developers** | [Getting Started](#getting-started), [Architecture](#architecture), [API Reference](#api-reference), [Testing](#testing) |
| **Project managers** | [Key Capabilities](#key-capabilities), [Implementation Status](#implementation-status), [Delivery Roadmap](docs/DELIVERY-ROADMAP.md) |
| **System administrators / operators** | [Getting Started](#getting-started), [Security Model](#security-model), [Troubleshooting](#troubleshooting), [Deployment & CI](#deployment--ci) |
| **End users** | [User Manual](docs/USER_MANUAL.md) |
| **Security reviewers** | [Authentication & Authorization](#authentication--authorization), [Database & Data Isolation](#database--data-isolation), [RLS Design](docs/RLS-DESIGN.md), [Integration Verification](docs/INTEGRATION-VERIFICATION.md) |
| **Contributors** | [Contributing](#contributing), [Documentation Workflow](docs/DOCUMENTATION_WORKFLOW.md), [CHANGELOG](CHANGELOG.md) |

---

## Key Capabilities

### Authentication & Identity

| Capability | Description |
|---|---|
| **First-party ASP.NET Core Identity** | Cookie-based sessions (HttpOnly, SameSite=Lax, 30-minute inactivity timeout with sliding renewal). No external identity provider required. |
| **Portal-scoped sign-in** | Three exclusive endpoints: citizen (`/api/auth/login`), organization (`/api/auth/login/organization`), and platform-admin (`/api/auth/login/platform-admin`). Wrong-portal attempts are rejected with machine-readable codes and no leftover session. |
| **Email verification** | Registration requires email confirmation before sign-in is permitted (`RequireConfirmedEmail = true`). |
| **Password policy** | 12+ characters, upper/lower/digit/non-alphanumeric, 6 unique characters. Account lockout after 5 failed attempts (15-minute window). |
| **Password recovery** | Forgot-password/reset-password flow with enumeration-safe responses (no disclosure of whether an email exists). Accounts migrated from the former external identity provider have no local password hash; they must set a local password through this flow. |
| **Personal access tokens (PATs)** | `Authorization: Bearer civicflow_pat_<id>_<secret>` for non-browser clients. Stored as SHA-256 hash, shown once at creation, revocable, optional expiry, max 25 per user. PATs carry the owner's roles and policies. |
| **CSRF protection** | Anti-forgery cookie + `X-CSRF-TOKEN` header validated on all unsafe `/api/*` methods. |
| **Rate limiting** | Fixed-window: 20/min on auth endpoints, 8/5min on password endpoints. |

### Case Management

| Capability | Description |
|---|---|
| **Case lifecycle** | Create, list (paged, searchable, filterable), view detail, change status, and assign with organization and per-case role enforcement. Organization admins see their organization's cases; staff see only cases assigned to them. |
| **Case editing and deletion** | Assigned CaseWorkers and organization administrators can update editable case details; organization administrators alone can permanently delete a case after an explicit confirmation. |
| **Status state machine** | Submitted → UnderReview → Assigned → InProgress → AwaitingInformation → Resolved → Closed/Rejected. Enforced in domain logic; closed/rejected cases are not editable. |
| **Assignment** | Organization administrators assign cases to active staff in the same organization as CaseWorker or Viewer; assignment and role changes are audited. Viewers have read-only case access. A newly submitted citizen case is triaged to an organization administrator automatically (assigned to an admin staff member when one exists, otherwise every active admin is notified). |
| **Citizen intake** | Citizens choose an active organization and submit a case with an optional attachment of any file type up to 10 MB. The submitting identity is derived from the session, and the new case is routed to an organization administrator for triage. |
| **Citizen tracking** | Owner-scoped, paged request list with search, status filtering, and sorting; detail with reference, organization, status, timestamps, citizen-visible status timeline, public updates, information requests and responses, and citizen-visible documents. Internal notes and internal-only documents are excluded server-side. |
| **Citizen communication** | Staff can publish a public update, request additional information (moving the case to `AwaitingInformation`), or keep an internal note. A citizen response returns an `AwaitingInformation` case to `UnderReview`, records a visible timeline entry, and notifies the assigned staff member. Status explanations are citizen-visible only when staff mark them so. |
| **Status & assignment history** | Full audit trail of every status change and assignment, with timestamps, actors, and per-entry citizen visibility. |
| **Document upload/download** | Multipart uploads of any file type up to 10 MB, SHA-256 hash verification, path-traversal protection, attachment-only downloads, and an explicit `visibleToCitizen` flag that defaults to internal. |

### Organization Management

| Capability | Description |
|---|---|
| **Multi-tenant scoping** | All case data is organization-scoped. Server-side resolution + PostgreSQL RLS prevents cross-organization access. |
| **Organization CRUD** | Super administrators create, rename, activate/deactivate, and delete organizations. Deletion is refused while cases exist. Duplicate codes and invalid changes are reported inline. Every organization is assigned a type; when none is chosen, the seeded `Uncategorized` type is applied. |
| **Organization types** | Super administrators manage a shared catalogue of organization types (`OrganizationType`) from the *Organization types* tab. Types can be created, renamed, re-coded, deactivated, reactivated, and deleted; `Uncategorized` is protected, and a type in use by any organization cannot be deleted. Organizations list and detail responses include `organizationTypeId` and `organizationTypeName`. |
| **Memberships** | Organization administrators open the Organizations view for the organizations they administer, then manage roles and activation but cannot grant OrganizationAdmin; only platform administrators can provision organization admins. Platform administrators can filter the members view by organization type and organization. |
| **Invitation-based onboarding** | Organization administrators invite staff; acceptance grants the Staff portal role and a CaseWorker organization membership. Single-use, expiring links (default 72h, configurable 1–720h); invitees choose their own password. Failed email delivery is recorded on the invitation and can be retried without invalidating the link. |
| **Phone numbers** | Optional on staff and invitations (6–30 chars, at least 6 digits). Never overwrites a number an existing account already has. Correctable by organization administrators and platform administrators. |

### Audit & Compliance

| Capability | Description |
|---|---|
| **Persisted audit trail** | Every authentication, account, invitation, membership, organization, staff, case, and access-token event is recorded in `audit_events` with actor (id + email), organization, entity, and detail. Asynchronous and fail-safe. |
| **Platform-wide audit log** | `GET /api/platform/audit` (super administrators only), filterable by category, action, free text, and date range. |
| **Organization-scoped activity log** | `GET /api/organizations/{orgId}/audit`: organization administrators see their organization's events; CaseWorkers and Viewers see only events they performed. |
| **Dashboard & statistics** | Platform-wide and organization-scoped case statistics: total, per-status, per-priority, weekly/monthly trends. |
| **Error tracking** | Unhandled exceptions persisted to `application_errors` with full context (stack trace, source, request path/method, user, org). Reviewable and mark-as-handled by super administrators. |

### Platform Administration

| Capability | Description |
|---|---|
| **Platform directory** | Overview counters (accounts, staff, organizations, invitations) plus three focused sections: Citizens with editable profiles, Staff accounts with editable contact details, and All Users with platform-role controls and organization links. Membership and invitation management live in the Organizations view. |
| **Organization management** | Full CRUD, including deletion with cleanup of memberships, staff records, and pending invitations. |
| **Super-admin bootstrap** | Configurable seed with fail-fast validation on partial/policy-violating configuration. Explicit `ResetPassword` switch for one-time repair. |

### Frontend

| Capability | Description |
|---|---|
| **Cookie-based auth** | All API calls use `credentials: 'include'`. No tokens or secrets in JavaScript-accessible storage. |
| **Role-specific workspaces** | Citizen dashboard, organization workspace, and platform administration — navigation derived from API-reported roles. |
| **Session restoration** | `GET /api/auth/me` on load; portal re-derived from roles. |
| **Complete auth flows** | Register, email verification, login (portal-scoped), logout, password recovery, password change. |
| **Toast notifications** | Success/error/info/warning feedback for all actions. |
| **Invitation management UI** | Send, resend, revoke, list (pending/accepted/expired/revoked). |
| **Audit log UI** | Shared component with category/action/text/date-range filters and pagination. |
| **Citizen tracking** | Searchable/filterable/sortable request list with paging, a detail view with a citizen-visible timeline, information-request responses, document downloads, and loading/error/retry/empty states. |
| **Profile management** | Unified self-service profile for citizens and staff. |
| **Theme and branding** | Complete light and dark themes with a persisted preference and system-theme fallback. The public and workspace headers use the configured application name, and every footer is generated from that same value. |
| **Validation and formatting** | Shared backend and frontend rules for trimming, canonical email/identifier casing, phone shape, field bounds, file signatures, accessible field errors, and locale/time-zone-aware display formatting. See [Validation & Formatting](docs/VALIDATION-AND-FORMATTING.md). |

---

## Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Backend runtime | .NET / ASP.NET Core | 10.0 (`net10.0`) |
| ORM | Entity Framework Core | 10.0.0 |
| Database | PostgreSQL | 17 |
| Database provider | Npgsql EF Core | 10.0.0 |
| Auth | ASP.NET Core Identity (cookie sessions + CSRF) | 10.0.0 |
| API documentation | Swashbuckle (Swagger) | 10.3.0 |
| Frontend framework | React | 19.2.8 |
| Frontend language | TypeScript | ~6.0.2 |
| Frontend build | Vite | ^8.3.0 |
| Frontend auth | First-party `fetch` + cookies (no third-party auth library) | — |
| Testing | xUnit, ASP.NET Core TestHost, EF Core InMemory; Playwright + Chromium browser E2E; opt-in disposable PostgreSQL RLS tests | 2.9.3, 10.0.0, 10.0.0; 1.64.1; Npgsql 10.0.0 |
| Code coverage | coverlet | 6.0.4 |
| CI | GitHub Actions (.NET 10, Node 20) | — |
| Containerization | Docker Compose (PostgreSQL) | — |

---

## Repository Structure

```
.
├── PublicServiceCaseManagement.sln          # .NET solution file
├── PublicServiceCaseManagement.Api/         # ASP.NET Core Web API
│   ├── Program.cs                           # Startup configuration
│   ├── appsettings.template.json           # Configuration template (copy to appsettings.json)
│   ├── Domain/                              # Entities (Case, Organization, ApplicationUser, etc.)
│   ├── Features/                            # Controllers + services + contracts
│   │   ├── Auth/                            #   Authentication, PATs, invitations
│   │   ├── Cases/                           #   Case CRUD, documents, history
│   │   ├── Citizens/                        #   Citizen self-service profile
│   │   ├── Dashboard/                       #   Statistics
│   │   ├── Errors/                          #   Application error tracking
│   │   ├── Memberships/                     #   Organization memberships
│   │   ├── Organizations/                   #   Organization CRUD
│   │   ├── Platform/                        #   Platform directory & overview
│   │   ├── Profiles/                        #   Unified self-service profile
│   │   └── Staff/                          #   Staff management
│   ├── Infrastructure/                      # Cross-cutting concerns
│   │   ├── Audit/                           #   Audit trail writer
│   │   ├── Authorization/                   #   Business endpoint access gate
│   │   ├── Errors/                          #   Global exception handler, error logger
│   │   ├── Files/                           #   Local file storage
│   │   ├── Identity/                        #   Roles, anti-forgery, security headers, seeder, PAT auth handler
│   │   ├── Organizations/                   #   Org context, resolvers, transaction middleware
│   │   └── Persistence/                    #   DbContext, design-time factory
│   └── Migrations/                         # 28 EF Core migrations
├── PublicServiceCaseManagement.Tests/       # xUnit test project (492 tests; 3 opt-in PostgreSQL RLS)
├── civicflow-web/                           # React + Vite frontend
│   ├── e2e/                                # Playwright browser journeys + disposable DB seeding
│   ├── playwright.config.ts                # Serial Chromium E2E runner (API + Vite)
│   ├── src/
│   │   ├── App.tsx                         # Session, routing, workspace
│   │   ├── auth.ts                        # authFetch, CSRF, login flows
│   │   ├── api.ts                         # API request wrapper (org header)
│   │   ├── api-client.ts                  # JSON request helper
│   │   ├── domain.ts                       # TypeScript types
│   │   ├── pages/                          # Page components
│   │   ├── components/                    # Shared components
│   │   └── api/                            # API modules (citizens, profile)
│   └── .env.example
├── docs/                                   # Supplementary documentation
├── infra/postgres/init/                    # PostgreSQL role initialization
├── .github/workflows/ci.yml                # CI pipeline
├── compose.yaml                            # Docker Compose (PostgreSQL)
├── CHANGELOG.md                            # Release history
├── CIVICFLOW-PROVENANCE.json               # Project identity metadata
└── README.md                               # This file
```

---

## Architecture

### Backend Architecture

The backend follows a **feature-based** organization with clear layering:

**Layers (by folder)**:

| Folder | Responsibility |
|---|---|
| `Domain/` | Entities and domain logic: `Case` (state machine, editability), `CaseStatus`, `CasePriority`, `CaseStatusHistory`, `CaseAssignmentHistory`, `CaseDocument`, `CaseUpdate` (`PublicUpdate`, `InformationRequest`, `InternalNote`, `CitizenResponse`), `Organization`, `OrganizationType`, `WellKnownOrganizationTypes`, `OrganizationMembership`, `OrganizationRole`, `AccountInvitation`, `ApplicationUser`, `CitizenProfile`, `PersonalAccessToken`, `PlatformRole`, `StaffMember`, `AuditEvent`, `ApplicationError`, `CaseStatistics`, `PhoneNumbers` |
| `Features/` | Controllers + service contracts + service implementations, organized by feature area |
| `Infrastructure/` | Cross-cutting: `Identity/` (roles, PAT auth handler, anti-forgery, security headers, seeder, email sender, audit logger), `Organizations/` (context, resolvers, middleware), `Persistence/` (DbContext, factory), `Files/` (local storage), `Errors/` (global handler, error logger), `Audit/` (audit trail writer), `Authorization/` (business endpoint gate) |
| `Migrations/` | EF Core migrations, including persistent background jobs and notifications |

**Startup (`Program.cs`)** configures in order:
1. Identity core (password policy, lockout, email confirmation)
2. Authentication (cookie sessions + PAT scheme + `CivicFlowSmart` policy scheme)
3. Cookie configuration (HttpOnly, SameSite, Secure, `__Host-` prefix in production)
4. Authorization policies (`CitizenClient`, `StaffClient`, `SuperAdmin`, `ProfileClient`, fallback)
5. Anti-forgery
6. Rate limiting (auth: 20/min, password: 8/5min)
7. CORS (configured origins)
8. DI registrations (all services)
9. Middleware pipeline: SecurityHeaders → GlobalExceptionHandler → RateLimiter → CORS → Authentication → Authorization → Antiforgery → OrganizationTransaction → Controllers
10. Identity seeding at startup

**Request flow**:
```
HTTP request
  → Security headers middleware
  → Global exception handler
  → Rate limiter
  → CORS
  → Authentication (cookie OR PAT via CivicFlowSmart policy scheme)
  → Authorization policies
  → Anti-forgery validation (unsafe /api/* methods)
  → Organization transaction middleware (sets RLS context)
  → Controller → Service → DbContext/EF Core → PostgreSQL
  → Response (JSON, enums serialized as strings)
```

### Frontend Architecture

**Entry point**: `main.tsx` wraps `App` in `ToastProvider`.

**Session management** (`App.tsx`): Restores session on load via `GET /api/auth/me`, re-derives portal from roles, renders the appropriate workspace.

**API layer** (no third-party HTTP libraries):

| File | Role |
|---|---|
| `auth.ts` | `authFetch` wraps `fetch` with `credentials: 'include'`, CSRF header on unsafe methods, and automatic CSRF retry keyed on the server's stable `antiforgery_invalid` code. `login` clears any token cached by a previous user before signing in, re-validates portal, and logs out on mismatch. |
| `api.ts` | `apiRequest` wraps `authFetch` with `X-Organization-Id` header. |
| `api-client.ts` | `requestJson<T>` wraps `apiRequest` with JSON parsing and `ApiError` class (status + code). |
| `api/citizens.ts` | Citizen profile API client. |
| `api/profile.ts` | Unified profile API client. |
| `api/organizations.ts` | Organization CRUD client (create/update optionally carry `organizationTypeId`). |
| `api/organization-types.ts` | SuperAdmin organization-type catalogue client. |

The organization workspace obtains active memberships from `GET /api/organizations/me`. The UI selects a membership and sends its ID as `X-Organization-Id`; the API validates the authenticated user's active account, membership, and organization on every scoped request. The organization ID is not configured in the frontend build environment.

**Routing**:

| Route | Page |
|---|---|
| `/` | Public landing page |
| `/admin` | Organization (staff) sign-in |
| `/super-platform-admin` | Platform-admin sign-in (intentionally unlinked) |
| `/setup-password` | Invitation redemption |
| `/citizen/login` | Citizen sign-in |
| `/citizen/register` | Citizen registration |
| `/citizen/verify-email` | Email verification |
| `/forgot-password` | Password recovery (email) |
| `/reset-password` | Password reset |
| `/workspace` | Authenticated workspace (role-specific) |

**Workspaces** (role-specific, navigation derived from API-reported roles):
- **Citizen**: Dashboard, public intake, profile, access tokens
- **Staff**: Cases, team contacts, activity log, access tokens; OrganizationAdmin also sees Organizations for administered organizations
- **SuperAdmin**: Organizations (three inner tabs: organization types, organizations, and members + invitations with a type/organization filter), directory, audit log, error tracker, background jobs, access tokens

**Key components**: `OrganizationAdministration`, `InvitationManager`, `StaffDirectory`, `PlatformDirectory`, `PlatformErrors`, `AuditLog`, `CaseDetails`, `CitizenDashboard`, `ProfilePage`, `StatCard`, `StatusPill`, `Toast`.

### Validation, Normalization & Display Conventions

The same rules are applied at every layer. Request DTOs carry DataAnnotations, domain factories enforce invariants, and PostgreSQL CHECK constraints added by `AddValidationConstraints` protect the critical bounds from direct writes. `Domain/TextNormalization.cs`, `Domain/PhoneNumbers.cs`, and `Infrastructure/Validation/ValidationAttributes.cs` are the backend source of truth; `utils/validation.ts` and `utils/format.ts` mirror them for immediate, accessible form feedback. The server always revalidates, so client normalization is never treated as a security control. Display formatting uses the explicit `en-GB` locale and `Asia/Kathmandu` time zone, binary file-size units, upper-case references, and the `—` fallback for missing values. Full field limits, upload signature rules, and the change checklist live in [Validation & Formatting](docs/VALIDATION-AND-FORMATTING.md).

### Authentication & Authorization

#### Authentication Methods

| Method | Header | Use case |
|---|---|---|
| **Cookie session** | (automatic, HttpOnly) | Browser-based SPA |
| **Personal access token** | `Authorization: Bearer civicflow_pat_<publicId>_<secret>` | API clients, scripts, integrations |

The `CivicFlowSmart` policy scheme inspects the `Authorization` header and routes to the PAT handler when `civicflow_pat_` is present, otherwise to the Identity cookie scheme.

#### Portal-Scoped Sign-In

Each sign-in endpoint accepts only one account type:

| Endpoint | Accepts | Rejects with |
|---|---|---|
| `POST /api/auth/login` | `citizen` | `not_a_citizen_account` (403) |
| `POST /api/auth/login/organization` | `staff` | `not_an_organization_account` (403), `use_platform_admin_portal` (403) |
| `POST /api/auth/login/platform-admin` | `superadmin` | `not_a_platform_admin` (403) |

A rejected attempt's session cookie is dropped — no usable session is left behind.

#### Authorization Policies

| Policy | Required role(s) | Scope |
|---|---|---|
| `CitizenClient` | `citizen` | Citizen endpoints |
| `StaffClient` | `staff` or `superadmin` | Staff/organization endpoints |
| `SuperAdmin` | `superadmin` | Platform administration |
| `ProfileClient` | `citizen` or `staff` | Unified profile (excludes superadmin) |
| Fallback | authenticated + `staff`/`superadmin` | All unspecified endpoints |

#### Organization Scoping

1. User authenticates (cookie or PAT).
2. `GET /api/organizations/me` returns only the caller's active organization memberships; the user selects a membership in the workspace.
3. `AuthenticatedOrganizationResolver` treats `X-Organization-Id` only as a requested membership selector and verifies the user, active account, active membership, and active organization.
4. `OrganizationTransactionContext` sets PostgreSQL `set_config('civicflow.organization_id', ..., true)`.
5. RLS policies enforce isolation on tenant-owned tables.
6. `OrganizationTransactionMiddleware` completes/rolls back the transaction context.

#### Invitation-Based Onboarding

No administrator ever handles another person's password:

1. Organization administrator sends `POST /api/organizations/{orgId}/invitations` with name, email, the staff role, and optional phone. Organization administrators cannot invite or promote another OrganizationAdmin; platform administrators can provision that role.
2. API stores only a SHA-256 hash of a 256-bit random token; emails a single-use `/setup-password?token=...` link.
3. Invitee looks up the link anonymously (`POST /api/auth/invitations/lookup`), sees the inviting org, their name, email, role, and expiry.
4. Invitee chooses their own password (validated against policy); acceptance creates/links the Identity account, grants the `staff` portal role, creates a CaseWorker organization membership and staff record.
5. Links are single-use, expiring (default 72h, configurable 1–720h), and can be resent (invalidates old link) or revoked. `superadmin` is never invitable.

### Database & Data Isolation

**Engine**: PostgreSQL 17

**Database roles** (created by `infra/postgres/init/001-create-application-roles.sh`):
- `civicflow_migrator` — CREATE privilege (for EF Core migrations only)
- `civicflow_runtime` — DML only (no DDL), used by the running API

**Tables** (19 application + 6 identity):

| Table | Purpose | RLS? |
|---|---|---|
| `cases` | Service requests/issues | Yes |
| `case_status_history` | Status change audit trail | Yes |
| `case_assignment_history` | Assignment audit trail | Yes |
| `case_documents` | Uploaded case documents | Yes |
| `case_updates` | Public updates, information requests, internal notes, and citizen responses | Yes |
| `staff_members` | Organization staff | Yes |
| `organizations` | Tenant organizations | No |
| `organization_types` | Shared organization-type catalogue (SuperAdmin-managed) | No |
| `organization_memberships` | User-organization associations | No |
| `account_invitations` | Pending staff invitations | No (anonymous redemption) |
| `application_users` | Identity users | No |
| `citizen_profiles` | Civil registry profiles | No |
| `personal_access_tokens` | PAT hashes | No |
| `audit_events` | Audit trail | No (scoped in query) |
| `application_errors` | Error tracking | No |

**Row-Level Security** (see [RLS-DESIGN.md](docs/RLS-DESIGN.md)):

RLS is enabled and forced on `cases`, `case_status_history`, `case_assignment_history`, `case_documents`, `case_updates`, and `staff_members`. The runtime role cannot bypass it. Staff requests read the transaction-local `civicflow.organization_id` setting; citizen tracking requests additionally set `civicflow.user_id`, which gates visible updates, history, documents, and the assigned staff row to the submitting citizen's own case:

```sql
CREATE POLICY cases_organization_isolation ON cases
USING (current_setting('civicflow.organization_id', true) = "OrganizationId"::text)
WITH CHECK (current_setting('civicflow.organization_id', true) = "OrganizationId"::text);
```

**Migrations**: 28 EF Core migrations, including `AddPersistentBackgroundJobsAndNotifications`, `AddBackgroundJobConcurrencyVersion`, `AddOrganizationTypes`, `AddCitizenCaseTrackingRls`, `AddCitizenCaseTrackingUpdates`, `AddCitizenTrackingRls`, `AddValidationConstraints`, and `AlignCaseTextMinimums` for the durable queue, notification outbox, attempt history, job concurrency protection, the platform-owned organization-type catalogue, the citizen update/timeline privacy and ownership policies, and the application-wide database validation bounds.

### Security Model

| Layer | Implementation |
|---|---|
| **Password hashing** | ASP.NET Core Identity default (PBKDF2) |
| **Password policy** | 12+ chars, upper/lower/digit/non-alphanumeric, 6 unique chars |
| **Lockout** | 5 failed attempts → 15-minute lockout |
| **Email confirmation** | Required before sign-in |
| **Session cookie** | HttpOnly, SameSite=Lax, 8h sliding. Dev: `civicflow.auth`, prod: `__Host-civicflow` (requires HTTPS) |
| **CSRF** | Anti-forgery cookie + `X-CSRF-TOKEN` header, validated on unsafe `/api/*` |
| **Rate limiting** | Auth: 20/min, Password: 8/5min |
| **Security headers** | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'none'`, COOP/COEP/CORP `same-origin`, `Permissions-Policy` |
| **CORS** | Restricted to configured origins with credentials |
| **PAT storage** | SHA-256 hash only; plaintext shown once; constant-time comparison; max 25 per user |
| **Invitation tokens** | SHA-256 hash of 256-bit random token; single-use; expiring |
| **Citizen tracking ownership** | Citizen routes derive the user from the validated session and constrain every query to `SubmittedByUserId`. PostgreSQL owner-scoped RLS clauses additionally restrict visible updates, status history, documents, and assigned-staff resolution to the citizen's own case; cross-citizen identifiers return `404`. |
| **Secrets** | `appsettings.json` gitignored; `appsettings.template.json` documents keys; seed admin via environment/user-secrets |
| **Audit logging** | All auth, account, invitation, membership, organization, staff, case, and token events persisted asynchronously and fail-safe |
| **Error tracking** | Unhandled exceptions persisted with full context; reviewable by super admins |
| **Enumeration safety** | Registration and password-recovery responses do not disclose whether an email exists |
| **Input validation** | Server-authoritative DataAnnotations plus domain invariants and 27 PostgreSQL CHECK constraints; shared frontend validators provide accessible feedback only. Uploaded files of declared PDF/PNG/JPEG/GIF types must match their magic signature; other declared types remain allowed because the contract is arbitrary-file. |

---

## Background Jobs & Notifications

CivicFlow uses EF Core and PostgreSQL as its durable queue and transactional outbox; it does not add Redis or a separate broker. `background_jobs`, `background_job_attempts`, and `notifications` are created by `AddPersistentBackgroundJobsAndNotifications`. The domain change, in-app notification, and email job are written in the same EF unit of work. The notification is independently durable, so an unavailable email relay cannot erase the in-app item.

The hosted `BackgroundJobWorker` claims work with a PostgreSQL `FOR UPDATE SKIP LOCKED` update and a time-limited lease. Only the registered `notification.email` job is accepted; its payload contains a notification ID, never an address, password, bearer token, invitation secret, or arbitrary executable data. Job states are `Queued → Running → Succeeded`, `RetryScheduled`, or `Failed`; queued and retry-scheduled work can also be `Cancelled`. A lease-expired job is recovered on a later poll. Transient email failures use exponential delay (default base 30 seconds, capped at one hour) with 0.75–1.25 jitter and a default maximum of five attempts (clamped to 1–20). Failed work remains inspectable until retention cleanup. A SuperAdmin retry is audited, retains monotonic attempt history, and can extend the total attempt ceiling only up to 20.

Email is at-least-once: a worker crash after SMTP accepts a message but before success is committed can cause a duplicate on lease recovery. In-app notifications have unique idempotency keys; event IDs are used for case status and assignment notifications. The implemented events are case submission (to the authenticated submitter), case assignment (to the active account with an active membership matching the assigned staff record), case status changes (to the case submitter and to the staff member currently assigned to the case, whether `CaseWorker` or read-only `Viewer`), citizen-visible public updates and information requests (to the submitter; internal notes notify nobody), and citizen responses (to the active account linked to the assigned staff record). A newly submitted citizen case is also triaged to an organization administrator: when an active admin has a matching staff record the case is assigned to the earliest such admin (notifying them through the case-assignment event), and otherwise every active `OrganizationAdmin` receives a triage notification while the case stays unassigned. Messages are generic and do not reveal case details, internal notes, or document contents. Invitation lifecycle mail remains on the existing invitation email flow; invitations are not stored as background jobs because their single-use secret is intentionally not persisted. Staff-membership and organization administration events do not currently create notifications.

Authenticated users can list their own notifications with `GET /api/notifications?page=1&pageSize=20&unreadOnly=false`, read their unread count at `GET /api/notifications/unread-count`, mark one read with `PATCH /api/notifications/{id}/read`, or mark their own unread notifications read with `POST /api/notifications/read`. All reads and writes are recipient-scoped. The notification panel in the workspace refreshes the count every 60 seconds.

Only the `superadmin` role can access `GET /api/platform/jobs` and `GET /api/platform/jobs/{id}` or the audited, rate-limited `POST /api/platform/jobs/{id}/cancel` and `/retry` actions. Responses contain job metadata and sanitized errors, never payload JSON, stack traces, or secrets. Organization administrators, staff, and citizens cannot inspect or operate jobs. The platform workspace includes a status-filtered job monitor; endpoint authorization is enforced by the API, not the UI.

| Setting (environment variable form) | Default | Purpose |
|---|---:|---|
| `BackgroundJobs__Enabled` | `true` | Enable the hosted worker; queued rows remain durable when disabled. |
| `BackgroundJobs__Concurrency` | `2` | Worker claims per poll (clamped to 1–16). |
| `BackgroundJobs__PollingMilliseconds` | `1000` | Idle poll delay (clamped to 100–60,000 ms). |
| `BackgroundJobs__LeaseSeconds` | `300` | Claim lease (clamped to 30–3,600 seconds). |
| `BackgroundJobs__MaxAttempts` | `5` | New-job retry ceiling (clamped to 1–20). |
| `BackgroundJobs__RetryBaseSeconds` / `BackgroundJobs__RetryMaxSeconds` | `30` / `3600` | Exponential retry base and cap. |
| `BackgroundJobs__RetentionDays` | `90` | Completed-job retention; expired notifications are removed by the daily worker cleanup. |
| `Smtp__Host`, `Smtp__Port`, `Smtp__FromAddress`, `Smtp__UseSsl` | No usable relay by default | SMTP host, port, sender, and required TLS. |
| `Smtp__Username`, `Smtp__Password` | Empty | Optional paired SMTP credentials; configure via a secret manager or user-secrets, never source control. |
| `Smtp__FromName` | `CivicFlow` | Optional display name. |

Apply the new migration before starting a worker-enabled deployment (`dotnet ef database update --project PublicServiceCaseManagement.Api --startup-project PublicServiceCaseManagement.Api`). Development does not deliver email: verification, password-reset, and invitation links are written to the API console automatically. These links contain sensitive, short-lived tokens; use this only in a private local Development run and never retain or share the logs. To deliver email, use a TLS-enabled SMTP relay outside Development. The worker is database-backed and requires PostgreSQL/Npgsql; it intentionally does not consume jobs when tests use EF InMemory.

## API Reference

### Authentication (`/api/auth`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/auth/csrf` | Anonymous | Get CSRF token |
| POST | `/api/auth/register` | Anonymous | Register citizen account |
| POST | `/api/auth/login` | Anonymous | Citizen portal sign-in |
| POST | `/api/auth/login/organization` | Anonymous | Organization (staff) portal sign-in |
| POST | `/api/auth/login/platform-admin` | Anonymous | Platform-admin portal sign-in |
| POST | `/api/auth/invitations/lookup` | Anonymous | Look up an invitation by token |
| POST | `/api/auth/invitations/accept` | Anonymous | Accept invitation and set password |
| POST | `/api/auth/logout` | Authenticated | End session |
| POST | `/api/auth/verify-email` | Anonymous | Confirm email address |
| POST | `/api/auth/resend-verification` | Anonymous | Resend verification email |
| POST | `/api/auth/forgot-password` | Anonymous | Request password reset link |
| POST | `/api/auth/reset-password` | Anonymous | Reset password with token |
| POST | `/api/auth/change-password` | Authenticated | Change password |
| GET | `/api/auth/me` | Authenticated | Get current user info, roles, portal |

**Login response codes**:

| Status | Code | Meaning |
|---|---|---|
| 200 | — | Success (response includes `portal` and `roles`) |
| 401 | `invalid_credentials` | Wrong email or password |
| 403 | `email_not_confirmed` | Email not verified |
| 403 | `not_a_citizen_account` / `not_an_organization_account` / `use_platform_admin_portal` / `not_a_platform_admin` | Wrong portal |
| 423 | `account_locked` | Account locked (5 failed attempts) |

### Personal Access Tokens (`/api/auth/tokens`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/auth/tokens` | Authenticated | List own tokens |
| POST | `/api/auth/tokens` | Authenticated | Create token (returns plaintext once) |
| DELETE | `/api/auth/tokens/{id}` | Authenticated | Revoke a token |

### Cases (`/api/cases`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| POST | `/api/cases` | Active CaseWorker or OrganizationAdmin membership | Create a case in the selected organization. Title is 3–200 characters after whitespace collapsing; description is 10–5,000 characters after trimming |
| GET | `/api/cases` | Active organization membership | Organization admins list all cases in their organization; staff list only cases assigned to themselves (paged) |
| GET | `/api/cases/{id}` | Active organization membership | Organization admins read any case in their organization; staff read only cases assigned to them |
| PATCH | `/api/cases/{id}/status` | OrganizationAdmin, or assigned CaseWorker | Change status only for a case the caller may edit; optionally add an explanation and mark it citizen-visible. Viewer assignments are read-only |
| PUT | `/api/cases/{id}` | OrganizationAdmin, or assigned CaseWorker | Update title and description of an editable case |
| DELETE | `/api/cases/{id}` | OrganizationAdmin | Permanently delete a case only with `{ "confirmed": true }`; attached document metadata and files are removed |
| GET | `/api/cases/{id}/history` | Active organization membership | Read history for an accessible case, including explanation and citizen-visibility flags |
| POST | `/api/cases/{id}/updates` | OrganizationAdmin, or assigned CaseWorker | Add a `PublicUpdate`, `InformationRequest`, or `InternalNote` (1–4,000 characters). Information requests move the case to `AwaitingInformation`; public kinds notify the submitter; internal notes never reach the citizen |
| GET | `/api/cases/{id}/updates` | Active organization membership | List all staff-visible updates for an accessible case, including internal notes |
| PATCH | `/api/cases/{id}/assignment` | OrganizationAdmin | Assign to active staff in the selected organization; specify case role `CaseWorker` or `Viewer` |
| GET | `/api/cases/{id}/assignment-history` | Active organization membership | Read assignment history for an accessible case |
| POST | `/api/cases/{id}/documents` | OrganizationAdmin, or assigned CaseWorker | Upload a document (multipart, 10 MB, non-empty, validated file name) with optional `visibleToCitizen` (default false); declared PDF/PNG/JPEG/GIF content must match its signature. Viewer assignments are read-only |
| GET | `/api/cases/{id}/documents` | Active organization membership | List documents for an accessible case |
| GET | `/api/cases/{id}/documents/{docId}` | Active organization membership | Download documents for an accessible case |

Case text is trimmed and normalized before persistence: titles collapse internal whitespace, descriptions preserve line breaks, and empty or whitespace-only required values are rejected with `400`. Staff-created cases always use the authenticated organization context; clients cannot select another organization in the request body. A CaseWorker-created case is assigned to that staff member. A citizen-submitted case is routed to an organization administrator for triage: assigned to an active admin staff member when one exists, otherwise every active admin is notified and the case stays unassigned until an admin assigns it. Only organization administrators can assign/reassign cases. Case assignment history records both staff member and per-case role. CaseWorker assignees may update case details, status, documents, and updates; Viewer assignees may read cases, history, and updates only. Organization administrators can manage all cases in their organization and are the only users allowed to permanently delete a case after confirming the action.

Staff communication is intentionally split by visibility: a public update and the fact of a status change are citizen-visible, an internal note is not, and an explanation is citizen-visible only when staff explicitly request it. Documents are internal unless uploaded with `visibleToCitizen=true`. An information request moves the case to `AwaitingInformation` and notifies the submitter; the citizen's response adds a `CitizenResponse` entry, returns the case to `UnderReview` with a visible history entry, and notifies the assigned staff account.

### Citizen Intake and Tracking (`/api/citizens`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/citizens/organizations` | CitizenClient | List active organizations available for case submission |
| GET | `/api/citizens/cases` | CitizenClient | List the caller's cases with `page`, `pageSize`, `search`, `status`, and `sort` (`updated_desc`, `updated_asc`, `created_desc`, `created_asc`) |
| GET | `/api/citizens/cases/{id}` | CitizenClient | Read one owned case with citizen-visible status history, updates, documents, `canRespond`, and `awaitingInformation` |
| POST | `/api/citizens/cases` | CitizenClient | Submit a case as multipart form data with selected active `organizationId`, `title` (3–200), `description` (10–5,000), and optional `additionalDocument` (any declared file type, non-empty, max 10 MB) |
| POST | `/api/citizens/cases/{id}/responses` | CitizenClient | Respond to an information request (message 1–4,000 characters); returns an `AwaitingInformation` case to `UnderReview` |
| GET | `/api/citizens/cases/{id}/documents` | CitizenClient | List the case's citizen-visible documents |
| GET | `/api/citizens/cases/{id}/documents/{documentId}` | CitizenClient | Download a citizen-visible document; non-visible or other-citizen documents return `404` |

The API obtains the citizen identity from the authenticated session, verifies the selected organization is active, stores the submitter ID, persists an optional document as a citizen-visible attachment, and routes the new case to an organization administrator for triage. Citizens can list, search, filter, sort, and page only their own cases and open a detail view with the full description and citizen-visible timeline. Internal notes, explanations marked internal, and internal-only documents are filtered in the database query and never reach a citizen response; another citizen's case is indistinguishable from a missing case (`404`). The client cannot choose an inactive or unknown organization. Uploads are size/name validated and downloaded as attachments; malware scanning is not currently provided. Citizen self-service and tracking routes remain available when `BusinessEndpoints:Enabled` is false.

### Staff (`/api/staff`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/staff` | Active organization membership | List staff only for the selected organization; phone numbers are visible only to its OrganizationAdmin |
| POST | `/api/staff` | OrganizationAdmin membership | Create staff member |
| GET | `/api/staff/platform` | SuperAdmin | List all platform staff |
| PATCH | `/api/staff/{id}/deactivate` | OrganizationAdmin membership | Deactivate staff in the selected organization |
| PATCH | `/api/staff/{id}/reactivate` | OrganizationAdmin membership | Reactivate staff in the selected organization |
| PATCH | `/api/staff/{id}/phone` | OrganizationAdmin membership | Update phone number |
| PATCH | `/api/staff/platform/{id}/deactivate` | SuperAdmin | Deactivate platform user |
| PATCH | `/api/staff/platform/{id}/reactivate` | SuperAdmin | Reactivate platform user |

### Organizations (`/api/organizations`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/organizations/me` | StaffClient + active account | Return only the caller's active memberships and organization-level roles; no organization header required |
| GET | `/api/organizations` | SuperAdmin: all organizations; OrganizationAdmin: active organizations they administer; other staff: empty list | List organizations (each row includes `organizationTypeId` and `organizationTypeName`) |
| POST | `/api/organizations` | SuperAdmin | Create organization; `OrganizationTypeId` is optional and falls back to the seeded `Uncategorized` type |
| GET | `/api/organizations/{id}` | SuperAdmin | Get organization detail |
| PUT | `/api/organizations/{id}` | SuperAdmin | Rename/update organization; `OrganizationTypeId` may be supplied to move an organization to another active type |
| PATCH | `/api/organizations/{id}/deactivate` | SuperAdmin | Deactivate organization |
| PATCH | `/api/organizations/{id}/reactivate` | SuperAdmin | Reactivate organization |
| DELETE | `/api/organizations/{id}` | SuperAdmin | Delete (204/404/409 if cases exist) |

### Organization Types (`/api/platform/organization-types`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/platform/organization-types` | SuperAdmin | List all organization types with active/inactive status and organization counts |
| GET | `/api/platform/organization-types/{id}` | SuperAdmin | Read a single organization type |
| POST | `/api/platform/organization-types` | SuperAdmin | Create an organization type; `409` on duplicate code |
| PUT | `/api/platform/organization-types/{id}` | SuperAdmin | Rename or re-code a type; `404` missing, `409` duplicate code |
| PATCH | `/api/platform/organization-types/{id}/deactivate` | SuperAdmin | Deactivate a type; the protected `Uncategorized` type cannot be deactivated |
| PATCH | `/api/platform/organization-types/{id}/reactivate` | SuperAdmin | Reactivate a previously deactivated type |
| DELETE | `/api/platform/organization-types/{id}` | SuperAdmin | Delete a type; `409` when any organization still references it, and `Uncategorized` cannot be deleted |

### Memberships (`/api/organizations/{orgId}/memberships`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| POST | — | OrganizationAdmin of `{orgId}` or SuperAdmin | Add a Viewer or CaseWorker membership; only SuperAdmin can grant OrganizationAdmin |
| GET | — | OrganizationAdmin of `{orgId}` or SuperAdmin | List memberships |
| GET | `/{id}` | OrganizationAdmin of `{orgId}` or SuperAdmin | Get membership detail |
| PUT | `/{id}/role` | OrganizationAdmin of `{orgId}` or SuperAdmin | Change Viewer/CaseWorker role; only SuperAdmin can grant OrganizationAdmin |
| PATCH | `/{id}/deactivate` | OrganizationAdmin of `{orgId}` or SuperAdmin | Deactivate membership |
| PATCH | `/{id}/reactivate` | OrganizationAdmin of `{orgId}` or SuperAdmin | Reactivate membership |

### Invitations (`/api/organizations/{orgId}/invitations`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| POST | — | StaffClient + OrgAdmin/SuperAdmin | Send invitation |
| GET | — | StaffClient + OrgAdmin/SuperAdmin | List invitations |
| POST | `/{id}/resend` | StaffClient + OrgAdmin/SuperAdmin | Resend (invalidates old link) |
| DELETE | `/{id}` | StaffClient + OrgAdmin/SuperAdmin | Revoke invitation |

### Citizens (`/api/citizens`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/citizens/me` | CitizenClient | Get own citizen profile |
| POST | `/api/citizens/me` | CitizenClient | Create/update own profile |
| PUT | `/api/citizens/me` | CitizenClient | Update own profile |

### Profile (`/api/profile`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/profile/me` | ProfileClient | Get own profile (citizen or staff) |
| PUT | `/api/profile/me` | ProfileClient | Update own profile |

### Platform Administration

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/platform/overview` | SuperAdmin | Platform-wide counters |
| GET | `/api/platform/users` | SuperAdmin | All accounts with roles/memberships |
| GET | `/api/platform/staff` | SuperAdmin | All staff memberships |
| PUT | `/api/platform/staff/{userId}` | SuperAdmin | Update a staff account's full name and phone number |
| GET | `/api/platform/audit` | SuperAdmin | Platform-wide audit trail |
| GET | `/api/platform/audit/categories` | SuperAdmin | Audit category list |
| GET | `/api/organizations/{orgId}/audit` | OrganizationAdmin of `{orgId}`, assigned staff of `{orgId}` (own events only), or SuperAdmin | Organization audit trail; staff results are actor-scoped on the server |
| GET | `/api/dashboard/platform` | SuperAdmin | Platform case statistics |
| GET | `/api/dashboard/organization?organizationId=...` | Active organization membership | Organization statistics; a supplied ID must match the authorized organization context |
| GET | `/api/errors` | SuperAdmin | List application errors (paged) |
| GET | `/api/errors/{id}` | SuperAdmin | Get error detail |
| PATCH | `/api/errors/{id}/mark-handled` | SuperAdmin | Mark error as handled |

### Health (`/api/health`)

| Method | Path | Authorization | Description |
|---|---|---|---|
| GET | `/api/health` | Anonymous | Health status |
| GET | `/api/health/ready` | Anonymous | Database connectivity check |

### Public Configuration (`/api/config/public`)

| Method | Path | Access | Description |
|---|---|---|---|
| GET | `/api/config/public` | Anonymous | Returns the safe application branding (`applicationName`) used by the shell and footer. Configure it with `Application:Name` in `appsettings.json`; nothing else from configuration is exposed. |

---

## Getting Started

### Prerequisites

- **.NET SDK 10.x** — [download](https://dotnet.microsoft.com/download)
- **Node.js** (compatible with Vite 8 / React 19, recommend v20+)
- **PostgreSQL 17** (or Docker for the database)

### Step 1: Clone

```bash
git clone https://github.com/bishalhumagain/civicflow-enterprise.git
cd civicflow-enterprise
```

### Step 2: Start PostgreSQL

```bash
# Create .env from template
cp .env.example .env
# Edit .env — set secure passwords for POSTGRES_PASSWORD, CIVICFLOW_RUNTIME_PASSWORD, CIVICFLOW_MIGRATOR_PASSWORD

# Start PostgreSQL
docker compose up -d postgres
```

### Step 3: Configure the API

```bash
# Copy the template
cp PublicServiceCaseManagement.Api/appsettings.template.json PublicServiceCaseManagement.Api/appsettings.json
```

Edit `appsettings.json`:
- `ConnectionStrings:CivicFlow` — use the `civicflow_migrator` credentials for migrations, `civicflow_runtime` for the running API
- `Cors:AllowedOrigins` — add the frontend origin (default: `http://localhost:5173`)
- `Frontend:BaseUrl` — the frontend URL (for invitation links)
- `Application:Name` — product name shown in the header and copyright footer (default: `CivicFlow Enterprise`)
- `SeedAdmin:Email` / `SeedAdmin:Password` / `SeedAdmin:FullName` — optional, for initial super-admin. The configured password is hashed by ASP.NET Core Identity; it is set when the account is first created and does not overwrite a later password change.

> **Never commit `appsettings.json`** — it is gitignored. Use environment variables or .NET user-secrets for production.

For a one-time reset of an existing seeded administrator to the configured password, set `SeedAdmin:ResetPassword=true` for a single API startup, then remove the setting. This is intentionally opt-in so normal restarts cannot silently replace an administrator's password.

### Step 4: Apply Migrations

```bash
dotnet ef database update \
  --project PublicServiceCaseManagement.Api \
  --startup-project PublicServiceCaseManagement.Api
```

> If using user-secrets for the connection string, pass it explicitly:
> `dotnet ef database update --project PublicServiceCaseManagement.Api -- --connection "<connection string>"`

### Step 5: Run the API

```bash
dotnet run --project PublicServiceCaseManagement.Api
```

> **Port 5000 note**: macOS AirPlay Receiver binds port 5000 by default. If you get "address already in use", either disable AirPlay Receiver (System Settings → General → AirDrop & Handoff) or run on a different port: `dotnet run --project PublicServiceCaseManagement.Api --urls http://localhost:5001` and update `VITE_API_BASE_URL` accordingly.

### Step 6: Run the Frontend

```bash
cd civicflow-web
cp .env.example .env.local   # Edit VITE_API_BASE_URL if needed
npm ci
npm run dev
```

The frontend is available at `http://localhost:5173`.

### Step 7: Create Your First Organization Login

Public registration only creates citizen accounts. To get a staff/organization login:

1. **Seed a super-admin** (if not done in Step 3):
   - Set `SeedAdmin:Email`, `SeedAdmin:Password`, `SeedAdmin:FullName` in `appsettings.json` or environment
   - Restart the API — the seeder runs at startup
   - Sign in at `/super-platform-admin`

2. **Create an organization**:
   ```
   POST /api/organizations
   { "name": "My Organization", "code": "MYORG" }
   ```

3. **Invite staff**:
   ```
   POST /api/organizations/{orgId}/invitations
   Headers: X-Organization-Id: {orgId}
   Body: { "fullName": "John Doe", "email": "john@example.com", "role": "CaseWorker" }
   ```
   - In Development, one-time links are printed to the API console because email is not delivered. Treat the output as a credential and do not share or retain it.

4. **Accept invitation**: Open the `/setup-password?token=...` link, choose a password, then sign in at `/admin`. The workspace resolves the active organization membership from the signed-in account; no organization ID is hardcoded.

### Email Delivery

Verification, password-reset, and invitation messages are sent through `IEmailSender`. The Development sender does not deliver email; it writes the complete one-time action link to the API console automatically. Run the API in Development (the default for local runs) and check its console after registration, resend verification, password reset, or invitation actions. These links contain sensitive tokens, so use only a private local run and never retain or share the logs. To deliver email, run outside Development with TLS-enabled SMTP. Set `Smtp__Host`, `Smtp__Port`, `Smtp__FromAddress`, and `Smtp__UseSsl=true`; optionally set both `Smtp__Username` and `Smtp__Password`. Startup fails outside Development if the SMTP settings are missing or invalid. Keep credentials in a secret manager, user-secrets, or environment variables.

---

## Testing

### Backend Tests

```bash
dotnet test PublicServiceCaseManagement.sln --configuration Release
```

**492 tests** (489 executed by default; 3 opt-in PostgreSQL RLS tests are skipped when no disposable database is configured) covering:

| Category | What's tested |
|---|---|
| **Auth lifecycle** | Registration, email confirmation, login, logout, password recovery, change password — using real Identity cookie flow with cookie container and capturing email sender |
| **Portal-scoped sign-in** | Citizen/staff/superadmin acceptance on matching portal, wrong-portal rejection with codes, session cleanup after rejected attempt, role-gated endpoint access |
| **Anti-forgery** | CSRF rejection of unsafe requests without valid token (returns stable `antiforgery_invalid` code), acceptance with valid token, and refresh-token-then-retry recovery after a session switch |
| **Invitation lifecycle** | Creation, token hashing at rest, lookup, password setup, reuse/expiry/revocation (`410`), duplicate/member conflicts (`409`), role escalation rejection, cross-org denial, existing-account linking |
| **Super-admin seeding** | Fresh seed + login, weak/partial seed failure, skip when unconfigured, repair of damaged account, password-change preservation |
| **Personal access tokens** | Creation, hashing, bearer auth, role propagation, least privilege, tampering, expiry, revocation |
| **Platform administration** | Org deletion (`204`/`404`/`409`/`403`), platform directory/overview, scoped organization listing, audit endpoints, staff account and phone update |
| **Cases** | CRUD, status changes, assignments, paging, routing a submitted case to an organization admin, and status notifications to the assigned staff member (`CaseWorker` and `Viewer`) |
| **Case documents** | Upload, download |
| **Citizen tracking** | Owner-scoped paging/search/filter/sort, detail privacy projection, public updates vs. internal notes, information request and response transitions, visible vs. internal documents, cross-citizen `404`, closed-case rejection, notification fan-out |
| **Citizens** | Profile auth, create/update, duplicates, intake, own-case list/detail with status history, administrator triage routing, business-gate exemption |
| **PostgreSQL RLS (opt-in)** | Real migrations applied, forced RLS on tenant and tracking tables, citizen owner visibility/write restrictions, organization-wide view, runtime-role restrictions |
| **Staff** | CRUD, platform staff, phone update |
| **Organizations** | CRUD, delete rules |
| **Memberships** | CRUD, role changes |
| **Audit** | Persistence, fail-safe behavior, query filtering/paging/scoping |
| **Profile** | Unified profile: citizen/staff GET/PUT, duplicate NID (409), business-endpoint gating, superadmin exclusion, create-on-update |
| **Validation and normalization** | Shared text, phone, and email normalization; custom validation attributes; trimmed-length, allowed-value, and GUID boundaries; registration, profile, case, and pagination payload validation; PDF/image signature and empty-file rejection; normalized persistence through real Identity registration |
| **Domain unit tests** | Case state machine, status transitions, editability, phone validation, citizen profile validation |

### Frontend Tests

```bash
cd civicflow-web
npm run lint            # ESLint
npm run build           # TypeScript compilation + Vite build
npm run typecheck:e2e   # TypeScript check for the Playwright suite
```

### Browser E2E Tests

The Playwright suite runs nine serial Chromium journeys against a disposable PostgreSQL database. It seeds confirmed citizen/staff accounts, an organization, and staff membership, then exercises real cookie and anti-forgery sessions:

```bash
cd civicflow-web
npm ci
npx playwright install chromium chromium-headless-shell
export CIVICFLOW_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=55433;Database=civicflow;Username=<admin>;Password=<local-admin-password>'
npm run test:e2e
```

`CIVICFLOW_E2E_DATABASE_URL` may replace the admin connection string with a PostgreSQL URL. `CIVICFLOW_E2E_API_URL` and `CIVICFLOW_E2E_WEB_URL` override the default `http://localhost:5001` / `http://localhost:5173` endpoints. The API must run with the runtime database role, `BusinessEndpoints__Enabled=true`, `Security__RequireAntiforgery=true`, and a writable `FileStorage__RootPath`. Outside CI, the runner reuses an already-running API and Vite server when available. Reports and failure traces are gitignored. See [Integration Verification](docs/INTEGRATION-VERIFICATION.md#browser-end-to-end-verification) for the journey and result details.

---

## Deployment & CI

### CI Pipeline (`.github/workflows/ci.yml`)

Two jobs run on every push/PR to `main`:

| Job | Runner | Steps |
|---|---|---|
| `build-and-test` | ubuntu-latest | .NET 10 → restore → build (Release) → test |
| `frontend` | ubuntu-latest | Node 20 → npm ci → lint → build |

### Docker Compose

The `compose.yaml` file provides PostgreSQL 17 with:
- Separate `civicflow_migrator` and `civicflow_runtime` roles (created by init script)
- Health check with `pg_isready`
- Persistent volume
- Port bound to `127.0.0.1` (localhost only)

### Production Considerations

- **HTTPS required** for `__Host-` cookie prefix and `SecurePolicy.Always`
- **SMTP provider** — TLS-enabled SMTP is required outside Development; local Development writes one-time links to the API console instead of delivering them
- **Object storage** — replace `LocalFileStorage` with production-grade storage + malware scanning
- **Secret management** — use environment variables, user-secrets, or a secret manager (never commit secrets)
- **Database** — use separate `civicflow_migrator` for migrations, `civicflow_runtime` for the running API
- **BusinessEndpoints:Enabled** — set to `true` only after security review; defaults to `false`. Citizen self-service routes are exempt.
- **CORS** — restrict `AllowedOrigins` to production frontend origins
- **Rate limiting** — tune for production traffic patterns

See [Delivery Roadmap](docs/DELIVERY-ROADMAP.md) for the full production-readiness checklist and [Release Verification](docs/RELEASE-VERIFICATION.md) for the signing workflow.

---

## Documentation

| Document | Purpose |
|---|---|
| **[README.md](README.md)** (this file) | Project overview, architecture, API reference, setup, testing |
| **[CHANGELOG.md](CHANGELOG.md)** | All notable changes, Keep a Changelog format |
| **[docs/USER_MANUAL.md](docs/USER_MANUAL.md)** | End-user guide: accessing CivicFlow, sign-in, workflows, troubleshooting, glossary |
| **[docs/API-AUTHORIZATION.md](docs/API-AUTHORIZATION.md)** | Current API endpoint inventory, request/response contracts, tenant scoping, and role/resource matrix |
| **[docs/DELIVERY-ROADMAP.md](docs/DELIVERY-ROADMAP.md)** | Implementation-first roadmap: completed baseline, active delivery, deferred work |
| **[docs/INTEGRATION-VERIFICATION.md](docs/INTEGRATION-VERIFICATION.md)** | Verification evidence: test coverage, over-HTTP results, acceptance checklist |
| **[docs/RLS-DESIGN.md](docs/RLS-DESIGN.md)** | PostgreSQL row-level security design and verification |
| **[docs/VALIDATION-AND-FORMATTING.md](docs/VALIDATION-AND-FORMATTING.md)** | Field rules, normalization, database constraints, file restrictions, display formats, and the change checklist |
| **[docs/RELEASE-VERIFICATION.md](docs/RELEASE-VERIFICATION.md)** | Release signing workflow, provenance, GPG verification |
| **[docs/DOCUMENTATION_WORKFLOW.md](docs/DOCUMENTATION_WORKFLOW.md)** | How to maintain documentation as the project evolves |

---

## Troubleshooting

| Problem | Likely cause | Fix |
|---|---|---|
| **Sign-in fails** | Wrong portal, unconfirmed email, account state, credentials, or DB unavailable | Use the correct portal. For password recovery or email verification, open **Forgot password** and enter the account email. Accounts migrated from the previous external identity provider have no local password and must set one through **Forgot password**; old provider credentials cannot be checked by CivicFlow. In Development, check the API console for a one-time link; outside Development, check SMTP delivery/configuration. If credentials still fail, ask an administrator to verify the account is active and confirmed; the UI intentionally does not reveal account existence or status. |
| **"This account is not a citizen/organization account"** | Signed in through wrong portal | Citizens use `/citizen/login`, staff use `/admin`, superadmins use `/super-platform-admin` |
| **"not a platform administrator"** | Staff/citizen at the platform portal | Use `/admin` (staff) or `/citizen/login` (citizens) |
| **Invitation link "invalid, expired, or already used"** | Link was used, revoked, or expired (72h default) | Ask an administrator to resend the invitation |
| **Invitation email not received** | SMTP is not configured or delivery failed | In Development, check the API console; outside Development, configure/check the SMTP host, TLS, sender address, and provider credentials |
| **404 from frontend** | `VITE_API_BASE_URL` missing or wrong | Set to API base URL (e.g., `http://localhost:5000`), restart Vite dev server |
| **401/403 in workspace** | Session cookie missing/expired (401) or insufficient role (403) | Sign in again; check organization membership/role |
| **CSRF 400 on save** | Anti-forgery token missing/expired | Refresh the page and retry; frontend fetches token from `GET /api/auth/csrf` |
| **Cross-origin cookies not sent** | CORS not configured or `credentials` not set | Add frontend origin to `Cors:AllowedOrigins`; `credentials: 'include'` is already set in `auth.ts` |
| **"address already in use" (port 5000)** | macOS AirPlay Receiver on port 5000 | Disable AirPlay Receiver or run API on another port (`--urls http://localhost:5001`) |
| **Migrations fail** | Wrong DB role or unreachable DB | Use `civicflow_migrator` credentials; verify PostgreSQL is running |
| **"This organization still holds cases"** | Trying to delete an org with active cases | Resolve or move cases first, then delete |
| **PAT returns 401** | Token revoked, expired, or owner deactivated | Create a new token |

---

## Implementation Status

### Implemented and Verified

- Backend API with EF Core/PostgreSQL and 27 migrations
- ASP.NET Core Identity authentication (cookie sessions, email verification, password recovery, lockout)
- Portal-scoped sign-in (citizen, organization, platform-admin) with wrong-portal rejection and session cleanup
- Personal access tokens (SHA-256 hash, one-time plaintext, revocation, expiry, bearer auth)
- CSRF protection on all unsafe `/api/*` methods
- Rate limiting on auth/password endpoints
- Security headers middleware
- Organization scoping with PostgreSQL RLS on 6 tenant-owned tables, including citizen owner-scoped tracking reads and writes
- Cases: create, list (paged/searchable/filterable), detail, status state machine with citizen-visible explanations, assignment, history, public updates, internal notes, information requests, and documents with explicit citizen visibility
- Staff: CRUD, platform-wide listing, phone numbers, activation/deactivation
- Organizations: CRUD, delete with case-check, memberships, roles
- Invitation-based onboarding (single-use, expiring, SHA-256 hashed, resend/revoke)
- Citizens: self-registration, profile with duplicate detection, owner-scoped paged/searchable/sortable tracking, detail view with citizen-visible timeline and documents, information-request responses, and administrator triage routing on submission
- Unified self-service profile for citizens and staff
- Audit trail (platform-wide and org-scoped, filterable, paged, fail-safe)
- Platform directory (overview counters, Citizens, Staff, and All Users sections)
- Dashboard statistics (platform-wide and org-scoped)
- Application error tracking (persisted, reviewable, mark-as-handled)
- Frontend: all auth flows, role-specific workspaces, session restoration, toast notifications
- 492 automated tests (unit + integration; 3 opt-in PostgreSQL RLS tests skipped without a disposable database)
- 9 Playwright browser end-to-end journeys (verified against disposable PostgreSQL)
- CI pipeline (GitHub Actions)

### Implemented but Limited

- The Playwright E2E suite and the opt-in PostgreSQL RLS tests are not yet wired into CI; both require a disposable PostgreSQL fixture
- Local file storage only (no object storage, malware scanning, or retention policy)
- Most API tests use EF Core InMemory with synthetic auth; a subset exercises real Identity cookie flow, and the tracking browser suite runs against real PostgreSQL
- PostgreSQL tenant-boundary coverage is opt-in and skipped by the default test run when no disposable database is configured
- Development does not deliver email. It writes one-time links (including sensitive tokens) to the API console automatically; use this only in a private Development environment. Use TLS-enabled SMTP for delivery outside Development.
- Citizen case intake supports active-organization selection and optional arbitrary-file attachments; submitted cases are routed to an organization administrator for triage, and citizens can track each request's status and open its full detail with status history

### Not Started

- CI wiring for the opt-in PostgreSQL RLS and Playwright browser suites
- Production-grade object storage and malware scanning
- Production deployment, backups, observability, and operational recovery

See [Delivery Roadmap](docs/DELIVERY-ROADMAP.md) for the full roadmap.

---

## Contributing

1. **Inspect the current diff before editing** — understand what has changed.
2. **Keep each batch focused** — run the narrowest relevant checks immediately.
3. **Never use Development configuration to grant roles implicitly.**
4. **Don't enable production business endpoints while boundaries remain incomplete.**
5. **Update documentation when functionality changes** — see [Documentation Workflow](docs/DOCUMENTATION_WORKFLOW.md).
6. **Add CHANGELOG entries** for meaningful changes.
7. **Verify before documenting** — distinguish implemented vs planned.
8. **Never commit secrets** — credentials, tokens, passwords, keys, or personal data.

---

## License

No license file is currently published. The project owner (Bishal Humagain) retains all rights. See [CIVICFLOW-PROVENANCE.json](CIVICFLOW-PROVENANCE.json) for project identity metadata and [Release Verification](docs/RELEASE-VERIFICATION.md) for the signing workflow.

---

*This README reflects the verified code state as of 2026-10-25. Items marked as limited or not started require runtime/config validation before production use.*
