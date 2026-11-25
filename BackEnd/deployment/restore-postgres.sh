#!/usr/bin/env bash
set -Eeuo pipefail

: "${POSTGRES_HOST:?POSTGRES_HOST is required}"
: "${POSTGRES_PORT:=5432}"
: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${BACKUP_AGE_IDENTITY_FILE:?BACKUP_AGE_IDENTITY_FILE is required}"
: "${BACKUP_FILE:?BACKUP_FILE is required}"
: "${RESTORE_CONFIRM:?RESTORE_CONFIRM must equal RESTORE_EMG}"
[[ "$RESTORE_CONFIRM" == RESTORE_EMG ]]

umask 077
working="$(mktemp -d)"
trap 'rm -rf "$working"' EXIT
sha256sum --check "$BACKUP_FILE.sha256"
age --decrypt --identity="$BACKUP_AGE_IDENTITY_FILE" --output="$working/emg.dump" "$BACKUP_FILE"
pg_restore --clean --if-exists --no-owner --no-acl \
  --host="$POSTGRES_HOST" --port="$POSTGRES_PORT" \
  --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" "$working/emg.dump"
