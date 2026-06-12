# ADR-0002 - EF Core, SQL Server And Optimistic Concurrency

## Status
Accepted

## Context
Two requests may pay the same loan concurrently. A read-modify-write sequence
without concurrency protection can lose one update or create an invalid balance.

## Decision
Persist loans through EF Core and SQL Server. Configure money as `decimal(18,2)`
and a SQL Server `rowversion` shadow property as the concurrency token.

## Consequences
- Concurrent writes fail predictably with HTTP 409.
- Transactions remain short and no pessimistic lock is held across HTTP work.
- Clients must reload before retrying after a conflict.

## Reversal
The persistence adapter can adopt another provider if it offers an equivalent
optimistic concurrency mechanism.
