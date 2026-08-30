#!/usr/bin/env bash
set -euo pipefail

: "${PGHOST:?PGHOST is required}"
: "${PGPORT:=5432}"
: "${PGDATABASE:?PGDATABASE is required}"
: "${PGUSER:?PGUSER is required}"
: "${BACKUP_DIR:=./backups}"

mkdir -p "$BACKUP_DIR"

timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
output="$BACKUP_DIR/${PGDATABASE}_${timestamp}.dump"

pg_dump \
  --format=custom \
  --no-owner \
  --no-acl \
  --file "$output" \
  "$PGDATABASE"

pg_restore --list "$output" >/dev/null

sha256sum "$output" > "$output.sha256"

echo "Backup creado y verificado: $output"
