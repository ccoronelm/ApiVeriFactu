# Plan de desarrollo completo - gesFactu

## Fases implementadas ?

### Fase 1: Infraestructura base
- ? Clean Architecture correctamente estructurada
- ? CQRS + MediatR
- ? Result Pattern
- ? DependencyInjection en cada capa
- ? Logging estructurado (Serilog)
- ? Middleware global de errores

### Fase 2: Primer caso de uso - Crear Registro
- ? Value Objects (TaxpayerNif, InvoiceSeries, InvoiceNumber, InvoiceIdentifier, Money, TaxRate)
- ? Agregado BillingRecord
- ? CreateBillingRecordCommand + Handler + Validator
- ? BillingRecordsController (POST)
- ? ApplicationDbContext

### Fase 3: Hash/Huella
- ? IHashCalculator puerto
- ? Sha256HashCalculator implementación
- ? 10 tests unitarios pasados
- ? Integración en handler

## Fases pendientes ??

### Fase 4: Persistencia y Queries
- [ ] IBillingRecordRepository
- [ ] BillingRecordRepository (EF Core)
- [ ] GetBillingRecordQuery + Handler
- [ ] GetBillingRecordByIdQuery + Handler
- [ ] EF Core Configuration/Mapping para Value Objects
- [ ] EF Core Migrations

### Fase 5: Anti-Corruption Layer AEAT
- [ ] IVeriFactuGatewayAdapter
- [ ] VeriFactuGatewayAdapter (implementación básica)
- [ ] Mappers: AEAT types ? internal models
- [ ] XSD/WSDL client generation (Refit/HttpClient)
- [ ] Error mapping (AEAT ? internal)

### Fase 6: Envío a AEAT
- [ ] SubmitBillingRecordCommand
- [ ] SubmitBillingRecordCommandHandler
- [ ] Certificado digital (carga y validación)
- [ ] Firma XML
- [ ] Generación de XML para AEAT
- [ ] Integración con gateway

### Fase 7: Transactional Outbox
- [ ] OutboxMessage entity
- [ ] OutboxMessageConfiguration
- [ ] OutboxProcessor background service
- [ ] OutboxMessageRepository
- [ ] Idempotencia en processor

### Fase 8: Encadenamiento y Concurrencia
- [ ] Lógica de obtención de "registro anterior"
- [ ] Lock pessimista en BD (UPDLOCK hint)
- [ ] Tests de concurrencia
- [ ] Validación de cadena

### Fase 9: Resiliencia y Retry
- [ ] Polly HttpClientFactory con retry
- [ ] Circuit breaker
- [ ] Backoff exponencial
- [ ] Transient error classification

### Fase 10: Anulación de registros
- [ ] CancelBillingRecordCommand
- [ ] RegistroAnulacion entity
- [ ] Validaciones de anulación

### Fase 11: Tests
- [ ] Tests unitarios completos
- [ ] Tests de integración
- [ ] Tests de concurrencia
- [ ] Tests de AEAT gateway

### Fase 12: Documentación y pulido
- [ ] README.md principal
- [ ] API documentation
- [ ] Architecture decisions
- [ ] Deployment guide

---

## Orden de ejecución (PRIORIZADO)

1. **Fase 4** - Persistencia (repo + queries) ? Sin esto no se puede probar nada
2. **Fase 5** - Anti-Corruption Layer básico ? Necesario para envío
3. **Fase 6** - Envío a AEAT ? Caso de uso crítico
4. **Fase 7** - Outbox ? Resiliencia y idempotencia
5. **Fase 8** - Encadenamiento ? Lógica fiscal crítica
6. **Fase 9** - Resiliencia ? Ya en Outbox/Envío
7. **Fase 10** - Anulación ? Segundo caso de uso
8. **Fase 11** - Tests exhaustivos ? Al final
9. **Fase 12** - Documentación ? Al final

---

## Commits esperados

- [ ] Commit: Repository pattern + EF Core mappings
- [ ] Commit: Queries (GetBillingRecord, ListBillingRecords)
- [ ] Commit: Anti-Corruption Layer AEAT
- [ ] Commit: Submit to AEAT + Certificate handling
- [ ] Commit: Outbox pattern implementation
- [ ] Commit: Chaining logic with concurrency
- [ ] Commit: Resilience policies (Polly)
- [ ] Commit: Cancellation records
- [ ] Commit: Unit + integration tests
- [ ] Commit: Documentation + README

**Total esperado: ~10-12 commits**

---

## Estimación de tiempo

- Fase 4: 30 min
- Fase 5: 45 min
- Fase 6: 60 min
- Fase 7: 45 min
- Fase 8: 45 min
- Fase 9: 30 min
- Fase 10: 30 min
- Fase 11: 90 min
- Fase 12: 30 min

**Total: ~5-6 horas de desarrollo**
