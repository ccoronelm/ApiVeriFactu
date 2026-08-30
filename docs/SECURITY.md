# Modelo de seguridad

Arquitectura prevista:

```text
React -> backend Python -> red privada -> gesFactu.Api -> mTLS -> AEAT
                         -> PostgreSQL compartido
```

React no debe contener la API key de gesFactu.Api.

## Capas

1. red privada/firewall;
2. HTTPS y reverse proxy;
3. `X-GesFactu-Api-Key`;
4. `X-GesFactu-Admin-Key` adicional para operaciones;
5. `Idempotency-Key` para mutaciones;
6. CORS como defensa del navegador, no como autenticación;
7. mTLS independiente hacia AEAT.

## Integridad fiscal

- BillingRecord y BillingTaxDetail son inmutables tras persistencia;
- no se permiten DELETE fiscales;
- AuditLog es append-only;
- SubmissionAttempts no se eliminan por cascade;
- correcciones sólo mediante registros VERI*FACTU oficiales.

## Secretos

En Docker Linux se montan API key, admin key, PFX y password como secret files. El PFX no se copia a la imagen.

## Responsabilidad del backend Python

La autenticación de usuarios finales y permisos por empresa/obligado pertenecen al backend Python. Python debe enviar un `X-GesFactu-Actor` trazable y mantener la `Idempotency-Key` entre retries.
