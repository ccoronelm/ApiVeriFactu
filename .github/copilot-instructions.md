# gesFactu — GitHub Copilot Repository Instructions

## Purpose

`gesFactu` is a .NET API that acts as a fiscal integration boundary between client applications and the Spanish AEAT VERI*FACTU services.

Client applications communicate with `gesFactu` through REST/JSON. They must not need to know or implement AEAT SOAP details, WSDL/XSD contracts, XML serialization, electronic certificates, billing-record hash generation, record chaining, QR rules, AEAT retry semantics, or AEAT-generated request/response types.

All VERI*FACTU regulatory and AEAT communication concerns are encapsulated by this API.

When answering the developer, use Spanish unless the developer asks for another language. Keep code identifiers consistent with the existing codebase; do not mix Spanish and English naming arbitrarily.

---

## 1. Repository layout

The repository root is `backend`.

Main paths:

- `/src/Api/gesFactu.Api`
- `/src/Core/gesFactu.Application`
- `/src/Core/gesFactu.Domain`
- `/src/Infrastructure/gesFactu.Infrastructure`
- `/VERIFACTU`
- `/.github`

Do not invent alternative project locations when these paths exist.

---

## 2. Official VERI*FACTU documentation is the source of truth

Official AEAT / Spanish Tax Agency VERI*FACTU documentation is stored under:

`/VERIFACTU`

Before implementing, modifying, reviewing, or fixing any functionality that depends on VERI*FACTU rules, inspect the relevant documentation in `/VERIFACTU` first.

This includes, but is not limited to:

- billing records
- registration records
- cancellation records
- hash / huella generation
- record chaining
- invoice types
- tax breakdowns
- rounding and decimal rules
- QR requirements and contents
- software identification
- timestamps and date formats
- XML structures
- namespaces
- XSD schemas
- WSDL definitions
- SOAP operations
- validation rules
- accepted values and enumerations
- AEAT error codes and response semantics
- retry / resubmission behavior
- examples supplied by AEAT

Never invent VERI*FACTU rules.

Never implement a fiscal rule only from model memory or general accounting knowledge when an official local specification exists.

Existing code is not authoritative for fiscal rules. If code conflicts with official AEAT documentation, identify the discrepancy and follow the official requirement unless the developer explicitly decides otherwise.

If the local documentation is incomplete, ambiguous, contradictory, unreadable, or does not cover the requested behavior, do not guess. State exactly what is missing or conflicting.

If web access is used because the local documentation is insufficient, use official AEAT / Spanish government sources only for normative or technical VERI*FACTU requirements.

Do not modify, rename, move, or delete files under `/VERIFACTU` unless the developer explicitly asks for that change.

### Documentation priority

Use this order when deciding implementation details:

1. Official regulation / ministerial order / official AEAT technical specification in `/VERIFACTU`
2. Official XSD/WSDL for wire-level structure and service contracts
3. Official AEAT examples
4. Official AEAT FAQs / explanatory documentation
5. Existing gesFactu implementation

If two official sources appear to conflict, stop and surface the conflict instead of silently choosing one.

### Mandatory documentation-first workflow

For any VERI*FACTU-dependent task:

1. Find and inspect the relevant official files under `/VERIFACTU`.
2. Identify the exact rule, format, field, operation, or validation involved.
3. Inspect the existing gesFactu implementation and conventions.
4. Determine the correct Clean Architecture layer.
5. Reuse existing abstractions whenever appropriate.
6. Implement the smallest coherent change.
7. Add or update automated tests.
8. When useful, leave a concise code comment naming the official document/section that explains a non-obvious fiscal rule.

Do not implement first and check the documentation afterwards.

---

## 3. Architecture

The solution follows Clean Architecture with pragmatic DDD and CQRS.

Dependency direction:

- `gesFactu.Domain` depends on no other project in the solution.
- `gesFactu.Application` may depend on `gesFactu.Domain`.
- `gesFactu.Infrastructure` may depend on `gesFactu.Application` and `gesFactu.Domain`.
- `gesFactu.Api` may depend on `gesFactu.Application` and `gesFactu.Infrastructure` for composition/DI.
- Avoid a direct `gesFactu.Api -> gesFactu.Domain` dependency unless there is a concrete, documented reason.

Never introduce a reverse dependency for convenience.

External systems, databases, SOAP, certificates, XML serialization, filesystem access, clocks, queues, and network clients are infrastructure concerns.

The application core must remain testable without AEAT, a real database, a certificate, or an HTTP server.

---

## 4. Architectural style and patterns

