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
