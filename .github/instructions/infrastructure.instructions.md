---
description: "Persistence, AEAT adapter, Outbox and external integration rules for gesFactu.Infrastructure"
applyTo: "src/Infrastructure/gesFactu.Infrastructure/**/*.cs"
---

# gesFactu.Infrastructure

Infrastructure implements technical concerns and Application/Domain ports.

Allowed concerns include:

- EF Core
- SQL Server / configured relational provider
- migrations
- repositories when justified
- AEAT SOAP integration
- WSDL-generated types
- XML serialization
- XSD validation
- electronic certificates
- hash implementation when assigned to Infrastructure
- QR implementation
- Outbox
- background processing
- resilience policies
- external HTTP/SOAP clients
- observability implementation

## AEAT Anti-Corruption Layer

AEAT-generated SOAP/WSDL types MUST stay in Infrastructure.

Never return them from an Infrastructure port.

Map them explicitly to internal Application/Domain-facing models.

Do not put business use-case orchestration inside the SOAP client or repository.

Do not manually concatenate fiscal XML.

Before changing AEAT mappings, namespaces, operations, hash input, retry behavior, or error mapping, inspect `/VERIFACTU`.

## Persistence

Use EF Core configurations in Infrastructure.

Do not introduce a generic repository merely to wrap every DbSet.

Use aggregate-specific repositories or persistence abstractions when they add real value.

Use explicit transactions for chain creation/idempotency/Outbox atomicity where required.

Do not hold a database transaction open across a slow AEAT network call unless explicitly justified.

## Outbox

Fiscal record + Outbox message should be committed atomically.

Outbox processing must support:

- safe retry
- multiple workers/API instances
- durable status
- duplicate-delivery protection
- bounded attempts/policy as designed

Do not use an in-memory queue as the only durable work source.

## Resilience

Classify transient vs permanent failures.

Retry transient communication/service failures only.

Do not retry permanent AEAT validation/business rejection blindly.

Avoid nested retry policies.

Use bounded timeout/backoff and circuit breaker where appropriate.

## Security

Never persist or log certificate passwords, private keys, bearer tokens, or secrets in plaintext application logs.

Never add real production certificates to the repository.

Configuration must come from secure configuration/secret mechanisms.

## Concurrency

Record chaining must be safe under concurrent requests and multiple application instances.

Do not rely on in-process locks as the sole protection for a database-backed fiscal chain.

Use a database concurrency/transaction strategy appropriate to the actual provider and test it against a relational test database.