Use these patterns when they solve a concrete problem:

- Clean Architecture
- Domain-Driven Design tactical patterns
- CQRS
- MediatR if already established in the solution
- Ports and Adapters
- Anti-Corruption Layer for AEAT
- Result Pattern for expected outcomes
- Value Objects for meaningful fiscal concepts
- Aggregate boundaries and domain methods for invariants
- Transactional Outbox for reliable AEAT submission
- Idempotency
- Explicit transaction boundaries
- Domain Events where they decouple meaningful domain behavior
- Strategy only when behavior genuinely varies
- State transitions / state machine semantics for fiscal/submission lifecycle
- Retry with backoff for transient external failures
- Circuit breaker where appropriate

Do not add patterns merely to increase abstraction.

Do not introduce Event Sourcing unless the developer explicitly chooses it.

Avoid a generic repository abstraction over EF Core merely for convention. Prefer aggregate-specific repositories or an application persistence abstraction only when they add value.

Do not create an interface for every class. Create abstractions at architectural boundaries, for replaceable behavior, or for testability when justified.

---

## 5. Domain layer

Path:

`/src/Core/gesFactu.Domain`

The Domain layer may contain:

- Entities
- Aggregates
- Value Objects
- Domain Events
- Domain Services
- Domain errors/exceptions
- Enums
- Specifications only when a rule benefits from them

The Domain layer must not depend on:

- Entity Framework Core
- ASP.NET Core
- controllers/endpoints
- MediatR handlers
- HTTP
- SOAP
- XML serialization
- AEAT-generated classes
- WSDL-generated classes
- database providers
- certificate APIs
- logging implementations
- Infrastructure
- Application
- Api

Prefer rich domain models over anemic property bags.

Entities must protect their invariants.

Avoid public setters on meaningful fiscal state. Prefer explicit intent-revealing methods such as:

- `Crear(...)`
- `Anular(...)`
- `MarcarPendienteEnvio(...)`
- `MarcarAceptado(...)`
- `MarcarRechazado(...)`

Use the existing naming language/style in the project.

Do not implement generic CRUD update/delete behavior for immutable fiscal history.

Corrections, cancellations, or rectifications must be represented by the appropriate domain/legal operation, not by silently rewriting historical records.

---

## 6. Value Objects and primitive safety

Avoid primitive obsession for meaningful fiscal concepts.

Consider Value Objects for concepts such as:

- NIF / tax identifier
- invoice number
- invoice series
- invoice identifier
- billing-record hash
- money
- tax rate
- tax amount
- software identifier
- fiscal period/date where behavior justifies it

Value Objects should be immutable and use value equality.

Validate intrinsic invariants at creation time when appropriate.

Do not wrap every primitive mechanically. A Value Object must represent a real concept or protect an invariant.

For money/tax values use `decimal`, not `float` or `double`.

Never rely on current culture for fiscal serialization, hash input, decimal formatting, or date formatting. Use the exact AEAT-required representation and `InvariantCulture` where applicable.

Rounding and scale must follow official documentation. Do not invent rounding rules.

---

## 7. Application layer

Path:

`/src/Core/gesFactu.Application`

The Application layer contains use cases and ports.

Use CQRS:

- Commands change state.
- Queries are read-only.

Use MediatR for commands/queries if MediatR is established in the solution.

Prefer feature-oriented organization over giant technical folders.

Example:

```text
RegistrosFacturacion/
  Commands/
    CrearRegistro/
      CrearRegistroCommand.cs
      CrearRegistroCommandHandler.cs
      CrearRegistroCommandValidator.cs
  Queries/
    ObtenerRegistro/
      ObtenerRegistroQuery.cs
      ObtenerRegistroQueryHandler.cs
```

Handlers orchestrate use cases. They must not contain:

- SOAP calls
- XML serialization
- certificate loading
- AEAT generated types
- ad-hoc hash algorithms
- database-provider-specific code
- HTTP-specific behavior
- duplicated domain rules

Application defines ports required from Infrastructure, for example:

- `IVeriFactuGateway`
- persistence abstractions
- hash-calculation abstraction if the algorithm is implemented outside Domain
- QR abstraction if required
- certificate/provider abstraction when needed by use cases
- time abstraction (`TimeProvider` is preferred where the target framework supports it)

Before creating a new interface, Result type, mapper, helper, repository, service, or Value Object, search the existing solution for an equivalent abstraction.

Propagate `CancellationToken` through all asynchronous I/O call chains.

Never use fire-and-forget tasks for fiscal work.

---

## 8. API layer

Path:

`/src/Api/gesFactu.Api`

