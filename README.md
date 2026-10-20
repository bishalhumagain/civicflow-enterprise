# CivicFlow Enterprise

CivicFlow Enterprise is a full-stack public service case management platform. It has a .NET 10 Web API backend with PostgreSQL and JWT/OIDC authentication (Keycloak), and a React + TypeScript + Vite frontend for staff operations and public intake.

## What the project can do so far

### Backend (PublicServiceCaseManagement.Api)
- **Authentication & Authorization**: JWT Bearer auth via Keycloak with issuer/audience validation. Policies for staff (`azp` = `civicflow-staff`) and citizens (`azp` = `civicflow-citizen`). Fallback policy requires authenticated staff client.
- **Organization scoping**: All business operations are organization-scoped. Middleware/resolvers enforce organization context from authenticated claims/tokens.
- **Cases**: Create/list/view cases, update status, assign cases to staff, view case history and assignment history, manage documents (upload/download with file storage).
- **Staff & Team**: Staff directory management (create/deactivate/reactivate).
- **Organizations & Memberships**: Create organizations, manage memberships and roles, toggle organization/membership status.
- **Public/Operational endpoints**: REST API under `/api/*` with Swagger in development.
- **Data layer**: EF Core with PostgreSQL (Npgsql), migrations support, domain entities for cases, staff, orgs, memberships, documents, history.
- **File storage**: Local file storage abstraction for case documents.

### Frontend (civicflow-web)
- **Modular architecture**: Components split into `src/components/` (CaseDetails, StaffDirectory, OrganizationAdministration, CitizenIntake, StatCard, StatusPill, LoginForm, Toast) and pages in `src/pages/` (LandingPage). Clean separation of concerns.
- **Landing page**: Professional welcome screen with two options - "I'm a Citizen" and "I'm a Government Officer". 
  - Government Officer flow shows an in-app branded login form; authentication remains server-authoritative via Keycloak OIDC (Authorization Code flow).
  - Citizen flow indicates journey intent and routes to public intake.
- **Authentication**: OIDC integration via `oidc-client-ts` (Keycloak). Session management, login/logout, token handling. Auth is backend-authoritative (JWT validation).
- **Workspace**: Authenticated staff workspace with views for Cases, Staff & Team, Organizations, and Public intake. Case list with filters/search/pagination, case details with status changes, assignments, history, and documents.
- **Case operations**: Create cases, update status (with guards for demo cases), assign to staff, upload/download documents.
- **Administration**: Staff management and organization/membership administration with role-based UI behavior.
- **Notifications**: Toast notification system for success/error/info/warning feedback.
- **Responsive design**: Mobile/tablet-friendly layouts, responsive grids/tables/buttons.
- **Error handling**: Proper error messages, loading states, guards for unauthenticated/configured states.

### Infrastructure/Tooling
- Docker Compose setup, scripts, infra folder, solution file, tests project present.

## Recommended next steps

### High Priority (Security & Core Flow)
1. **Complete Keycloak integration** - Set up proper Keycloak realm/clients (civicflow-api, civicflow-staff, civicflow-citizen), configure env vars, test token flows. Consider custom Keycloak theme to avoid UI jump.
2. **Role/claim enforcement** - Wire up UI to respect backend policies/roles (hide admin actions based on claims), ensure proper authorization checks.
3. **Error boundaries & validation** - Add React error boundaries, improve form validation, handle token expiry gracefully.
4. **API error standardization** - Ensure consistent error responses from API, better user-facing messages.

### Medium Priority (UX & Features)
5. **Citizen portal completion** - Flesh out public intake flow (submission, tracking) with proper auth/roles.
6. **Notifications/real-time** - Add in-app notifications or signalR for case updates.
7. **Audit trail UI** - Enhance history/assignment history display with better timelines.
8. **File handling** - Add file type/size validation, previews, better download UX.
9. **Search & filters** - Advanced filtering, saved views.

### Low Priority (Polish & Ops)
10. **Testing** - Add unit/integration tests (frontend + backend). Current tests project exists but needs coverage.
11. **Performance** - Code splitting, lazy loading routes/components, API pagination improvements.
12. **Observability** - Logging, health checks, monitoring.
13. **CI/CD** - GitHub Actions for lint/test/build/deploy.
14. **Documentation** - API docs expansion, deployment guide, env var docs.

**Immediate recommendation:** Focus on Keycloak realm/client configuration and end-to-end auth testing. The architecture is solid and ready for proper identity setup. The UI now provides a clean branded entry point while maintaining server-authoritative security.
