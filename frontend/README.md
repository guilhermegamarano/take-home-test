# Frontend

Angular 22 standalone client for the Loan Management System.

## Run

```sh
npm ci
npm start
```

The development server proxies `/api` to `http://localhost:5000`.

## Validate

```sh
npm test -- --watch=false --browsers=ChromeHeadless --code-coverage
npm run build
npm audit --audit-level=moderate
```

The production container serves the compiled app through an unprivileged Nginx
runtime with security headers and a `/health` endpoint. The runtime proxy reads
`API_UPSTREAM`, allowing the same image to target Docker Compose locally or an
internal API endpoint in cloud.
