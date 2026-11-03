# Local Development

This guide runs the API and Vite development server on the host and uses
Docker Compose for PostgreSQL. It does not require the Compose API or frontend
containers.

## Prerequisites

- .NET 10 SDK.
- Node.js supported by Vite 8 (20.19+ or 22.12+).
- npm.
- Docker Engine/Desktop with the Docker Compose plugin.
- OpenSSL for generating local-only passwords and signing keys.
- Optional: `dotnet-ef` 10.0.0 for EF Core migrations.

From the repository root, confirm the .NET SDK and Docker Compose are
available:

```bash
dotnet --version
docker compose version
```

Install the EF Core command-line tool if `dotnet ef` is not already available:

```bash
dotnet tool install --global dotnet-ef --version 10.0.0
```

## 1. Configure and start PostgreSQL

Copy the example Compose environment file and replace every placeholder with
unique local-only values. Do not commit `.env`.

```bash
cp .env.example .env
openssl rand -base64 36
```

Generate separate values for `POSTGRES_PASSWORD`,
`CIVICFLOW_RUNTIME_PASSWORD`, and `CIVICFLOW_MIGRATOR_PASSWORD`, then paste
them into the ignored `.env` file. Start only the database service:

```bash
docker compose up -d postgres
docker compose ps postgres
```

PostgreSQL 17 is bound to loopback at port 5432 by default. Change
`POSTGRES_HOST_PORT` in `.env` if that port is occupied. The initialization
scripts create a database administrator, a migration role, and a restricted
runtime role. Initialization scripts run only when PostgreSQL creates a new
data directory.

## 2. Configure API secrets and apply migrations

The API project already has a .NET user-secrets ID. Generate a local JWT
signing key and keep it in user secrets rather than source control:

```bash
openssl rand -base64 48
dotnet user-secrets set "Jwt:SigningKey" "<paste-the-generated-value>" \
  --project PublicServiceCaseManagement.Api
dotnet user-secrets set "BusinessEndpoints:Enabled" "true" \
  --project PublicServiceCaseManagement.Api
```

Set the migration connection temporarily for the EF command. Use the
`civicflow_migrator` password from the local `.env` file:

```bash
ConnectionStrings__CivicFlow='Host=localhost;Port=5432;Database=civicflow;Username=civicflow_migrator;Password=<local-migrator-password>' \
dotnet ef database update \
  --project PublicServiceCaseManagement.Api \
  --startup-project PublicServiceCaseManagement.Api
```

After migrations complete, configure the API to use the restricted runtime
role:

```bash
dotnet user-secrets set "ConnectionStrings:CivicFlow" \
  "Host=localhost;Port=5432;Database=civicflow;Username=civicflow_runtime;Password=<local-runtime-password>" \
  --project PublicServiceCaseManagement.Api
```

Migrations are not applied automatically at API startup. Never use the
migrator or database-administrator account as the API runtime connection.

### Optional local Platform Admin

To seed a local Platform Admin, set both the email and password in user
secrets. Identity confirms this bootstrap account at creation. Use a unique
local password that meets the Identity policy; do not place a real or reused
credential in documentation or source control.

```bash
dotnet user-secrets set "SeedAdmin:Email" "admin@example.test" \
  --project PublicServiceCaseManagement.Api
dotnet user-secrets set "SeedAdmin:Password" "<local-password>" \
  --project PublicServiceCaseManagement.Api
dotnet user-secrets set "SeedAdmin:FullName" "Local Platform Admin" \
  --project PublicServiceCaseManagement.Api
```

Leaving both email and password unset skips account seeding. A partial seed
configuration fails startup. Re-running the seeder does not replace an
existing administrator's changed password.

## 3. Run the API

The `PublicServiceCaseManagement.Api` launch profile sets Development and
listens on HTTP port 5001 and HTTPS port 7001:

```bash
dotnet run \
  --project PublicServiceCaseManagement.Api/PublicServiceCaseManagement.Api.csproj \
  --launch-profile PublicServiceCaseManagement.Api
```

The business endpoints setting defaults to `false`; the user-secrets setting
above enables organization workflows for local development. Health endpoints
remain available regardless of that flag.

When no SMTP relay is configured in Development, the API prints the full
message, including the recipient, subject, body, and one-time link, to its
console. Do not share or retain that output. Configure the TLS SMTP sender
(`Smtp:Host`, `Smtp:Port`, `Smtp:FromAddress`, `Smtp:UseSsl=true`) to deliver
real mail locally, or use the automated authentication tests, which capture
messages in memory.

## 4. Run the frontend

In another terminal:

