# CivicFlow web client

The React 19 + TypeScript + Vite frontend for CivicFlow Enterprise. It provides the public landing page, citizen and staff authentication pages, and the organization-scoped workspace.

## Current status

Implemented:

- Public landing page with separate Citizen (sign in / register) and Organization portal entry points.
- Citizen pages: login, register, email verification, forgot password, reset password.
- Staff/organization portal sign-in at the dedicated `/admin` route.
- Platform administrator sign-in at `/super-platform-admin` (intentionally unlinked from public site).
- Session restoration on load via `GET /api/auth/me`.
- Citizen workspace: a citizen dashboard with self-service actions (public intake, own profile, access tokens). Intake submits multipart cases to an active organization with an optional arbitrary-file attachment up to 10 MB. A **My requests** list tracks each submitted request's status, and a detail view shows the full description and complete status history.
- Organization workspace: cases, staff directory, organization administration, access tokens, with filtering, search, pagination, and case details (history, assignments, documents). Organization administration includes organization create/rename/activate/deactivate/delete, membership management (add/update roles/activate/deactivate), and invitation management.
- Platform administrator workspace: organizations (organization/membership/invitation administration), platform directory (overview, accounts, staff memberships), platform audit log, and access tokens.
- Portal-scoped sign-in: the API rejects an account that signs in through the wrong portal, and the client re-validates the issued session before rendering a workspace.
- Role-derived navigation and views: the sidebar, the active view, and the API calls are computed from the roles the API reports.
- Organization and platform dashboards visualize API-backed case status, priority, and weekly/monthly activity data; the platform dashboard also shows live organization, account, membership, and invitation totals.
- Shared responsive workspace navigation, visible keyboard focus, a skip link, reduced-motion support, and accessible chart labels and data summaries.
- Organization members see a trusted, current-organization `My Organization` view with role-limited team details; case workspaces use distinct list/details tabs, save feedback, and unsaved-change protection.
- Case document previews fetch content through the existing authorized API routes; PDFs and images render inline, while unsupported types remain downloadable.
- Toast notifications and responsive layout.
- TypeScript compilation, Vite production build, and ESLint validation pass.

## Authentication model

Authentication is first-party and handled by the CivicFlow API using HttpOnly session cookies:

- The browser session is an HttpOnly, SameSite cookie — the frontend never stores tokens or secrets in JavaScript-accessible storage.
- API requests are made with `credentials: 'include'`.
- State-changing requests (`POST`/`PUT`/`PATCH`/`DELETE`) attach an anti-forgery token in the `X-CSRF-TOKEN` header. The token is fetched from `GET /api/auth/csrf` and cached in memory by `authFetch` in `src/auth.ts`; `login` clears any cached token before signing in so a token from a previous user is never reused. On a CSRF failure the server returns the stable code `antiforgery_invalid`, and the token is refreshed once and the request retried.
- `src/api.ts` and `src/api-client.ts` build on `authFetch` and set the `X-Organization-Id` header for organization-scoped endpoints.

There is no OIDC/bearer-token handling in the frontend.

### Portal-scoped sign-in

The product has three separate sign-in surfaces, and the API binds every attempt to one of them:

| Portal | Sign-in route | API endpoint | Accepted roles |
|---|---|---|---|
| Citizen | `/citizen/login` | `POST /api/auth/login` | `citizen` |
| Organization | `/admin` | `POST /api/auth/login/organization` | `staff`, `superadmin` |
| Platform Administration | `/super-platform-admin` | `POST /api/auth/login/platform-admin` | `superadmin` |

`src/auth.ts` exposes the portal model (`SignInPortal`, `allowedPortals`, `canUsePortal`, `isSuperAdmin`, `loginEndpoint`). After a successful sign-in the client fetches `GET /api/auth/me` and verifies that the session really carries the roles of the portal it signed in through; if it does not, the client signs out again and reports the server's reason (`not_a_citizen_account` / `not_an_organization_account`).

The portal a restored session belongs to is derived from the account roles on every load. The last used portal is only remembered as a UI preference in `sessionStorage` (`civicflow.portal`) and is never trusted unless the roles allow it. No credential is stored in browser storage.

### Personal access tokens

The **Access tokens** workspace view lets a signed-in user create, list, and revoke personal access tokens (PATs) for non-browser API clients. The token plaintext is displayed once, right after creation, and is never stored by the web app; the server keeps only a SHA-256 hash. `src/auth.ts` exposes the client functions (`listPersonalAccessTokens`, `createPersonalAccessToken`, `revokePersonalAccessToken`) and the related types.

