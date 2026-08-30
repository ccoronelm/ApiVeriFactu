# Checklist AEAT TEST → Producción

Este documento es un gate de release. No activar Producción si alguno de los puntos obligatorios no está verificado.

## 1. Código y CI

- `develop` contiene todos los PR del roadmap VERI*FACTU.
- CI verde: build Release, migraciones EF, Infrastructure.Tests y compilación E2E.
- No hay `.pfx`, `.p12`, `.pem` ni `.key` versionados.
- No hay `PfxPassword` ni `Operations:AdminApiKey` no vacíos en archivos versionados.
- `master` no se modifica directamente.

## 2. E2E real AEAT TEST

Ejecutar en Windows con el certificado real instalado en `CurrentUser/My`:

```powershell
$env:GESFACTU_RUN_AEAT_E2E="true"
dotnet test .\src\AeatE2ETests\gesFactu.AeatE2ETests.csproj `
  --filter "Category=AEAT-E2E" `
  --logger "console;verbosity=normal"
Remove-Item Env:\GESFACTU_RUN_AEAT_E2E
```

Gate obligatorio: `Failed = 0` y `Skipped = 0`.

La matriz incluye F1, F2, R1-R5, subsanación, anulación, desgloses especiales, duplicados y consultas.
La paginación se valida contra XSD/parser porque forzar una página de más de 10.000 registros en TEST no es apropiado.

## 3. Certificados

Por cada obligado tributario:
- certificado válido y no caducado;
- clave privada disponible;
- thumbprint correcto;
- instalado en `CurrentUser/My` si se usa Windows Store;
- titular con permisos AEAT suficientes;
- ningún PFX/P12 ni contraseña en Git.

El arranque SOAP y `/health/ready` cargan realmente los certificados.

## 4. Configuración

En el entorno de aplicación Production:
- `VeriFactu:Environment=Production`
- `VeriFactu:AllowProduction=true`
- `VeriFactu:ClientMode=SoapClient`
- obligados tributarios correctos;
- `SistemaInformatico` definitivo;
- si hay varios obligados: `TipoUsoPosibleMultiOT=S` e `IndicadorMultiplesOT=S`;
- `Operations:AdminApiKey` suministrada como secreto y con al menos 32 caracteres;
- `Cors:AllowedOrigins` contiene únicamente orígenes autorizados;
- `OpenApi:Enabled=false` salvo necesidad operativa explícita.

No activar `AllowProduction=true` antes de completar este checklist.

## 5. PostgreSQL y migraciones

- backup probado antes de desplegar;
- `dotnet ef database update` validado sobre copia/restauración;
- índices y migraciones aplicados;
- espacio en disco y retención monitorizados;
- restauración PostgreSQL ensayada.

## 6. Outbox / DLQ

- `/health/ready` responde `Healthy`;
- worker Outbox activo;
- política de retries conocida;
- circuito de DLQ probado;
- recuperación administrativa probada mediante `POST /api/v1/operations/dead-letters/{id}/requeue`;
- la clave administrativa no aparece en logs.

## 7. Observabilidad

- logs estructurados centralizados;
- `X-Correlation-ID` preservado en respuestas;
- alertas para readiness no saludable, crecimiento de DLQ, fallos AEAT repetidos, certificados próximos a caducar y PostgreSQL no disponible;
- nivel de log Production = Information o superior.

## 8. Smoke test tras despliegue

1. comprobar `/health/live`;
2. comprobar `/health/ready`;
3. comprobar conexión PostgreSQL;
4. comprobar `ClientMode=SoapClient`;
5. comprobar que el endpoint resuelto es Producción;
6. comprobar configuración del obligado correcto;
7. realizar la primera remisión productiva de forma supervisada.

## 9. Reglas fiscales no negociables

- VERI*FACTU usa autenticación mTLS.
- No añadir firma XML/XAdES a los registros VERI*FACTU.
- Mantener huella SHA-256 y encadenamiento oficial.
- Mantener cadenas independientes por obligado tributario.
- No reutilizar certificados de otra empresa por error de routing.

## 10. Rollback

Si el despliegue falla antes de remitir registros: volver a la versión anterior, conservar PostgreSQL y Outbox y no borrar mensajes pendientes.

Si el fallo ocurre después de una llamada AEAT incierta: no crear manualmente un segundo registro; revisar SubmissionAttempt, Outbox y AEAT; permitir la reconciliación de duplicados 3000; usar DLQ/requeue sólo después de entender el estado externo.

## 11. Gate adicional de seguridad e integridad

- `Security:ApiKey` o `Security:ApiKeyFile` >= 32 caracteres;
- `Operations:AdminApiKey` o `AdminApiKeyFile` >= 32 caracteres y diferente;
- `AllowedHosts` explícito, nunca `*`;
- API y PostgreSQL no expuestos directamente a Internet;
- `Idempotency-Key` probado en retries;
- `AuditLogs` append-only;
- borrado/modificación fiscal bloqueados;
- FK fiscales en `RESTRICT`;
- `Api.Tests` verdes;
- imagen Docker Linux construida por CI;
- backup restaurado en una base temporal;
- `GET /api/v1/operations/metrics` revisado antes del go-live.