The API is a REST/JSON facade for client applications.

Controllers/endpoints must be thin.

They may:

- receive/validate transport input
- map API contracts to Application commands/queries
- dispatch use cases
- map Application results to HTTP responses

They must not:

- calculate VERI*FACTU hashes
- decide chaining
- generate fiscal XML manually
- call AEAT SOAP directly
- use `DbContext` directly
- load certificates
- implement retry policies
- contain fiscal/domain rules

Use explicit request/response contracts. Do not expose Domain entities, EF entities, or AEAT SOAP/WSDL models directly.

Prefer versioned routes such as `/api/v1/...`.

Use consistent error responses, preferably ASP.NET Core `ProblemDetails`.

Never expose stack traces, certificate details, secrets, raw SOAP exceptions, or internal implementation details to API consumers.

Client applications should not normally provide fields that gesFactu owns, including:

- current record hash
- previous record hash
- AEAT XML
- SOAP namespaces
- certificate path/password
- internal submission-attempt data

---

## 9. AEAT Anti-Corruption Layer

AEAT is an external bounded integration concern.

All SOAP/WSDL/XSD-generated types must remain inside Infrastructure.

Preferred flow:

```text
Client App
  -> REST API Contract
  -> Application Command/Query
  -> Domain
  -> Application Port
  -> Infrastructure AEAT Adapter
  -> AEAT generated contract
  -> SOAP/XML
  -> AEAT
```

Response flow:

```text
AEAT response
  -> Infrastructure mapping
  -> internal result model
  -> Application result
  -> API response
```

Never leak AEAT-generated types into Domain, Application, or public API contracts.

Use explicit mappings at fiscal boundaries.

For critical fiscal fields, prefer explicit mapping over convention-based automatic mapping.

Do not silently ignore mandatory AEAT fields.

Do not build XML by string concatenation. Use typed/generated contracts or a deliberate serializer and validate against official schemas where practical.

---

## 10. Hash / huella

Hash generation is a critical fiscal algorithm.

There must be one authoritative implementation.

Never duplicate hash calculation in:

- controllers
- handlers
- repositories
- mappers
- background workers
- entities
- ad-hoc utilities

The exact:

- included fields
- field order
- separators
- normalization
- decimal formatting
- date/time formatting
- text encoding
- algorithm
- previous-record data

must follow `/VERIFACTU`.

Hash generation must be deterministic and culture-independent.

Add deterministic unit tests.

Use official AEAT examples/test vectors when available.

Any change to hash behavior requires reviewing the official documentation and updating tests.

---

## 11. Billing-record chaining and concurrency

Record chaining is concurrency-sensitive.

Never implement chaining as an unprotected:

1. read last record
2. calculate next hash
3. insert new record

The operation must remain correct under concurrent requests and multiple API instances.

The chain scope/partition must follow official AEAT rules. Do not assume one global chain and do not assume a single taxpayer.

When a use case requires atomicity, protect the operation with an appropriate database transaction/concurrency strategy supported by the actual database provider.

The following operations must be atomic when required:

- resolve the previous record in the correct chain
- create the new record
- compute/store its chain/hash data
- persist the record
- persist its Outbox message

Do not hold a database transaction open while waiting for AEAT unless there is a specific, documented reason.

Add concurrency-focused integration tests.

---

## 12. Idempotency

Fiscal creation/submission operations must be idempotent where required.

A repeated logical request or retry must not:

- create duplicate fiscal records
- create duplicate chain links
- generate a second logical invoice record
- produce inconsistent hashes
- create duplicate submission work

Separate the fiscal record from transport/submission attempts.

A communication retry is not a new billing record.

Design idempotency around stable business/fiscal identifiers and official rules, not around random request timing.

If an idempotency key is exposed by the API, define its scope, persistence, replay behavior, and conflict behavior explicitly.

---

## 13. Persistence, Unit of Work and Outbox

EF Core belongs in Infrastructure.

Use explicit transactions for operations that require atomicity.

`DbContext` already provides Unit of Work semantics; do not wrap it in a redundant generic Unit of Work unless the application boundary genuinely benefits from it.

Use a Transactional Outbox for reliable AEAT submission.

Preferred flow:

```text
Database transaction:
  persist fiscal record
  persist Outbox message
commit

Outbox processor:
  claim message safely
  send to AEAT
  persist response/attempt/status
```

The Outbox processor must be safe under retries and multiple worker instances.

Do not use an in-memory queue as the only durable source of fiscal submission work.

Do not mark a message complete before the relevant durable state has been stored.