The web app never sends a PAT itself — it is a management surface for tokens that other systems use as `Authorization: Bearer civicflow_pat_…`.

## Environment configuration

Vite loads environment files from this directory only. Copy the example and edit it:

```bash
cp .env.example .env.local
```

| Variable | Purpose | Local default |
|---|---|---|
| `VITE_API_BASE_URL` | CivicFlow API base URL | `http://localhost:5001` |
| `VITE_DEV_PORT` | Vite browser port; Vite refuses fallback ports | `5173` |
| `VITE_DEV_HTTPS` | Serve Vite over HTTPS using the exported .NET development certificate | `false` |
| `VITE_DEV_CERTIFICATE` | Path to the local-only PFX when HTTPS is enabled | `.certs/localhost.pfx` |
| `VITE_DEV_CERTIFICATE_PASSWORD` | Local-only PFX password | unset |

The standalone API launch profile listens on port `5001`; Docker Compose uses the separate `API_BASE_URL` in the repository-root `.env` (default port `5000`). The workspace resolves active organization memberships from the authenticated session with `GET /api/organizations/me`; no organization ID is configured in the frontend. `.env` and `.env.local` are gitignored; only `.env.example` is committed. The API enforces its own session, anti-forgery, membership, organization, and role checks — frontend configuration is not a security boundary. Restart `npm run dev` after changing the API URL (Vite reads it at startup).

> **Browser origin**: `vite.config.ts` is authoritative for the local browser URL (`http://localhost:5173`) and uses `strictPort`, so Vite will report a port conflict instead of silently switching origins. The API's Development `Frontend:BaseUrl` must match this origin because it creates verification and invitation links. If changing the Vite port, update `Frontend:BaseUrl` too. Development CORS permits loopback origins on any port; the actual Vite URL, not a CORS allowlist entry, is the source of truth for the browser.

### HTTPS development

The API also listens at `https://localhost:7001`. To run both apps over HTTPS, follow the repository README's **Optional local HTTPS** instructions to trust and export the .NET development certificate. Set `VITE_API_BASE_URL=https://localhost:7001`, `VITE_DEV_HTTPS=true`, and the local certificate path/password in `.env.local`; start the API with `Frontend__BaseUrl=https://localhost:5173`. Never check in `.certs`, PFX files, or a certificate password. To use a non-default Vite port, set `VITE_DEV_PORT` and keep `Frontend__BaseUrl` aligned; strict port behavior prevents authentication from unexpectedly moving to 5174/5175.

## Components

### OrganizationAdministration
The `OrganizationAdministration` component (`src/components/OrganizationAdministration.tsx`) provides the platform administration interface for managing organizations, memberships, and invitations. It is available to platform super administrators in the platform workspace.

**Features:**
- Organization selection dropdown with name and code display
- Organization actions: Rename, Deactivate/Reactivate, Delete
- Inline editing form for organization updates (name max 200 chars, code max 50 chars)
- Organization creation form
- Membership management form (add by user ID with role selection)
- Membership list with role dropdown and activation toggle
- Integrated InvitationManager component for invitation management

**Props:**
- `authenticated`: boolean - whether the user is authenticated
- `loading`: boolean - loading state for operations
- `organizations`: Organization[] - list of organizations
- `memberships`: Membership[] - list of memberships for selected organization
- `invitations`: AccountInvitation[] - list of invitations for selected organization
- `selectedOrganizationId`: string - currently selected organization ID
- `onSelectOrganization`: (id: string) => void - callback for organization selection
- `onCreateOrganization`: (name: string, code: string) => Promise<void> - create organization
- `onUpdateOrganization`: (id: string, name: string, code: string) => Promise<void> - update organization
- `onToggleOrganization`: (organization: Organization) => Promise<void> - activate/deactivate
- `onDeleteOrganization`: (organization: Organization) => Promise<void> - delete organization
- `onCreateMembership`: (userId: string, role: string) => Promise<void> - add membership
- `onUpdateMembership`: (membership: Membership, action: 'role' | 'active', role?: string) => Promise<void> - update membership
- `onInvite`: (fullName: string, email: string, phoneNumber: string, role: string) => Promise<boolean> - send invitation
- `onResendInvitation`: (invitationId: string) => Promise<void> - resend invitation
- `onRevokeInvitation`: (invitationId: string) => Promise<void> - revoke invitation

**Related Components:**
- `InvitationManager`: Handles invitation creation, listing, resend, and revoke operations
- Domain types: `Organization`, `Membership`, `AccountInvitation` from `src/domain.ts`

## Commands

```bash
npm install
npm run dev
npm run build
npm run lint
```
