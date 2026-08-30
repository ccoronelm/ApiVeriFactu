# Operación de gesFactu

## Health

- `GET /health/live`: proceso ASP.NET vivo.
- `GET /health/ready`: PostgreSQL + configuración/certificados VERI*FACTU listos.

Readiness no sustituye los E2E reales, pero evita aceptar tráfico cuando la infraestructura básica no está preparada.

## Correlación

La API acepta `X-Correlation-ID`. Si no llega, genera uno. La misma cabecera se devuelve en la respuesta y se incorpora al contexto de logging.

## Dead Letter Queue

Listar pendientes:

```http
GET /api/v1/operations/dead-letters?take=50
X-GesFactu-Admin-Key: <secret>
```

El listado no incluye el payload fiscal.

Reencolar:

```http
POST /api/v1/operations/dead-letters/{dlq-id}/requeue
X-GesFactu-Admin-Key: <secret>
Content-Type: application/json

{
  "notes": "Incidencia AEAT resuelta; reintento autorizado"
}
```

El reencolado reutiliza el mensaje Outbox original, limpia locks/retries previos y marca la DLQ como revisada. El worker normal vuelve a procesarlo.

## Secretos

Suministrar secretos mediante User Secrets, variables de entorno o secret manager.

Ejemplos:

```text
Operations__AdminApiKey
VeriFactu__Certificate__Thumbprint
ConnectionStrings__DefaultConnection
VeriFactu__Taxpayers__0__Certificate__Thumbprint
```

No almacenar PFX/P12, claves privadas ni contraseñas en Git.

## OpenAPI

En Development se habilita Swagger. Fuera de Development sólo se habilita con `OpenApi__Enabled=true`.

En Producción se recomienda mantenerlo deshabilitado salvo ventana operativa controlada.

## CORS

Configurar únicamente los frontends permitidos, por ejemplo:

```text
Cors__AllowedOrigins__0=https://facturacion.example.com
```

Si no se configura ningún origen, no se concede acceso CORS a otros orígenes.

## Incidencia de resultado incierto

Si la red cae después de que AEAT haya recibido la petición, el siguiente intento puede obtener error 3000. gesFactu conserva SubmissionAttempt y reconcilia el duplicado cuando AEAT confirma que el registro ya existe con estado correcto.

## Seguridad servidor-a-servidor

Todos los endpoints salvo health requieren `X-GesFactu-Api-Key`. Los endpoints de operaciones requieren además `X-GesFactu-Admin-Key`.

En Linux se recomienda usar `Security__ApiKeyFile` y `Operations__AdminApiKeyFile` apuntando a ficheros bajo `/run/secrets`.

## Idempotencia HTTP

Las operaciones mutantes bajo `/api` requieren `Idempotency-Key` en Producción.

- misma key + mismo método/ruta/payload: replay de la primera respuesta;
- misma key + payload diferente: `409 Conflict`;
- key `Pending`: `409` fail-closed;
- los completados caducan automáticamente; los `Pending` no se eliminan automáticamente.

Python debe conservar la misma `Idempotency-Key` durante todos los retries de una misma operación.

## Actor y auditoría

`X-GesFactu-Actor` identifica al usuario/servicio originador. `AuditLogs` es append-only y puede consultarse con `GET /api/v1/operations/audit` usando las dos claves.

## Métricas

`GET /api/v1/operations/metrics` devuelve Outbox pendiente, antigüedad del pendiente más antiguo, DLQ sin revisar e intentos AEAT recientes.

## Inmutabilidad fiscal

`BillingRecord` y `BillingTaxDetail` persistidos no pueden borrarse ni modificar sus campos fiscales. Las correcciones se hacen por subsanación, rectificación o `RegistroAnulacion`.

Las FK de desgloses e intentos AEAT usan `RESTRICT`, evitando que un borrado arrastre evidencias.

## Docker Linux y secretos

El PFX se monta read-only y su contraseña se suministra mediante `VeriFactu__Certificate__PfxPasswordFile`. `CertificateLoader` usa `EphemeralKeySet`.

## Reverse proxy y límites

Producción exige `AllowedHosts` explícito, usa HSTS, sólo confía en las IP de `ReverseProxy:TrustedProxies`, aplica rate limiting y limita el body a 1 MiB por defecto.
