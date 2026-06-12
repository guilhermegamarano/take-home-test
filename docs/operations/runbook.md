# Operations Runbook

This runbook describes the minimum operational flow for the loan management
system in local, preview or cloud-hosted environments.

## Health Checks

- Frontend: `GET /health`
- API liveness: `GET /api/health/live` through the frontend proxy or
  `/health/live` directly on the API.
- API readiness: `GET /api/health/ready` through the frontend proxy or
  `/health/ready` directly on the API.

Readiness checks SQL Server connectivity. Liveness checks only the API process.

## Smoke Test

```bash
curl --fail https://example-url/health
curl --fail --cookie-jar cookies.txt \
  --header "Content-Type: application/json" \
  --header "X-Requested-With: XMLHttpRequest" \
  --data '{"username":"reviewer","password":"change-me"}' \
  https://example-url/api/auth/session
curl --fail --cookie cookies.txt \
  "https://example-url/api/loans?page=1&pageSize=1"
```

For write-path validation, create a small valid personal loan and apply a
minimum-valid payment. Use test identities only.

## Incident Triage

1. Check frontend health.
2. Check API liveness.
3. Check API readiness.
4. Inspect recent API logs by `traceId` from Problem Details responses.
5. Check SQL Server availability and connection failures.
6. Confirm deployment revision, image tags and recent configuration changes.

## Common Failures

### Authentication Fails

- Verify `Authentication__SigningKey`, usernames and passwords were supplied by
  the environment secret store.
- Confirm the browser is using the same public origin for login and subsequent
  requests.
- Check that session cookies are HttpOnly, SameSite and scoped to `/`.

### Writes Return 403

- Confirm the signed-in identity has `loans.write`.
- Cookie-authenticated writes must include
  `X-Requested-With: XMLHttpRequest`.
- Read-only reviewer identities are expected to fail write attempts.

### SQL Readiness Fails

- Confirm the connection string secret is present.
- Confirm firewall/private networking allows the API runtime to reach SQL.
- Review SQL database service health and login failures.

### Payment Conflicts

- A `409 Conflict` can be valid business behavior: paid loans cannot be paid
  again, overpayments are rejected and concurrent updates are protected by
  rowversion.
- Ask the operator to refresh loan details before retrying.

## Deployment

The release workflow publishes immutable images tagged by Git SHA, deploys the
Azure Bicep template and smoke-tests the public frontend URL.

Production deployment should run database migrations before shifting traffic.
This repository keeps migrations explicit so schema changes are visible during
review and are not hidden inside the regular request path.

## Rollback

1. Identify the last known-good API and frontend image tags.
2. Re-run the release workflow with the previous tags or redeploy the Bicep
   template with those image values.
3. Run health and authenticated list smoke tests.
4. Review whether any database migration is backward incompatible before
   rolling application code back.

## Secret Rotation

Rotate these values through the target environment secret store:

- `JWT_SIGNING_KEY`
- `APP_PASSWORD`
- `APP_READONLY_PASSWORD`
- `SQL_ADMINISTRATOR_PASSWORD`
- `REGISTRY_PULL_PASSWORD`

After rotating the JWT signing key, active sessions and bearer tokens should be
treated as invalid.

## Useful Log Queries

Look for these structured fields or message fragments:

- `traceId` from Problem Details responses.
- Authentication failures by status code `401` or `403`.
- SQL health check failures.
- Payment operations returning conflict.
- Deployment revision and image tag around the incident window.
