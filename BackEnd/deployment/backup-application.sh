#!/usr/bin/env bash
set -Eeuo pipefail

: "${POSTGRES_HOST:?POSTGRES_HOST is required}"
: "${POSTGRES_PORT:=5432}"
: "${POSTGRES_DB:?POSTGRES_DB is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${PGPASSFILE:?PGPASSFILE is required}"
: "${BACKUP_AGE_RECIPIENT:?BACKUP_AGE_RECIPIENT is required}"
: "${BACKUP_ROOT:?BACKUP_ROOT is required}"
: "${UPLOADS_ROOT:?UPLOADS_ROOT is required}"
: "${DATA_PROTECTION_ROOT:?DATA_PROTECTION_ROOT is required}"
: "${RECOVERY_CONFIG_ROOT:?RECOVERY_CONFIG_ROOT is required}"
: "${BACKUP_CATEGORY:?BACKUP_CATEGORY is required}"

case "$BACKUP_CATEGORY" in
  weekly|monthly|manual) ;;
  *) echo 'BACKUP_CATEGORY must be weekly, monthly or manual.' >&2; exit 2 ;;
esac

umask 077
export PGPASSFILE
mkdir -p "$BACKUP_ROOT/$BACKUP_CATEGORY"
exec 9>"$BACKUP_ROOT/.backup.lock"
flock -n 9 || { echo 'Another EMG backup is already running.' >&2; exit 3; }

timestamp="$(TZ=Africa/Cairo date +%Y%m%dT%H%M%S%z)"
partial="$(mktemp -d "$BACKUP_ROOT/.partial-${timestamp}.XXXXXX")"
final="$BACKUP_ROOT/$BACKUP_CATEGORY/emg-${timestamp}"
cleanup() { rm -rf "$partial"; }
trap cleanup EXIT

mkdir -p "$partial/payload" "$partial/metadata"
chmod 700 "$partial" "$partial/payload" "$partial/metadata"

snapshot_files() {
  local root="$1" output="$2"
  find "$root" -type f -printf '%P\t%s\t%T@\n' | LC_ALL=C sort > "$output"
}

snapshot_files "$UPLOADS_ROOT" "$partial/metadata/uploads.before"
snapshot_files "$DATA_PROTECTION_ROOT" "$partial/metadata/dataprotection.before"

pg_dump --format=custom --no-owner --no-acl \
  --host="$POSTGRES_HOST" --port="$POSTGRES_PORT" \
  --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" \
  --file="$partial/payload/emg.postgres.dump"

tar --create --gzip --file="$partial/payload/emg.uploads.tar.gz" -C "$UPLOADS_ROOT" .
tar --create --gzip --file="$partial/payload/emg.dataprotection.tar.gz" -C "$DATA_PROTECTION_ROOT" .
tar --create --gzip --file="$partial/payload/emg.recovery-config.tar.gz" -C "$RECOVERY_CONFIG_ROOT" .

snapshot_files "$UPLOADS_ROOT" "$partial/metadata/uploads.after"
snapshot_files "$DATA_PROTECTION_ROOT" "$partial/metadata/dataprotection.after"
cmp -s "$partial/metadata/uploads.before" "$partial/metadata/uploads.after" || {
  echo 'Uploads changed during backup; refusing an inconsistent recovery point.' >&2; exit 4;
}
cmp -s "$partial/metadata/dataprotection.before" "$partial/metadata/dataprotection.after" || {
  echo 'Data Protection keys changed during backup; refusing an inconsistent recovery point.' >&2; exit 4;
}

printf 'category=%s\ntimestamp=%s\npostgres_host=%s\npostgres_port=%s\npostgres_db=%s\n' \
  "$BACKUP_CATEGORY" "$timestamp" "$POSTGRES_HOST" "$POSTGRES_PORT" "$POSTGRES_DB" \
  > "$partial/metadata/recovery-point.txt"

for payload in "$partial"/payload/*; do
  name="$(basename "$payload")"
  age --encrypt --recipient="$BACKUP_AGE_RECIPIENT" \
    --output="$partial/${name}.age" "$payload"
done

rm -rf "$partial/payload"
sha256sum "$partial"/*.age > "$partial/SHA256SUMS"
printf 'EMG_BACKUP_COMPLETE\n' > "$partial/COMPLETE"

mv "$partial" "$final"
chmod 700 "$final"
echo "Completed encrypted EMG backup: $final"
