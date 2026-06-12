# Loan Management System

A backend-focused full-stack implementation built with .NET 10, Angular 22,
Entity Framework Core, SQL Server and Docker Compose.

## Features

- Create, retrieve and list loans.
- Apply payments without allowing negative balances.
- Automatically mark fully paid loans as `paid`.
- Configurable loan products with independent amount ranges, minimum payment
  rules and enable/disable switches.
- JWT bearer authentication for every loan endpoint.
- SQL Server persistence, deterministic seed data and optimistic concurrency.
- RFC 9457 Problem Details responses with trace identifiers.
- Structured JSON logs, health checks and rate limiting.
- Responsive Angular operations UI with sign-in, create-loan, payment, loading,
  filtering, pagination, loan details, error and empty states.
- Unit tests, HTTP contract tests, SQL Server integration tests, Docker images
  and GitHub Actions CI.
- Azure-ready infrastructure as code, immutable image release workflow,
  deployment smoke tests and an operations runbook.

## Architecture

The backend follows pragmatic Clean Architecture:

- `Fundo.Domain`: entities and business invariants.
- `Fundo.Application`: use cases, contracts and persistence ports.
- `Fundo.Infrastructure`: EF Core, SQL Server and repository adapters.
- `Fundo.Applications.WebApi`: HTTP, authentication and observability.

Dependencies point inward. The domain has no framework dependency. See
[`docs/architecture`](docs/architecture) for C4 diagrams and decision records.

## Cloud Readiness

The local Docker Compose setup is not the deployment ceiling. The repository
also includes:

- `infra/azure/main.bicep` for Azure Container Apps, Azure SQL, Key Vault,
  managed identities and Log Analytics.
- `.github/workflows/release.yml` for manual environment releases with quality
  gates, Git SHA image tags, SBOM/provenance, Azure OIDC deployment and smoke
  tests against the deployed URL.
- `docs/operations/runbook.md` for health checks, incident triage, rollback and
  secret rotation.
- `docs/architecture/c4/05-deployment-cloud.puml` for the cloud deployment
  view.

The frontend container reads `API_UPSTREAM` at startup, so the same image can
proxy to the Compose API service locally or to the internal API Container App in
cloud.

## Requirements

- .NET SDK 10.0.301 or newer 10.0 feature-band SDK.
- Node.js 24 LTS.
- Docker Desktop or Docker Engine with Compose v2.

## Run With Docker

1. Copy `.env.example` to `.env`.
2. Replace every placeholder with a strong local value.
3. Start the stack:

```bash
docker compose up --build -d
docker compose ps
```

Open `http://localhost:8080` and sign in with `APP_USERNAME` and
`APP_PASSWORD` from `.env`.

Stop the stack without deleting data:

```bash
docker compose down
```

## Run Natively

Start SQL Server and configure these values through environment variables or
.NET user secrets:

```text
ConnectionStrings__LoansDatabase
Authentication__SigningKey
Authentication__Username
Authentication__Password
```

The signing key must contain at least 32 characters.

Run the API:

```bash
dotnet run --project backend/src/Fundo.Applications.WebApi
```

Run the frontend in another terminal:

```bash
cd frontend
npm ci
npm start
```

The Angular development server proxies `/api` to `http://localhost:5000`.

## API

The Angular frontend signs in through `/auth/session`, which issues an
HttpOnly, SameSite cookie. Cookie-authenticated write requests must include the
`X-Requested-With: XMLHttpRequest` header, added by the Angular interceptor, to
reduce cross-site write risk.

The built-in assessment identity supports two permission levels:

- Operator credentials receive `loans.read` and `loans.write`.
- Optional read-only credentials receive only `loans.read`.

API clients can obtain a bearer token:

```http
POST /auth/token
Content-Type: application/json

{
  "username": "configured-user",
  "password": "configured-password"
}
```

Send the returned token as `Authorization: Bearer <token>`.

