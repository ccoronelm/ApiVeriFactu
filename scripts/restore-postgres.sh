#!/usr/bin/env bash
set -euo pipefail

: "${1:?Uso: restore-postgres.sh <backup.dump>}"
: "${PGHOST:?PGHOST is required}"
: "${PGPORT:=5432}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"

backup="$1"

if [[ ! -f "$backup" ]]; then
  echo "No existe el backup: $backup" >&2
  exit 1
fi

if [[ -f "$backup.sha256" ]]; then
  sha256sum --check "$backup.sha256"
fi

pg_restore --list "$backup" >/dev/null

if [[ "${GESFACTU_CONFIRM_RESTORE:-}" != "RESTORE" ]]; then
  echo "BLOQUEADO: defina GESFACTU_CONFIRM_RESTORE=RESTORE para restaurar." >&2
  exit 2
fi

pg_restore \
  --clean \
  --if-exists \
  --no-owner \
  --no-acl \
  --dbname "$PGDATABASE" \
  "$backup"

echo "Restauración completada en $PGDATABASE"
