# Pipeline

GitHub Actions runs a small but production-minded delivery flow:

1. `risk-review` scans for high-risk secret patterns before build jobs start.
2. `backend` restores, verifies formatting, builds Release, runs tests, enforces
   a 90% combined line-coverage gate and scans direct/transitive NuGet
   packages for known vulnerabilities.
3. `frontend` performs a clean npm install, headless tests with coverage,
   production build and complete npm audit.
4. `container-smoke` builds the Docker images, starts SQL Server/API/frontend,
   signs in through the Nginx proxy, verifies bearer-token paginated listing,
   verifies read-only users are forbidden from writes, verifies HttpOnly cookie
   session login, confirms cookie writes without the application header are
   rejected, then creates a loan, retrieves it by id and applies a payment.
5. `deploy-preview` runs only on `main` or manual dispatch. It uses a protected
   GitHub environment, renders the Compose deployment manifest and stores it as
   an artifact after the smoke test gate.

The workflow uses read-only repository permissions, concurrency cancellation
and ephemeral CI credentials. The preview deploy stage is intentionally a
deployment-readiness gate rather than a fake cloud deploy, because no target
infrastructure is provided by the assessment.

## Release Workflow

`release.yml` is a manual workflow for environments that have cloud secrets
configured. It repeats the backend and frontend quality gates, publishes API and
frontend images to GHCR with immutable Git SHA tags, enables BuildKit SBOM and
provenance output, deploys `infra/azure/main.bicep` with Azure OIDC, and runs
health plus authenticated-list smoke tests against the deployed frontend URL.

Required GitHub environment variables:

- `AZURE_RESOURCE_GROUP`
- `AZURE_LOCATION`
- `REGISTRY_PULL_USERNAME`
- `APP_READONLY_USERNAME`

Required GitHub environment secrets:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `REGISTRY_PULL_PASSWORD`
- `SQL_ADMINISTRATOR_LOGIN`
- `SQL_ADMINISTRATOR_PASSWORD`
- `JWT_SIGNING_KEY`
- `APP_USERNAME`
- `APP_PASSWORD`
- `APP_READONLY_PASSWORD`

The Azure login uses GitHub OIDC rather than a long-lived Azure credential.
`REGISTRY_PULL_PASSWORD` is separate from `GITHUB_TOKEN` because the runtime
needs to pull images after the workflow has finished.

Production deployment checklist:

- Build and push signed API/frontend images to a protected registry.
- Generate and publish SBOM/provenance attestations.
- Run a dedicated migration job before application rollout.
- Require manual approval for environments that hold deployment secrets.
- Deploy to staging, run smoke tests against the public endpoint and keep a
  documented rollback path to the previous image tag.