| Method | Route | Result |
|---|---|---|
| `POST` | `/loans` | Creates a loan and returns `201 Created` |
| `GET` | `/loans/{id}` | Returns a loan or `404 Not Found` |
| `GET` | `/loans` | Lists loans with pagination and filters |
| `POST` | `/loans/{id}/payment` | Applies a payment or returns `409 Conflict` |

List query parameters:

| Name | Description |
|---|---|
| `page` | 1-based page number. Defaults to `1`. |
| `pageSize` | Page size from `1` to `50`. Defaults to `10`. |
| `status` | Optional `active` or `paid`. |
| `type` | Optional `personal`, `small-business` or `bridge`. |
| `applicantName` | Optional applicant-name contains filter. |
| `minimumBalance` | Optional minimum current balance. |
| `highExposureOnly` | Optional flag for balances at or above 50,000. |

Create request:

```json
{
  "amount": 1500.00,
  "applicantName": "Maria Silva",
  "type": "personal"
}
```

`type` is optional and defaults to `personal`. Supported products are
`personal`, `small-business` and `bridge`. Their limits are configured under
`LoanProducts` and can be changed without code changes:

- `personal`: 500.00 to 25,000.00, minimum partial payment 25.00.
- `small-business`: 10,000.00 to 250,000.00, minimum partial payment 100.00.
- `bridge`: 50,000.00 to 1,000,000.00, minimum partial payment 500.00.

Each product has an `enabled` switch. Disabling a product blocks new loans for
that product while still allowing existing loans to be serviced.

Payment request:

```json
{
  "amount": 250.00
}
```

## Validation

```bash
dotnet restore backend/src/src.sln
dotnet build backend/src/src.sln --no-restore
dotnet test backend/src/src.sln --no-build
backend/src/coverage-gate.ps1 -SearchRoot backend/src -MinimumLineCoverage 90
dotnet format backend/src/src.sln --verify-no-changes
dotnet list backend/src/src.sln package --vulnerable --include-transitive

cd frontend
npm ci
npm test -- --watch=false --browsers=ChromeHeadless --code-coverage
npm run build
npm audit --audit-level=moderate
```

Current local verification covers 68 backend tests with 92.21% combined line
coverage for handwritten backend code, plus 26 Angular tests with 92.35% line
coverage for the instrumented frontend code.

## Security Notes

- Secrets are never stored in source-controlled configuration.
- Input DTOs prevent clients from setting balances or statuses.
- JWT issuer, audience, signature and lifetime are validated.
- The browser UI uses HttpOnly cookie sessions instead of storing bearer tokens
  in `localStorage` or `sessionStorage`.
- Cookie-authenticated write requests require an application-originated header.
- Loan endpoints are protected by read/write authorization policies rather than
  a single "any authenticated user" rule.
- Credential comparisons use fixed-time hash comparison.
- Logs exclude applicant names, credentials and request payloads.
- CORS origins are configured explicitly.
- Same-origin Nginx/proxy routing keeps browser traffic under `/api` and avoids
  exposing backend internals to the frontend runtime.
- API and frontend runtime containers run without root privileges.
- The frontend serves a restrictive Content Security Policy and common browser
  hardening headers.
- GitHub Actions scans for high-risk secret patterns, audits dependencies and
  creates a protected preview deployment manifest after container smoke tests.

The included credential provider is suitable for a self-contained assessment,
not multi-user production identity. A production deployment should integrate an
OpenID Connect provider and role/resource authorization.

## Trade-offs And Future Improvements

- Offset pagination is implemented for the assessment. Large production
  portfolios may move to cursor pagination once stable sort keys and UX needs
  are known.
- Payment requests are concurrency-safe through SQL Server `rowversion`, but a
  client-facing idempotency key would improve retry behavior.
- Migrations may run at API startup only when explicitly enabled for the local
  Compose environment. Production should use a dedicated migration job.
- The release workflow can deploy Azure infrastructure when the target
  environment secrets are configured. Production should replace the template's
  SQL public firewall rule with private networking once the subscription
  topology is known.
- Angular 22 production and development dependencies audit clean. The project
  uses the current `@angular/build` builders and does not retain deprecated
  animation or dynamic-bootstrap packages.