---

## 14. Submission attempts and state

Model fiscal record state separately from AEAT communication attempts when needed.

Conceptually:

```text
BillingRecord
  -> Submission
      -> Attempt 1
      -> Attempt 2
      -> Attempt 3
```

A transient communication failure must not create a new fiscal record.

Distinguish at least the concepts required by the official specification and the application:

- pending submission
- submitting
- accepted/correct
- accepted with issues if the specification provides such a state
- rejected
- transient communication failure
- retry pending
- permanent failure when appropriate

Use official AEAT terminology where practical.

Avoid arbitrary status assignment. Prefer explicit transition methods and prevent invalid transitions.

Test state transitions.

---

## 15. Resilience

External AEAT communication must define deliberate resilience behavior.

Use:

- timeout
- bounded retry
- appropriate backoff
- transient/permanent error classification
- circuit breaker where useful

Retry only transient failures.

Do not retry permanent AEAT validation/business rejections as though they were network failures.

Avoid nested retry layers that multiply attempts unexpectedly.

Every retry path must preserve idempotency.

Persist enough attempt information for diagnosis without leaking sensitive data.

---

## 16. Validation

Separate validation responsibilities:

### API/transport validation
- malformed request
- missing API fields
- invalid JSON/route/query values

### Application validation
- use-case preconditions
- required command/query data

### Domain validation
- intrinsic invariants
- valid state transitions

### AEAT contract validation
- XSD/contract-specific constraints
- wire-format requirements
- AEAT-only enumerations/lengths where they are not domain concepts

Use FluentValidation if it is already present or intentionally chosen by the developer.

Do not duplicate the same rule across layers without a reason.

---

## 17. Result Pattern and error handling

Expected outcomes must not use exceptions as routine control flow.

Use an existing `Result`/error model if the solution has one. Do not create competing Result implementations.

Distinguish:

- validation error
- domain/business error
- idempotency conflict/replay
- AEAT validation rejection
- AEAT business rejection
- transient AEAT communication failure
- certificate/authentication failure
- persistence/concurrency failure
- unexpected system failure

Do not catch `Exception` and convert it into success.

Do not swallow exceptions silently.

Do not return raw Infrastructure or SOAP exceptions from Application/API.

Unexpected exceptions should be handled centrally at the API boundary and logged with correlation information.

---

## 18. Security and certificates

Never commit:

- production `.pfx` / `.p12` certificates
- private keys
- certificate passwords
- client secrets
- access tokens
- production connection strings
- credentials

Use configuration plus a secure secret provider appropriate to the deployment environment.

Never log:

- private keys
- certificate passwords
- authorization headers
- bearer tokens
- secrets

Avoid logging complete fiscal XML/payloads by default.

If payload auditing is legally/operationally required, implement it deliberately with access control, retention rules, masking where applicable, and separation from ordinary application logs.

---

## 19. Logging and observability

Use structured logging.

Use correlation identifiers and stable business identifiers where safe, for example:

- `CorrelationId`
- `BillingRecordId`
- `SubmissionId`
- `SubmissionAttemptId`

Do not concatenate structured log values into unsearchable strings.

Log meaningful transitions and external-call outcomes without secrets.

Example:

```csharp
logger.LogInformation(
    "Submitting billing record {BillingRecordId} attempt {AttemptNumber}",
    billingRecordId,
    attemptNumber);
```

Do not log huge SOAP/XML payloads at Information level.

---

## 20. Multi-company / multi-taxpayer readiness

Do not hardcode an assumption that only one taxpayer/company will ever exist.

Taxpayer-specific configuration, certificates, chain state, identifiers, and submission data must be scoped according to official VERI*FACTU rules.

Do not use static mutable global state for taxpayer or chain data.

Never mix chain state between taxpayers/issuers when the official rules require separate chains.

---

## 21. Date/time

Do not call `DateTime.Now` throughout application/domain code when the current time affects behavior.

Prefer `TimeProvider` where available, or the existing project time abstraction.

Be explicit about:

- `DateOnly`
- local time
- UTC
- timezone/offset
- AEAT-required timestamp formats

Do not perform implicit timezone conversions in fiscal code.

Formatting used in hash/XML/QR must exactly match official documentation.

---

## 22. Dependency Injection

Each implementation is registered from the layer that owns it.

Prefer composition methods such as:

- `AddApplication(...)`
- `AddInfrastructure(...)`

Keep `Program.cs` focused on application composition and middleware order.

Avoid Service Locator.

Do not manually resolve arbitrary dependencies from `IServiceProvider` unless required by a framework integration.

