#!/usr/bin/env bash
set -Eeuo pipefail

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.prod.yml}"
readonly ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
readonly STATE_FILE="${STATE_FILE:-/opt/emg/deploy-state/last-success.env}"
readonly LOCK_FILE="${LOCK_FILE:-/run/lock/emg-deploy.lock}"

exec 9>"$LOCK_FILE"
flock -n 9 || { echo 'Another EMG deployment is already running.' >&2; exit 20; }

[[ -s "$STATE_FILE" ]] || { echo 'Rollback state is missing.' >&2; exit 1; }
# shellcheck disable=SC1090
source "$STATE_FILE"
[[ "${previous_backend:-}" =~ ^[^[:space:]]+(@sha256:[0-9a-fA-F]{64})?$ ]] || exit 1
[[ "${previous_frontend:-}" =~ ^[^[:space:]]+(@sha256:[0-9a-fA-F]{64})?$ ]] || exit 1

compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
"${compose[@]}" config >/dev/null
export BACKEND_IMAGE="$previous_backend"
export FRONTEND_IMAGE="$previous_frontend"
export DATABASE_APPLY_MIGRATIONS=false
export SECURITY_BOOTSTRAP_ADMIN_ENABLED=false
"${compose[@]}" up -d --no-build --no-deps api frontend >/dev/null

for endpoint in http://127.0.0.1:5121/api/health http://127.0.0.1:3100/; do
  curl --fail --silent "$endpoint" >/dev/null || { echo "Rollback health check failed: $endpoint" >&2; exit 1; }
done

echo 'Application-only rollback passed health checks; database, uploads, keys and Caddy were not changed.'
