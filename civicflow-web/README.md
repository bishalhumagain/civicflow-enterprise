# CivicFlow web client

The React 19 + TypeScript + Vite frontend for CivicFlow Enterprise. It provides the public landing page, citizen and staff authentication pages, and the organization-scoped workspace.

## Current status

Implemented:

- Public landing page with separate Citizen (sign in / register) and Organization portal entry points.
- Citizen pages: login, register, email verification, forgot password, reset password.
- Staff/organization portal sign-in at the dedicated `/admin` route.
- Session restoration on load via `GET /api/auth/me`.
- Organization workspace: cases, staff directory, organization administration, public intake, citizen profile, with filtering, search, pagination, and case details (history, assignments, documents).
- Toast notifications and responsive layout.
- TypeScript compilation, Vite production build, and ESLint validation pass.

## Authentication model

Authentication is first-party and handled by the CivicFlow API using HttpOnly session cookies:

- The browser session is an HttpOnly, SameSite cookie — the frontend never stores tokens or secrets in JavaScript-accessible storage.
- API requests are made with `credentials: 'include'`.
- State-changing requests (`POST`/`PUT`/`PATCH`/`DELETE`) attach an anti-forgery token in the `X-CSRF-TOKEN` header. The token is fetched from `GET /api/auth/csrf` and cached in memory by `authFetch` in `src/auth.ts`; on a CSRF failure the token is refreshed once and the request retried.
- `src/api.ts` and `src/api-client.ts` build on `authFetch` and set the `X-Organization-Id` header for organization-scoped endpoints.

There is no OIDC/bearer-token handling in the frontend.

## Environment configuration

Vite loads environment files from this directory only. Copy the example and edit it:

```bash
cp .env.example .env.local
```

| Variable | Purpose | Local default |
|---|---|---|
| `VITE_API_BASE_URL` | CivicFlow API base URL | `http://localhost:5000` |
| `VITE_CIVICFLOW_ORGANIZATION_ID` | Default workspace organization | seeded org id |

`.env` and `.env.local` are gitignored; only `.env.example` is committed. The API enforces its own session, anti-forgery, membership, organization, and role checks — the frontend values are convenience defaults, not a security boundary. Restart `npm run dev` after changing these values (Vite reads them at startup).

## Commands

```bash
npm install
npm run dev
npm run build
npm run lint
```
