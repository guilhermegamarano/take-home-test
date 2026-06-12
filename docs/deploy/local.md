# Local Deployment

## Docker Compose

1. Create `.env` from `.env.example` and replace all placeholders.
2. Run `docker compose up --build -d`.
3. Wait for `docker compose ps` to show healthy services.
4. Open `http://localhost:8080`.

The API applies migrations only because Compose sets
`Database__ApplyMigrations=true`. Production environments should run migrations
as a separate release step.

Compose sets the frontend runtime variable `API_UPSTREAM=http://api:8080`.
Cloud deployments set the same variable to an internal API endpoint, allowing
the Angular/Nginx image to move between environments without a rebuild.

## Browser Security Model

The frontend is served by Nginx and proxies API calls through `/api`, keeping
the browser on a same-origin boundary. The Angular app signs in with
`POST /api/auth/session`, receiving an HttpOnly SameSite cookie instead of a
JavaScript-readable bearer token. Write requests made with that cookie include
`X-Requested-With: XMLHttpRequest`, and the API rejects cookie-authenticated
writes without that header.

Operator credentials receive read/write loan permissions. Optional read-only
credentials can be configured with `APP_READONLY_USERNAME` and
`APP_READONLY_PASSWORD`; those users can list, filter, page and view loan
details but cannot create loans or apply payments.

## Health Checks

- `/api/health/live`: process liveness.
- `/api/health/ready`: SQL Server readiness.
- `/health`: Nginx/frontend health.

## Troubleshooting

- Confirm Docker Desktop or Docker Engine is running.
- Check `docker compose logs api sql-server`.
- SQL Server passwords must satisfy its complexity policy.
- JWT signing keys must contain at least 32 characters.

## Reset

`docker compose down` preserves data. `docker compose down -v` deletes the local
database volume and must be used only when a full reset is intended.
