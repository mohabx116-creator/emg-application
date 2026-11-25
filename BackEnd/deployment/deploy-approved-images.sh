#!/usr/bin/env bash
set -Eeuo pipefail

# Owner-controlled application-only deployment. The database, uploads, keys and Caddy
# services are intentionally not restarted or changed by this script.

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.prod.yml}"
readonly ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
readonly STATE_DIR="${STATE_DIR:-/opt/emg/deploy-state}"
readonly STATE_FILE="$STATE_DIR/last-success.env"
readonly LOCK_FILE="${LOCK_FILE:-/run/lock/emg-deploy.lock}"
readonly BACKUP_MARKER="${BACKUP_MARKER:?BACKUP_MARKER must point to a verified complete backup marker}"
readonly BACKUP_MANIFEST="${BACKUP_MANIFEST:?BACKUP_MANIFEST must point to the backup SHA256 manifest}"
readonly EXPECTED_SOURCE_SHA="${EXPECTED_SOURCE_SHA:?EXPECTED_SOURCE_SHA is required}"
BACKEND_IMAGE="${BACKEND_IMAGE:?BACKEND_IMAGE must be an immutable digest reference}"
FRONTEND_IMAGE="${FRONTEND_IMAGE:?FRONTEND_IMAGE must be an immutable digest reference}"
readonly HEALTH_TIMEOUT_SECONDS="${HEALTH_TIMEOUT_SECONDS:-120}"
readonly BACKUP_MAX_AGE_SECONDS="${BACKUP_MAX_AGE_SECONDS:-86400}"
readonly MIN_AVAILABLE_MEMORY_MB="${MIN_AVAILABLE_MEMORY_MB:-512}"

exec 9>"$LOCK_FILE"
flock -n 9 || { echo 'Another EMG deployment is already running.' >&2; exit 20; }

die() { echo "DEPLOYMENT BLOCKED: $*" >&2; exit 1; }

[[ -f "$COMPOSE_FILE" ]] || die "Compose file not found."
[[ -f "$ENV_FILE" ]] || die "Production env file not found."
[[ -s "$BACKUP_MARKER" ]] || die "Verified backup marker is missing or empty."
[[ -s "$BACKUP_MANIFEST" ]] || die "Backup SHA256 manifest is missing or empty."
[[ "$EXPECTED_SOURCE_SHA" =~ ^[0-9a-fA-F]{40}$ ]] || die "Invalid source commit SHA."
[[ "$BACKEND_IMAGE" =~ ^[^[:space:]]+@sha256:[0-9a-fA-F]{64}$ ]] || die "Backend must use a digest reference."
[[ "$FRONTEND_IMAGE" =~ ^[^[:space:]]+@sha256:[0-9a-fA-F]{64}$ ]] || die "Frontend must use a digest reference."

grep -Eiq '^[[:space:]]*DATABASE_APPLY_MIGRATIONS=(true|1|yes)[[:space:]]*$' "$ENV_FILE" && \
  die 'Automatic migrations are enabled; deployment requires false.' || true
grep -Eiq '^[[:space:]]*SECURITY_BOOTSTRAP_ADMIN_ENABLED=(true|1|yes)[[:space:]]*$' "$ENV_FILE" && \
  die 'Administrator bootstrap is enabled; deployment requires false.' || true

backup_mtime="$(stat -c '%Y' "$BACKUP_MARKER")"
backup_age=$(( $(date +%s) - backup_mtime ))
(( backup_age >= 0 && backup_age <= BACKUP_MAX_AGE_SECONDS )) || die 'Verified backup is too old.'
( cd -- "$(dirname -- "$BACKUP_MANIFEST")" && sha256sum -c "$(basename -- "$BACKUP_MANIFEST")" >/dev/null ) || \
  die 'Backup checksum verification failed.'

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"

free_kb="$(df -Pk "$STATE_DIR" | awk 'NR==2 {print $4}')"
[[ "$free_kb" =~ ^[0-9]+$ ]] && (( free_kb >= 1048576 )) || die 'Less than 1 GiB free in the deployment filesystem.'
available_memory_mb="$(awk '/MemAvailable:/ {printf "%d", $2 / 1024}' /proc/meminfo 2>/dev/null || printf '0')"
[[ "$available_memory_mb" =~ ^[0-9]+$ ]] && (( available_memory_mb >= MIN_AVAILABLE_MEMORY_MB )) || \
  die 'Insufficient available memory for the application rollout.'

compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
"${compose[@]}" config >/dev/null || die 'Effective Compose configuration is invalid.'

previous_backend=''
previous_frontend=''
if [[ -s "$STATE_FILE" ]]; then
  # shellcheck disable=SC1090
  source "$STATE_FILE"
fi

if [[ -z "${previous_backend:-}" || -z "${previous_frontend:-}" ]]; then
  current_images="$(${compose[@]} ps --format '{{.Image}}' api frontend 2>/dev/null || true)"
  previous_backend="$(printf '%s\n' "$current_images" | sed -n '1p')"
  previous_frontend="$(printf '%s\n' "$current_images" | sed -n '2p')"
fi
[[ -n "$previous_backend" && -n "$previous_frontend" ]] || die 'No previous application images recorded for rollback.'

echo 'Pulling and validating candidate images before changing application services.'
docker pull "$BACKEND_IMAGE" >/dev/null
docker pull "$FRONTEND_IMAGE" >/dev/null

for image in "$BACKEND_IMAGE" "$FRONTEND_IMAGE"; do
  revision="$(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}')"
  os="$(docker image inspect "$image" --format '{{.Os}}')"
  arch="$(docker image inspect "$image" --format '{{.Architecture}}')"
  [[ "$revision" == "$EXPECTED_SOURCE_SHA" ]] || die "Image revision mismatch."
  [[ "$os" == linux && "$arch" == amd64 ]] || die "Image architecture mismatch."
done

export BACKEND_IMAGE FRONTEND_IMAGE
export DATABASE_APPLY_MIGRATIONS=false
export SECURITY_BOOTSTRAP_ADMIN_ENABLED=false

rollback() {
  echo 'Health check failed; restoring previous application images only.' >&2
  export BACKEND_IMAGE="$previous_backend"
  export FRONTEND_IMAGE="$previous_frontend"
  "${compose[@]}" up -d --no-build --no-deps api frontend >/dev/null || true
}
trap rollback ERR

"${compose[@]}" up -d --no-build --no-deps api frontend >/dev/null

deadline=$((SECONDS + HEALTH_TIMEOUT_SECONDS))
until curl --fail --silent http://127.0.0.1:5121/api/health >/dev/null && \
      curl --fail --silent http://127.0.0.1:3100/ >/dev/null; do
  (( SECONDS < deadline )) || die 'Application health checks timed out.'
  sleep 3
done

cat >"$STATE_FILE" <<EOF
previous_backend=$previous_backend
previous_frontend=$previous_frontend
source_sha=$EXPECTED_SOURCE_SHA
EOF
chmod 600 "$STATE_FILE"
trap - ERR
echo 'Application-only deployment passed health checks.'
