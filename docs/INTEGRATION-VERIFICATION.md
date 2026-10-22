# Integration verification

## Current evidence

The automated suite uses EF Core InMemory and a synthetic authentication handler for most API tests. A subset exercises the real ASP.NET Core Identity flow through the cookie scheme (with a cookie container and a capturing email sender): registration with email-confirmation gating, register → verify → login → `me`, wrong-password rejection, duplicate-email enumeration safety, lockout (`423`), forgot-password/reset-password, change-password, and logout. Dedicated anti-forgery tests assert that unsafe requests without a valid `X-CSRF-TOKEN` are rejected (`400`) and that a valid token is accepted.

PostgreSQL migrations and runtime-role restrictions have been verified against a disposable live stack: migrations applied with `civicflow_migrator`, and `civicflow_runtime` could read application tables but could not create schema objects. See [`RLS-DESIGN.md`](RLS-DESIGN.md) for the tenant-boundary design. These live probes are not yet CI integration tests.

Citizen self-service profile endpoints (`/api/citizens/me`) are covered by automated integration tests using the synthetic authentication handler: anonymous requests return 401, staff requests return 403, unlinked identities return 401, create/update behavior and duplicate validation are asserted, and the endpoints honour `BusinessEndpoints:Enabled` gating.

Personal access token endpoints (`/api/auth/tokens`) are covered by automated integration tests against the real bearer scheme: anonymous create is rejected, the plaintext token is returned exactly once and only a hash is persisted, the token authenticates `me` and carries the owner's roles, a citizen token receives `403` on a staff endpoint, revocation invalidates the credential immediately, and unknown, tampered, and expired tokens are rejected.

Portal-scoped sign-in is covered by automated integration tests against the real Identity cookie flow: a citizen account is accepted on `POST /api/auth/login` and rejected with `403` / `not_an_organization_account` on `POST /api/auth/login/organization`, a staff account is rejected with `403` / `not_a_citizen_account` on the citizen endpoint and accepted on the organization endpoint, a super administrator is accepted on the organization endpoint, a rejected attempt leaves no session behind (`GET /api/auth/me` answers `401`), the login response reports the session roles, and a staff account is denied on a citizen-only endpoint.

The full auth journey was additionally exercised over HTTP against a local PostgreSQL instance: register → confirmation link in the API log → verify → login → `me` → create PAT → list → bearer `me` (`200`) → staff endpoint with a citizen PAT (`403`) → revoke (`204`) → bearer `me` (`401`). Sign-in failures were also confirmed to return the machine-readable codes `email_not_confirmed` (`403`) and `invalid_credentials` (`401`). A live browser run of the same flows was not performed in this environment.

The portal/role matrix was additionally exercised over HTTP against the same local PostgreSQL instance: a citizen account signed in on the citizen portal (`200`, `portal: citizen`, `roles: [citizen]`), was refused on the organization portal (`403`, `not_an_organization_account`), and was left without a session after the refusal (`401` on `me`); a super administrator was refused on the citizen portal (`403`, `not_a_citizen_account`) and accepted on the organization portal (`200`, `portal: organization`, `roles: [superadmin]`), also without a leftover session. Role-gated endpoints behaved as designed: a citizen received `403` from `/api/staff` and `/api/cases` but `200` from `/api/citizens/me`, while the super administrator received `403` from `/api/citizens/me`. The CORS preflight for the frontend origin (`http://localhost:5173`) returned `Access-Control-Allow-Origin: http://localhost:5173` with `Access-Control-Allow-Credentials: true`.

## Port note

The API listens on `http://localhost:5000` by default, but macOS's AirPlay Receiver can occupy that port and make the API fail to start. When that happens, run the API with `--urls http://localhost:5001` and point the frontend at it with `VITE_API_BASE_URL=http://localhost:5001` in `civicflow-web/.env.local` (restart the Vite dev server afterwards). The frontend origin (`http://localhost:5173`) must stay in the API's `Cors:AllowedOrigins`; the API port does not affect CORS.

## Required local verification sequence

Use the disposable local fixtures and local-only credentials:

```bash
cp .env.example .env
# Replace placeholders in .env; never commit .env.
docker compose up -d postgres

ConnectionStrings__CivicFlow="Host=localhost;Port=5432;Database=civicflow;Username=civicflow_migrator;Password=<local-migrator-password>" \
dotnet ef database update \
  --project PublicServiceCaseManagement.Api \
  --startup-project PublicServiceCaseManagement.Api

dotnet test PublicServiceCaseManagement.sln \
  --configuration Release \
  --no-restore
```

The Compose fixture initializes separate `civicflow_migrator` and `civicflow_runtime` PostgreSQL roles. Configure the API connection with the runtime role and use the migration role only for `dotnet ef database update`.

Email verification and password-reset messages are produced by `IEmailSender`. The default `LoggingEmailSender` writes them to the application log at `Information` level (including the link), which is sufficient for local verification without an SMTP server; replace it with a real implementation for production.

Do not run the migration command against a persistent or production database. Do not put database passwords, email credentials, or any tokens in the repository, test output, or documentation.

## Required acceptance evidence

- Registration cannot sign in until the email is confirmed; the confirmation link enables sign-in.
- Wrong credentials are rejected with `401`; unconfirmed email returns `403`; lockout returns `423`.
- Registration and password-recovery responses do not disclose whether an email exists.
- Sign-in failures return a machine-readable `code` (`email_not_confirmed`, `invalid_credentials`).
- Sign-in is portal-bound: a citizen account is accepted only on the citizen portal, staff/super administrators only on the organization portal, and a wrong-portal attempt returns `403` with `not_a_citizen_account` / `not_an_organization_account` and leaves no session behind.
- Unsafe requests without a valid CSRF token are rejected; a valid token is accepted.
- A personal access token is shown once, stored only as a hash, authenticates as a bearer credential, carries the owner's roles, is rejected after revocation, and never grants more than the owning account's permissions.
- Staff/organization endpoints require the `staff`/`superadmin` role; citizens receive `403`.
- Active application user, membership, and organization are required.
- Cross-organization case and document access is denied without data disclosure.
- PostgreSQL migrations apply to a clean disposable database using `civicflow_migrator`.
- Runtime database role can read application tables but cannot create schema objects or bypass the tested role boundary.
- Migration role is not used by the running API.
- Production endpoint behavior is verified with `BusinessEndpoints:Enabled` both `false` and `true`.