---

## 23. Package policy

Before adding a NuGet package:

1. inspect existing dependencies
2. prefer .NET/framework capabilities when sufficient
3. justify the dependency
4. avoid packages that duplicate existing infrastructure
5. avoid introducing a package merely to save a few lines of code

Do not replace established project libraries without an explicit reason.

---

## 24. Code quality

Before writing code:

1. inspect nearby code and naming conventions
2. search for existing equivalent abstractions
3. inspect official `/VERIFACTU` documentation when the task is fiscal
4. choose the correct architectural layer
5. identify tests that must be added/updated
6. implement the smallest coherent change

Apply SOLID pragmatically.

Prefer explicit, auditable fiscal code over clever metaprogramming.

Avoid generic names such as `Helper`, `Utils`, `Manager`, `Data`, or `Info` when a precise business/technical name exists.

Avoid generic enums such as `EstadosEnum`; prefer a precise concept such as `EstadoRegistroFacturacion` or `EstadoEnvioVeriFactu`.

Use nullable reference types consistently with the project.

Do not suppress compiler warnings merely to make a build green without understanding the cause.

---

## 25. Async

Use async all the way for I/O.

Propagate `CancellationToken`.

Do not use:

- `.Result`
- `.Wait()`
- `.GetAwaiter().GetResult()`
- `async void` except legitimate event handlers

Do not create unobserved background tasks for fiscal work.

---

## 26. Testing

Critical fiscal behavior requires automated tests.

At minimum cover, when applicable:

- Value Object validation
- domain invariants
- hash generation
- official AEAT hash examples
- record chaining
- concurrent chain creation
- idempotency
- state transitions
- registration records
- cancellation records
- API contract validation
- AEAT request mapping
- AEAT response mapping
- XML serialization
- XSD validation
- required fields
- namespace correctness
- decimal/date formats
- transient communication failure
- permanent AEAT rejection
- certificate/authentication failure mapping
- Outbox processing
- duplicate Outbox delivery
- retry behavior
- database transactions/concurrency

Tests for fiscal algorithms must be deterministic.

Do not use EF Core InMemory as evidence that relational constraints, locks, transactions, indexes, or concurrency behavior is correct. Use an integration-test database/provider for those behaviors.

Use architecture tests to protect project dependency rules if an appropriate architecture-test mechanism exists or is intentionally added.

---

## 27. Backward compatibility

`gesFactu` is consumed by external applications.

Avoid breaking the public REST API unnecessarily.

Do not let AEAT contract changes leak automatically into the public API.

One purpose of the Anti-Corruption Layer is to isolate API consumers from AEAT implementation changes.

When a breaking API change is required, use deliberate versioning/migration.

---

## 28. Never do these things

Copilot must not:

- bypass Clean Architecture boundaries for convenience
- put EF Core in Domain
- reference Infrastructure from Domain or Application
- expose AEAT generated classes from Infrastructure
- expose Domain/EF entities as public API contracts
- calculate hashes in controllers/handlers
- duplicate the hash algorithm
- invent fiscal field values or enum meanings
- guess XML namespaces
- silently omit mandatory AEAT fields
- concatenate fiscal XML manually
- rewrite historical fiscal records to "fix" them
- implement arbitrary CRUD update/delete for immutable fiscal records
- treat communication retries as new billing records
- use fire-and-forget AEAT submission
- use only an in-memory queue for durable fiscal work
- ignore concurrency in record chaining
- use static mutable chain state
- retry permanent validation/business errors blindly
- log secrets/certificate passwords
- commit certificates/private keys
- hide errors by returning success
- add duplicate abstractions when equivalent ones already exist
- change `/VERIFACTU` reference documents without explicit instruction

---

## 29. Decision priority

When making implementation decisions, use this order:

1. Official AEAT / VERI*FACTU requirements
2. Fiscal correctness and integrity
3. Security
4. Data integrity, idempotency, and concurrency safety
5. Clean Architecture boundaries
6. Existing gesFactu conventions
7. Maintainability and testability
8. Performance
9. Convenience / reduction of code

Never trade fiscal correctness or data integrity merely to reduce code.

---

## 30. Definition of done for fiscal changes

A VERI*FACTU-related change is not complete until:

- the relevant `/VERIFACTU` source was checked
- the implementation is in the correct layer
- external AEAT types remain isolated
- validation/error behavior is explicit
- idempotency/concurrency implications were considered
- secrets are not exposed
- tests cover the critical rule
- the solution builds
- relevant tests pass
- any ambiguity or unresolved official-document conflict is reported
