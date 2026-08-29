---
description: "Mandatory AEAT VERI*FACTU rules for fiscal integration code"
applyTo: "src/**/VeriFactu/**/*.cs,src/**/Verifactu/**/*.cs,src/**/AEAT/**/*.cs,src/**/Aeat/**/*.cs,src/**/*VeriFactu*.cs,src/**/*Verifactu*.cs,src/**/*AEAT*.cs,src/**/*Aeat*.cs"
---

# VERI*FACTU-specific implementation rules

Before editing this code, inspect the relevant official documents under `/VERIFACTU`.

Official local AEAT documentation is the source of truth.

Never invent:

- field names or meanings
- allowed enum values
- XML namespaces
- SOAP operations
- mandatory/optional field semantics
- invoice type behavior
- cancellation behavior
- hash input or field order
- separators/normalization used for hash
- QR content
- date/decimal formatting
- validation rules
- error-code semantics
- resubmission behavior

## Hash

Maintain exactly one authoritative hash implementation.

Hash output must be deterministic and culture-independent.

Use exact AEAT-specified formatting and encoding.

Update deterministic tests whenever hash behavior changes.

Prefer official AEAT examples as test vectors.

## Chaining

Chaining must be transaction/concurrency safe.

The chain scope must follow official AEAT rules.

Do not assume one global taxpayer/chain.

Do not use an in-process lock as the sole protection in a multi-instance service.

## XML / SOAP

AEAT generated contract types remain in Infrastructure.

Do not hand-build fiscal XML with string concatenation.

Use explicit mapping and serialization.

Validate produced XML against official XSD where practical.

Treat WSDL/XSD as transport contracts; do not leak them into Domain/Application/API.

## Submission and retry

A retry is a new submission attempt, not a new fiscal record.

Preserve idempotency.

Retry only transient failures.

Persist enough submission/attempt state to recover safely after process failure.

Do not mark submission successful until the relevant durable response/state has been stored.

## Ambiguity

If official documentation is ambiguous or conflicts with another official source, stop and report the exact conflict before changing fiscal behavior.
