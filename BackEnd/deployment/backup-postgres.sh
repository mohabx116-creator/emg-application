#!/usr/bin/env bash
set -Eeuo pipefail

: "${POSTGRES_HOST:?POSTGRES_HOST is required}"
: "${POSTGRES_PORT:=5432}"
: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${BACKUP_AGE_RECIPIENT:?BACKUP_AGE_RECIPIENT is required}"
: "${BACKUP_DESTINATION:?BACKUP_DESTINATION is required}"
: "${BACKUP_RETENTION_DAYS:=30}"

umask 077
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
working="$(mktemp -d)"
trap 'rm -rf "$working"' EXIT
dump="$working/emg-${timestamp}.dump"
encrypted="$working/emg-${timestamp}.dump.age"

pg_dump --format=custom --no-owner --no-acl \
  --host="$POSTGRES_HOST" --port="$POSTGRES_PORT" \
  --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" --file="$dump"
age --encrypt --recipient="$BACKUP_AGE_RECIPIENT" --output="$encrypted" "$dump"
sha256sum "$encrypted" > "$encrypted.sha256"
mkdir -p "$BACKUP_DESTINATION"
install -m 600 "$encrypted" "$encrypted.sha256" "$BACKUP_DESTINATION/"
find "$BACKUP_DESTINATION" -type f -name 'emg-*.dump.age' -mtime "+$BACKUP_RETENTION_DAYS" -delete
find "$BACKUP_DESTINATION" -type f -name 'emg-*.dump.age.sha256' -mtime "+$BACKUP_RETENTION_DAYS" -delete
