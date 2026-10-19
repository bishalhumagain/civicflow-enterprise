# CivicFlow web client

This directory contains the React/TypeScript reviewer-facing dashboard shell for CivicFlow Enterprise.

## Current status

Implemented and verified:

- Responsive organization workspace layout.
- Case list with status filtering and selection.
- Case detail, assignment, priority, activity, and summary-card presentation.
- Explicit `DEMO MODE` indicator using local representative data.
- TypeScript compilation, Vite production build, and ESLint validation.

Implemented locally without a third-party OIDC package:

- Authorization Code + PKCE redirect flow in `src/auth.ts`.
- State and code-verifier validation using session storage.
- In-memory access-token handling after callback.
- Configurable bearer API request helper in `src/api.ts`.

The OIDC flow becomes active only when the required Vite environment values are supplied.

Not yet implemented:

- Connecting dashboard data to API calls.
- Protected route handling and token lifecycle management.
- Live document upload/download and server-side case mutations.

The demo data is intentionally visible as demo data. Do not add a hidden token, client secret, or unauthenticated production API path to make the screen appear live.

Example local frontend configuration:

```bash
VITE_KEYCLOAK_AUTHORITY=http://localhost:8080/realms/civicflow
VITE_KEYCLOAK_CLIENT_ID=civicflow-web
VITE_KEYCLOAK_REDIRECT_URI=http://localhost:5173
VITE_API_BASE_URL=https://localhost:7248
```

These values identify a public browser client; do not add a client secret to Vite environment variables. The API still enforces its own JWT, client, application-user, membership, organization, and role checks.

## Commands

```bash
npm install
npm run dev
npm run build
npm run lint
```

The next frontend implementation step is an OIDC integration using a public Keycloak client configuration, followed by a typed API client that sends bearer tokens to endpoints protected by the backend authorization policies.
