---
description: "Testing rules for gesFactu"
applyTo: "tests/**/*.cs,src/**/*Tests/**/*.cs,src/**/*Test/**/*.cs"
---

# Testing rules

Tests must be deterministic, readable, and focused on behavior.

Use Arrange / Act / Assert consistently unless the project already follows another clear convention.

## Required fiscal coverage

When applicable, test:

- Value Object validation
- domain invariants
- state transitions
- registration records
- cancellation records
- hash generation
- official AEAT hash examples
- exact culture-independent formatting
- chain creation
- concurrent chain creation
- idempotent duplicate requests
- AEAT request mapping
- AEAT response/error mapping
- XML namespaces
- mandatory XML elements
- decimal/date formats
- XSD validation
- transient communication failures
- permanent validation/business rejection
- retry policy behavior
- Outbox duplicate processing
- multi-worker Outbox safety
- persistence transactions/concurrency

## Database tests

Do not use EF Core InMemory as proof of relational behavior.

For transactions, unique constraints, row-version/concurrency, locking, indexes, and SQL-provider behavior, use an integration test against an appropriate relational database/provider.

## External AEAT tests

Do not make ordinary unit tests depend on live AEAT.

Wrap external AEAT access behind a port.

Use contract fixtures/mappings and controlled integration environments for external-service tests.

## Official documentation

Fiscal expected values must come from `/VERIFACTU` or from clearly derived rules.

Do not invent expected hash/XML values just to make a test pass.

When official examples are available, preserve them as immutable fixtures where repository policy permits.
