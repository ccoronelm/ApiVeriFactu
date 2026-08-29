# Plan Maestro de Desarrollo - gesFactu

## ?? Resumen de Fases

**Estado Actual:** Fase 7 en progreso (Anti-Corruption Layer AEAT)

---

## ? Fases Completadas

### **Fase 0: Inicialización (? Completada)**
- [x] Repositorio Git inicializado en `backend`
- [x] Remoto GitHub privado configurado
- [x] Instrucciones Copilot creadas y documentadas en `.github/copilot-instructions.md`

### **Fase 1: Base Clean Architecture (? Completada)**
- [x] Proyectos creados: Domain, Application, Infrastructure, Api, Tests
- [x] Eliminadas dependencias incorrectas de Domain
- [x] Configurado resultado Pattern con discriminated records
- [x] Middleware global de excepciones
- [x] Logging estructurado con Serilog

### **Fase 2: Primer Caso de Uso - Crear Registro (? Completada)**
- [x] Entidad `BillingRecord` agregado raíz
- [x] Value Objects: `TaxpayerNif`, `InvoiceSeries`, `InvoiceNumber`, `InvoiceIdentifier`, `Money`
- [x] Comando `CreateBillingRecordCommand` + Handler + Validator
- [x] Endpoint API `POST /api/v1/BillingRecords`

### **Fase 3: Hash/Huella VERI*FACTU (? Completada)**
- [x] `IHashCalculator` (puerto)
- [x] `Sha256HashCalculator` (implementación)
- [x] `BillingRecordHashInput` (modelo de entrada determinista)
- [x] 10 tests de hash pasando
- [x] Documentación en `docs/HASH_CALCULATION.md`

### **Fase 4: Repository Pattern + Query (? Completada)**
- [x] `IBillingRecordRepository` definido
- [x] Implementación EF Core `BillingRecordRepository`
- [x] Query `GetBillingRecordQuery` + Handler
- [x] Endpoint API `GET /api/v1/BillingRecords/{id}`

### **Fase 5: Value Objects en EF Core + Migraciones (? Completada)**
- [x] Refactorizado `BillingRecord` con propiedades desnormalizadas para EF Core
- [x] `BillingRecordConfiguration` con mapeo simplificado
- [x] `ApplicationDbContextFactory` para diseño de tiempo EF Core
- [x] Migración inicial `InitialCreate` generada y aplicada
- [x] Base de datos SQL Server local funcional
- [x] Commit `6199a6d` subido

### **Fase 6: Tests de Persistencia (? Completada)**
- [x] `BillingRecordRepositoryTests` creado con 19 tests
- [x] Tests de `AddAsync`, `GetByIdAsync`, `GetPreviousRecordAsync`
- [x] Tests de `UpdateSubmissionStatusAsync`, `UpdateAeatStatusAsync`
- [x] Tests de preservación de `Money` y `InvoiceIdentifier`
- [x] Tests de paginación en `ListByIssuerAsync`
- [x] 19/20 tests pasando (1 omitido por TODO en filtrado)
- [x] Commit `24022dd` subido

---

## ?? Fases En Progreso / Pendientes

### **Fase 6: Tests de Persistencia (? Completada)**

**Objetivo:** Validar que el repositorio, las queries y la persistencia funcionan correctamente

**Tareas:**
- [x] Crear `BillingRecordRepositoryTests` (xUnit + EF Core InMemory)
- [x] Tests de `AddAsync`, `GetByIdAsync`, `GetPreviousRecordAsync`
- [x] Tests de `UpdateSubmissionStatusAsync`, `UpdateAeatStatusAsync`
- [x] Tests de actualización de hash
- [x] Fixture de base de datos de prueba (SQL Server local opcional)
- [x] Tests de integración de queries en API
- [x] 19/20 tests pasando

---

### **Fase 7: Anti-Corruption Layer AEAT (? ACTUAL)**

**Objetivo:** Definir los puertos e interfaces para la comunicación con AEAT

**Tareas:**
- [ ] `IVeriFactuGateway` (puerto principal)
- [ ] `VeriFactuRequest` / `VeriFactuResponse` (DTOs de aplicación)
- [ ] WSDL/XSD scaffolding (esperar documentos locales o usar proxy)
- [ ] Mapeador de dominio ? SOAP request
- [ ] Mapeador de SOAP response ? resultado aplicación
- [ ] Manejo de errores AEAT específicos
- [ ] Tests unitarios de mapeos

**Prioridad:** ALTA - Necesario para caso de uso "Enviar a AEAT"

---

### **Fase 8: Transactional Outbox (Bloque siguiente)**

**Objetivo:** Garantizar entrega confiable de mensajes a AEAT bajo fallos

**Tareas:**
- [ ] Entidad `OutboxMessage`
- [ ] Configuración EF Core para Outbox
- [ ] Migración Outbox
- [ ] `IOutboxStore` puerto
- [ ] `OutboxMessageProcessor` (worker background)
- [ ] Procesador idempotente (by `CorrelationId`)
- [ ] Tests de múltiples intentos y duplicados
- [ ] Integración con `IVeriFactuGateway`

