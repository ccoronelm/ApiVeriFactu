# Backup y restauración PostgreSQL

La base de `ApiVeriFactu` debe usar una base de datos y un usuario propios aunque comparta la misma instancia PostgreSQL con la aplicación React/Python.

Ejemplo:

- instancia: PostgreSQL compartida;
- base: `gesfactu_verifactu`;
- usuario: `gesfactu_verifactu`;
- el usuario Python no recibe permisos sobre esta base.

## Backup

Los scripts usan las variables estándar de PostgreSQL (`PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER`, `PGPASSWORD`).

```bash
PGHOST=postgres \
PGDATABASE=gesfactu_verifactu \
PGUSER=gesfactu_verifactu \
PGPASSWORD="..." \
BACKUP_DIR=/backups \
./scripts/backup-postgres.sh
```

El backup es `custom format`, se valida con `pg_restore --list` y se genera un SHA-256 acompañante.

## Restore

La restauración está bloqueada salvo confirmación explícita:

```bash
GESFACTU_CONFIRM_RESTORE=RESTORE \
PGHOST=postgres \
PGDATABASE=gesfactu_verifactu_restore_test \
PGUSER=gesfactu_verifactu \
PGPASSWORD="..." \
./scripts/restore-postgres.sh /backups/gesfactu_verifactu_....dump
```

## Gate de producción

Antes de activar AEAT Producción debe existir al menos una restauración ensayada en una base temporal y deben comprobarse:

- `BillingRecords` y cadena de huellas;
- `BillingTaxDetails`;
- `SubmissionAttempts`;
- `OutboxMessages`;
- `DeadLetterMessages`;
- `AuditLogs`;
- `IdempotencyRecords`.

No se deben borrar filas fiscales para "limpiar" una restauración. Los registros fiscales son append-only y las correcciones se realizan mediante los mecanismos VERI*FACTU correspondientes.
