#!/usr/bin/env bash
set -Eeuo pipefail

: "${POSTGRES_CONTAINER:?POSTGRES_CONTAINER is required}"
: "${POSTGRES_USER:?POSTGRES_USER is required}"
: "${POSTGRES_DB:?POSTGRES_DB is required}"

container_ip="$(docker inspect "$POSTGRES_CONTAINER" --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')"
[[ "$container_ip" =~ ^[0-9.]+$ ]] || { echo 'Unable to resolve the private PostgreSQL container IP.' >&2; exit 2; }
export POSTGRES_HOST="$container_ip"

pgpass_file="$(mktemp)"
chmod 600 "$pgpass_file"
trap 'rm -f "$pgpass_file"' EXIT
docker inspect "$POSTGRES_CONTAINER" --format '{{range .Config.Env}}{{println .}}{{end}}' \
  | awk -F= -v user="$POSTGRES_USER" '$1 == "POSTGRES_PASSWORD" { print "*:*:*:" user ":" substr($0, index($0, "=") + 1) }' \
  > "$pgpass_file"
[[ -s "$pgpass_file" ]] || { echo 'Unable to obtain the PostgreSQL password from the isolated container configuration.' >&2; exit 2; }
export PGPASSFILE="$pgpass_file"
exec /opt/emg/backup/backup-application.sh
