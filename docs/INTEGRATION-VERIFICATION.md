# Integration verification

## Current evidence

The automated suite uses EF Core InMemory and a synthetic authentication handler for most API tests. A subset exercises the real ASP.NET Core Identity flow through the cookie scheme (with a cookie container and a capturing email sender): registration with email-confirmation gating, register → verify → login → `me`, wrong-password rejection, duplicate-email enumeration safety, lockout (`423`), forgot-password/reset-password, change-password, and logout. Dedicated anti-forgery tests assert that unsafe requests without a valid `X-CSRF-TOKEN` are rejected (`400`) and that a valid token is accepted.

PostgreSQL migrations and runtime-role restrictions have been verified against a disposable live stack: migrations applied with `civicflow_migrator`, and `civicflow_runtime` could read application tables but could not create schema objects. See [`RLS-DESIGN.md`](RLS-DESIGN.md) for the tenant-boundary design. These live probes are not yet CI integration tests.

Citizen self-service profile endpoints (`/api/citizens/me`) are covered by automated integration tests using the synthetic authentication handler: anonymous requests return 401, staff requests return 403, unlinked identities return 401, create/update behavior and duplicate validation are asserted, and the endpoints honour `BusinessEndpoints:Enabled` gating.

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

Email verification and password-reset messages are produced by `IEmailSender`. The default `LoggingEmailSender` writes them to the application log (including the link), which is sufficient for local verification without an SMTP server; replace it with a real implementation for production.

Do not run the migration command against a persistent or production database. Do not put database passwords, email credentials, or any tokens in the repository, test output, or documentation.

## Required acceptance evidence

- Registration cannot sign in until the email is confirmed; the confirmation link enables sign-in.
- Wrong credentials are rejected with `401`; unconfirmed email returns `403`; lockout returns `423`.
- Registration and password-recovery responses do not disclose whether an email exists.
- Unsafe requests without a valid CSRF token are rejected; a valid token is accepted.
- Staff/organization endpoints require the `staff`/`superadmin` role; citizens receive `403`.
- Active application user, membership, and organization are required.
- Cross-organization case and document access is denied without data disclosure.
- PostgreSQL migrations apply to a clean disposable database using `civicflow_migrator`.
- Runtime database role can read application tables but cannot create schema objects or bypass the tested role boundary.
- Migration role is not used by the running API.
- Production endpoint behavior is verified with `BusinessEndpoints:Enabled` both `false` and `true`.
