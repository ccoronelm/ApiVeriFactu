---
description: "CQRS, use-case and port rules for gesFactu.Application"
applyTo: "src/Core/gesFactu.Application/**/*.cs"
---

# gesFactu.Application

Application orchestrates use cases and defines ports toward Infrastructure.

## CQRS

Commands modify state.

Queries must be read-only.

Use MediatR if it is already established in the solution.

Prefer feature/use-case organization.

Handlers should be small and focused on orchestration.

Do not put fiscal domain rules in handlers when they belong in Domain.

## Boundaries

Application MUST NOT reference:

- `gesFactu.Infrastructure`
- `gesFactu.Api`
- EF Core provider-specific implementation
- AEAT WSDL/SOAP generated types
- XML serialization implementation
- certificate implementation
- raw HTTP/SOAP clients

Define interfaces/ports for external capabilities.

Before creating a new abstraction, search for an existing equivalent.

Do not create competing `Result`, repository, mapping, or time abstractions.

## Validation and errors

Use application validation for use-case preconditions.

Keep transport validation in API and intrinsic invariants in Domain.

Use expected Result/error outcomes instead of exceptions for routine validation/business failures.

Do not leak SOAP/Infrastructure exceptions.

## Async

Propagate `CancellationToken`.

No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`, or fire-and-forget fiscal tasks.

## VERI*FACTU

For fiscal use cases, inspect `/VERIFACTU` before implementing behavior.

Application may depend on an `IVeriFactuGateway`-style port, but it must not depend on AEAT-generated contract types.

Application DTOs/results are internal contracts and must isolate callers from AEAT transport models.

Consider idempotency, concurrency, and transaction boundaries for every command that creates or changes fiscal/submission state.