**Prioridad:** ALTA - Base para resiliencia

---

### **Fase 9: Envío a AEAT (Comando)**

**Objetivo:** Implementar `EnviarRegistroAEATCommand`

**Tareas:**
- [ ] Comando `EnviarRegistroAEATCommand`
- [ ] Validator (registro debe estar pendiente, hash debe existir)
- [ ] Handler (crear Outbox, llamar gateway, actualizar estado)
- [ ] Endpoint `POST /api/v1/BillingRecords/{id}/submit`
- [ ] Manejo de respuestas AEAT (aceptado, rechazado, error)
- [ ] Tests de happy path y error cases

**Prioridad:** ALTA

---

### **Fase 10: Anulación de Registros (Comando)**

**Objetivo:** Implementar `CancelarRegistroCommand` conforme a VERI*FACTU

**Tareas:**
- [ ] Revisar `/VERIFACTU` para estructura de cancellations
- [ ] Entidad `CancellationRecord` (agregado aparte o parte de BillingRecord)
- [ ] Comando `CancelarRegistroCommand`
- [ ] Validaciones de reglas de anulación
- [ ] Manejo de hash para anulaciones
- [ ] Endpoint `POST /api/v1/BillingRecords/{id}/cancel`
- [ ] Tests

**Prioridad:** MEDIA - Después de envío funcional

---

### **Fase 11: Resiliencia y Retry**

**Objetivo:** Políticas de reintento y circuit breaker

**Tareas:**
- [ ] Polly policies (retry exponencial, circuit breaker)
- [ ] Transient vs permanent error classification en AEAT responses
- [ ] Max retry limits y backoff
- [ ] Telemetría de reintentos
- [ ] Tests de resiliencia

**Prioridad:** MEDIA - Después de Outbox funcional

---

### **Fase 12: Registro de Envíos (Entidad)**

**Objetivo:** Rastrear todos los intentos de envío

**Tareas:**
- [ ] Entidad `SubmissionAttempt`
- [ ] Relación con `BillingRecord`
- [ ] Campos: timestamp, request payload (sin secrets), response, error details
- [ ] Configuración EF Core
- [ ] Migración
- [ ] Queries por registro

**Prioridad:** MEDIA - Observabilidad

---

### **Fase 13: QR / Código QR**

**Objetivo:** Generación de QR conforme a VERI*FACTU

**Tareas:**
- [ ] Revisar `/VERIFACTU` QR requirements
- [ ] `IQRGenerator` (puerto)
- [ ] Implementación (e.g., `QRNet`, `ZXing`)
- [ ] Contenido del QR (NIF, serie, número, hash, timestamp)
- [ ] Integración en handler de creación
- [ ] Tests

**Prioridad:** MEDIA - Requerimiento VERI*FACTU

---

### **Fase 14: Consultas Avanzadas**

**Objetivo:** Queries para búsqueda y reportes

**Tareas:**
- [ ] `GetBillingRecordsPagedQuery` con filtros
- [ ] Filtrar por NIF, serie, status, fecha
- [ ] Endpoints `/api/v1/BillingRecords?nif=...&status=...`
- [ ] Paginación
- [ ] Sorting

**Prioridad:** MEDIA

---

### **Fase 15: Documentación y Ejemplos**

**Objetivo:** README, Postman, ejemplos de uso

**Tareas:**
- [ ] README.md completo
- [ ] Colección Postman
- [ ] Ejemplos de integración cliente
- [ ] Swagger/OpenAPI mejorado
- [ ] Diagrama de arquitectura

**Prioridad:** BAJA - Final

---

## ?? Roadmap Resumido por Bloque

| Bloque | Fases | Estado | ETA |
|--------|-------|--------|-----|
| **MVP Básico** | 0-5 | ? Completo | - |
| **Persistencia y Queries** | 6 | ?? En progreso | +1h |
| **AEAT Integration** | 7-9 | ? Próximo | +3-4h |
| **Resiliencia** | 8, 11 | ? Siguiente | +2h |
| **Funcionalidades Avanzadas** | 10, 12-14 | ? Después | +3-4h |
| **Documentación** | 15 | ? Final | +1h |

---

## ?? Notas Generales

- Todas las fases respetan Clean Architecture
- Hash y encadenamiento implementados deterministicamente
- Value Objects protegen invariantes de dominio
- AEAT separado mediante Anti-Corruption Layer
- Outbox garantiza entrega sin duplicados
- Pruebas enfocadas en casos críticos fiscales

---

## ?? Referencias

- `/VERIFACTU`: Documentación oficial AEAT (requiere revisión para cada fase)
- `.github/copilot-instructions.md`: Reglas de arquitectura y estilo
- `docs/HASH_CALCULATION.md`: Referencia del hash determinista
- Commits recientes: Inspeccionar para cambios en patrones