```bash
cd civicflow-web
cp .env.example .env.local
npm ci
npm run dev
```

The default browser URL is `http://localhost:5173`, with API requests sent to
`http://localhost:5001`. The Vite server uses strict port behavior; if port
5173 is occupied, set `VITE_DEV_PORT` in the ignored `.env.local` file and
update the API's `Frontend:BaseUrl` user secret to the same browser origin.
Set `VITE_API_BASE_URL` when the API URL changes.

The frontend uses first-party cookies and does not store bearer tokens. In
Development, the API's CORS policy allows trusted loopback HTTP(S) origins;
do not replace it with a wildcard credentialed policy.

## 5. Enable local HTTPS

The API launch profile already listens on HTTPS port 7001. Trust the .NET
development certificate and verify it:

```bash
dotnet dev-certs https --trust
dotnet dev-certs https --check
```

To serve Vite over HTTPS too, export the development certificate to an ignored
local file. Choose a local export password and do not commit the certificate
or password:

```bash
mkdir -p civicflow-web/.certs
dotnet dev-certs https -ep civicflow-web/.certs/localhost.pfx \
  -p "<local-export-password>"
```

Edit `civicflow-web/.env.local`:

```dotenv
VITE_API_BASE_URL=https://localhost:7001
VITE_DEV_PORT=5173
VITE_DEV_HTTPS=true
VITE_DEV_CERTIFICATE=.certs/localhost.pfx
VITE_DEV_CERTIFICATE_PASSWORD=<local-export-password>
```

Set the API's frontend origin to match:

```bash
dotnet user-secrets set "Frontend:BaseUrl" "https://localhost:5173" \
  --project PublicServiceCaseManagement.Api
```

Restart Vite and use `https://localhost:5173`. Both browser and API origins
must use the same scheme for a consistent cookie/CSRF flow. `dotnet dev-certs
https --clean` removes cached .NET development certificates for the current
user; use it only when intentionally cleaning up local certificates, then
trust and export a replacement if needed.

## 6. Open Swagger

Swagger UI is enabled only in Development:

- HTTP: <http://localhost:5001/swagger>
- HTTPS: <https://localhost:7001/swagger>

The UI describes request and response schemas. For same-origin cookie
requests, its interceptor obtains a CSRF token before sending an unsafe API
request. API clients may instead use the documented bearer-token workflow.
See [API Contract and Authorization Matrix](API-AUTHORIZATION.md).

## 7. Build and test

From the repository root:

```bash
dotnet build PublicServiceCaseManagement.sln --configuration Release
dotnet test PublicServiceCaseManagement.sln --configuration Release
```

From `civicflow-web`:

```bash
npm run lint
npm run build
npm run typecheck:e2e
```

There is no standalone frontend unit-test script. Playwright browser tests
require Chromium and a disposable PostgreSQL database:

```bash
cd civicflow-web
npx playwright install chromium chromium-headless-shell
export CIVICFLOW_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=<disposable-port>;Database=<disposable-db>;Username=<admin>;Password=<local-test-password>'
npm run test:e2e
```

The PostgreSQL RLS tests are opt-in and use disposable admin/runtime
connections. Follow the connection and isolation instructions in
[RLS Design and Implementation](RLS-DESIGN.md). Never point RLS or E2E tests at
a shared, persistent, or production database.

## Configuration reference

| Setting | Purpose |
|---|---|
| `ConnectionStrings:CivicFlow` | PostgreSQL connection; use the migrator only for EF commands and runtime for the API. |
| `Jwt:SigningKey` | Local signing key for short-lived JWT access tokens; keep it in user secrets. |
| `BusinessEndpoints:Enabled` | Enables organization and platform business routes; default is `false`. |
| `SeedAdmin:Email`, `SeedAdmin:Password`, `SeedAdmin:FullName` | Optional local bootstrap account; email and password must both be configured or both omitted. |
| `Frontend:BaseUrl` | Browser origin used for verification and password-recovery links. |
| `VITE_API_BASE_URL` | API base URL used by the Vite frontend. |
| `VITE_DEV_PORT`, `VITE_DEV_HTTPS`, `VITE_DEV_CERTIFICATE`, `VITE_DEV_CERTIFICATE_PASSWORD` | Local Vite listener and HTTPS certificate configuration. |

Compose port defaults and environment-variable names are in
[`compose.yaml`](../compose.yaml), [`.env.example`](../.env.example), and
[`civicflow-web/.env.example`](../civicflow-web/.env.example). Never commit
`.env`, `.env.local`, development certificates, signing keys, SMTP
credentials, or connection strings containing passwords.
