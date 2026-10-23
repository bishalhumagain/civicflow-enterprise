# CivicFlow web client

The React 19 + TypeScript + Vite frontend for CivicFlow Enterprise. It provides the public landing page, citizen and staff authentication pages, and the organization-scoped workspace.

## Current status

Implemented:

- Public landing page with separate Citizen (sign in / register) and Organization portal entry points.
- Citizen pages: login, register, email verification, forgot password, reset password.
- Staff/organization portal sign-in at the dedicated `/admin` route.
- Platform administrator sign-in at `/super-platform-admin` (intentionally unlinked from public site).
- Session restoration on load via `GET /api/auth/me`.
- Citizen workspace: a citizen dashboard with self-service actions (public intake, own profile, access tokens). Intake submits multipart cases to an active organization with an optional arbitrary-file attachment up to 10 MB. Case tracking is not available to citizens yet.
- Organization workspace: cases, staff directory, organization administration, access tokens, with filtering, search, pagination, and case details (history, assignments, documents). Organization administration includes organization create/rename/activate/deactivate/delete, membership management (add/update roles/activate/deactivate), and invitation management.
- Platform administrator workspace: organizations (organization/membership/invitation administration), platform directory (overview, accounts, staff memberships), platform audit log, and access tokens.
- Portal-scoped sign-in: the API rejects an account that signs in through the wrong portal, and the client re-validates the issued session before rendering a workspace.
- Role-derived navigation and views: the sidebar, the active view, and the API calls are computed from the roles the API reports.
- Organization and platform dashboards visualize API-backed case status, priority, and weekly/monthly activity data; the platform dashboard also shows live organization, account, membership, and invitation totals.
- Shared responsive workspace navigation, visible keyboard focus, a skip link, reduced-motion support, and accessible chart labels and data summaries.
- Toast notifications and responsive layout.
- TypeScript compilation, Vite production build, and ESLint validation pass.

## Authentication model

Authentication is first-party and handled by the CivicFlow API using HttpOnly session cookies:

- The browser session is an HttpOnly, SameSite cookie — the frontend never stores tokens or secrets in JavaScript-accessible storage.
- API requests are made with `credentials: 'include'`.
- State-changing requests (`POST`/`PUT`/`PATCH`/`DELETE`) attach an anti-forgery token in the `X-CSRF-TOKEN` header. The token is fetched from `GET /api/auth/csrf` and cached in memory by `authFetch` in `src/auth.ts`; on a CSRF failure the token is refreshed once and the request retried.
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
| `VITE_API_BASE_URL` | CivicFlow API base URL | `http://localhost:5000` |

The workspace resolves active organization memberships from the authenticated session with `GET /api/organizations/me`; no organization ID is configured in the frontend. `.env` and `.env.local` are gitignored; only `.env.example` is committed. The API enforces its own session, anti-forgery, membership, organization, and role checks — frontend configuration is not a security boundary. Restart `npm run dev` after changing the API URL (Vite reads it at startup).

> **Port note**: the API listens on `http://localhost:5000` by default, but macOS's AirPlay Receiver can also bind port `5000`, which makes the API fail to start. Run the API on another port (for example `--urls http://localhost:5001`) and set `VITE_API_BASE_URL` to that same URL in `.env.local`, then restart the Vite dev server. The frontend origin (`http://localhost:5173`) must remain in the API's `Cors:AllowedOrigins`; the API port does not affect CORS.

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
