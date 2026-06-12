# ADR-0003 - Native Framework Capabilities First

## Status
Accepted

## Context
Small applications can accumulate packages for validation, logging, mediation,
test mocking and resilience that the platform already supports.

## Decision
Use native ASP.NET Core Problem Details, JWT authentication, `ILogger`, rate
limiting, health checks and native validation code. Use hand-written test fakes.
External production packages are limited to official Microsoft database,
authentication and OpenAPI components.

## Consequences
- Dependency and vulnerability surfaces are smaller.
- Core behavior is explicit and easy to debug.
- Some infrastructure code is written locally instead of configured by a library.
