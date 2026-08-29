---
description: "REST API boundary rules for gesFactu.Api"
applyTo: "src/Api/gesFactu.Api/**/*.cs"
---

# gesFactu.Api

The API is the REST/JSON facade used by client applications.

## Thin boundary

Controllers/endpoints may:

- receive HTTP input
- perform transport-level validation
- map request contracts to Application commands/queries
- dispatch the use case
- map results to HTTP responses

Controllers/endpoints MUST NOT:

- calculate VERI*FACTU hash
- determine previous chain record
- generate fiscal XML
- call AEAT SOAP directly
- access `DbContext` directly
- load certificates
- implement retry/outbox behavior
- contain domain/fiscal rules

## Contracts

Use explicit request/response DTOs.

Do not expose:

- Domain entities directly
- EF Core entities directly
- AEAT/WSDL generated types
- raw SOAP responses/exceptions

Client applications must not be required to send implementation-owned values such as hash, previous hash, SOAP XML, AEAT namespaces, certificate paths/passwords, or internal submission-attempt details.

Prefer versioned routes (`/api/v1/...`).

Use consistent `ProblemDetails` error responses.

Never expose stack traces or secrets.

## Async and observability

Propagate `CancellationToken`.

Use/generate a correlation identifier consistently.

Log structured identifiers, not full sensitive fiscal payloads.

## Compatibility

gesFactu isolates consumers from AEAT implementation changes.

Do not let an AEAT contract change automatically become a breaking public API change.

Version deliberate breaking API changes.
