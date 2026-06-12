# ADR-0001 - Pragmatic Clean Architecture

## Status
Accepted

## Context
The starter repository placed all backend behavior in one Web API project. The
assignment evaluates code quality and maintainability but remains a small system.

## Decision
Use four production projects: Domain, Application, Infrastructure and WebApi.
Avoid mediator libraries, generic repositories and extra shared projects.

## Consequences
- Business rules are independently testable.
- Framework and database details remain at the edges.
- The solution contains more projects, but each has a clear responsibility.

## Reversal
Projects can be merged if the system remains permanently trivial, without
changing public HTTP contracts.
