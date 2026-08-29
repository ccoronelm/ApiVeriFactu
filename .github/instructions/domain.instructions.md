---
description: "Clean Architecture and DDD rules for gesFactu.Domain"
applyTo: "src/Core/gesFactu.Domain/**/*.cs"
---

# gesFactu.Domain

This is the innermost project.

## Dependencies

`gesFactu.Domain` MUST NOT reference:

- `gesFactu.Application`
- `gesFactu.Infrastructure`
- `gesFactu.Api`
- Entity Framework Core
- ASP.NET Core
- MediatR handlers
- SOAP/WSDL generated classes
- XML serialization infrastructure
- database providers
- certificate APIs
- HTTP clients

Keep Domain persistence-ignorant.

## Modeling

Use entities, aggregates, Value Objects, domain events, domain services, and precise enums only when they express real domain concepts.

Prefer rich domain behavior over public setters and anemic entities.

Protect invariants in constructors/factories/domain methods.

Prefer explicit state transitions over `entity.Estado = ...`.

Do not implement generic update/delete operations for immutable fiscal history.

Corrections/cancellations must use explicit domain operations that match official VERI*FACTU/legal behavior.

## Value Objects

Use Value Objects for meaningful concepts such as NIF, invoice identifiers, series, hash values, money, tax rate, and software identifiers when they protect invariants or improve type safety.

Use `decimal` for money/tax amounts.

Do not use culture-sensitive formatting in fiscal logic.

Do not create meaningless one-property wrappers just to claim DDD compliance.

## Fiscal rules

If a Domain rule depends on VERI*FACTU, inspect `/VERIFACTU` before implementing it.

Do not place AEAT wire-contract details in Domain unless they are genuinely part of the business/fiscal concept independent of SOAP/XML.

Domain must not know WSDL class names, XML element names, SOAP operations, certificate details, or transport-specific error classes.
