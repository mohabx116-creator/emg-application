#!/usr/bin/env bash
set -Eeuo pipefail

# Owner-controlled application-only rollout. PostgreSQL, uploads, Data Protection
# keys and Caddy are never recreated or restarted by this script.

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
readonly COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.prod.yml}"
readonly ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
readonly COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:?COMPOSE_PROJECT_NAME must identify the existing production stack}"
readonly STATE_DIR="${STATE_DIR:-/opt/emg/deploy-state}"
readonly STATE_FILE="$STATE_DIR/release-state.env"
readonly LOCK_FILE="${LOCK_FILE:-/run/lock/emg-deploy.lock}"
readonly BACKUP_MARKER="${BACKUP_MARKER:?BACKUP_MARKER must point to a verified COMPLETE marker}"
readonly BACKUP_MANIFEST="${BACKUP_MANIFEST:?BACKUP_MANIFEST must point to the matching SHA256SUMS file}"
readonly BACKUP_REQUIRED_FILES="${BACKUP_REQUIRED_FILES:?BACKUP_REQUIRED_FILES must list required encrypted recovery artifacts, comma-separated}"
readonly EXPECTED_SOURCE_SHA="${EXPECTED_SOURCE_SHA:?EXPECTED_SOURCE_SHA is required}"
BACKEND_IMAGE="${BACKEND_IMAGE:?BACKEND_IMAGE must be an immutable digest reference}"
FRONTEND_IMAGE="${FRONTEND_IMAGE:?FRONTEND_IMAGE must be an immutable digest reference}"
readonly EXPECTED_POSTGRES_VOLUME="${EXPECTED_POSTGRES_VOLUME:?EXPECTED_POSTGRES_VOLUME is required}"
readonly EXPECTED_UPLOADS_VOLUME="${EXPECTED_UPLOADS_VOLUME:?EXPECTED_UPLOADS_VOLUME is required}"
readonly EXPECTED_KEYS_VOLUME="${EXPECTED_KEYS_VOLUME:?EXPECTED_KEYS_VOLUME is required}"
readonly PUBLIC_HEALTH_URL="${PUBLIC_HEALTH_URL:?PUBLIC_HEALTH_URL is required}"
readonly PUBLIC_API_HEALTH_URL="${PUBLIC_API_HEALTH_URL:?PUBLIC_API_HEALTH_URL is required}"
readonly PUBLIC_ACCOUNT_URL="${PUBLIC_ACCOUNT_URL:?PUBLIC_ACCOUNT_URL is required}"
readonly AUTH_ACCEPTANCE_COOKIE_FILE="${AUTH_ACCEPTANCE_COOKIE_FILE:?AUTH_ACCEPTANCE_COOKIE_FILE is required}"
readonly AUTH_ACCEPTANCE_URL="${AUTH_ACCEPTANCE_URL:?AUTH_ACCEPTANCE_URL is required}"
readonly AUTH_ACCEPTANCE_SCRIPT="${AUTH_ACCEPTANCE_SCRIPT:-$SCRIPT_DIR/authenticated-acceptance.sh}"
readonly HEALTH_TIMEOUT_SECONDS="${HEALTH_TIMEOUT_SECONDS:-120}"
readonly BACKUP_MAX_AGE_SECONDS="${BACKUP_MAX_AGE_SECONDS:-86400}"
readonly MIN_AVAILABLE_MEMORY_MB="${MIN_AVAILABLE_MEMORY_MB:-512}"

die() { echo "DEPLOYMENT BLOCKED: $*" >&2; exit 1; }
read_state_value() {
  local key="$1"
  [[ -f "$STATE_FILE" ]] || return 0
  awk -F= -v key="$key" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$STATE_FILE"
}
atomic_state() {
  local pending="$1" current_backend="$2" current_frontend="$3" current_source="$4"
  local previous_backend="$5" previous_frontend="$6" previous_source="$7"
  local candidate_backend="$8" candidate_frontend="$9" candidate_source="${10}" result="${11}"
  local temp="$STATE_FILE.tmp.$$"
  umask 077
  {
    printf 'state_version=1\n'
    printf 'pending=%s\n' "$pending"
    printf 'current_backend=%s\ncurrent_frontend=%s\ncurrent_source=%s\n' "$current_backend" "$current_frontend" "$current_source"
    printf 'previous_backend=%s\nprevious_frontend=%s\nprevious_source=%s\n' "$previous_backend" "$previous_frontend" "$previous_source"
    printf 'candidate_backend=%s\ncandidate_frontend=%s\ncandidate_source=%s\n' "$candidate_backend" "$candidate_frontend" "$candidate_source"
    printf 'result=%s\ndeployed_at=%s\n' "$result" "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
  } > "$temp"
  chmod 600 "$temp"
  mv -f -- "$temp" "$STATE_FILE"
}
service_container() {
  local service="$1" id project name
  id="$(docker compose -p "$COMPOSE_PROJECT_NAME" --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps -q "$service" | tr -d '\r')"
  [[ -n "$id" && "$id" != *$'\n'* ]] || die "Expected exactly one running $service service in project $COMPOSE_PROJECT_NAME."
  project="$(docker inspect "$id" --format '{{index .Config.Labels "com.docker.compose.project"}}')"
  name="$(docker inspect "$id" --format '{{index .Config.Labels "com.docker.compose.service"}}')"
  [[ "$project" == "$COMPOSE_PROJECT_NAME" && "$name" == "$service" ]] || die "Container identity mismatch for $service."
  printf '%s' "$id"
}
mount_volume() {
  local id="$1" destination="$2"
  docker inspect "$id" --format "{{range .Mounts}}{{if eq .Destination \"$destination\"}}{{.Name}}{{end}}{{end}}"
}
wait_http() {
  local url="$1" deadline=$((SECONDS + HEALTH_TIMEOUT_SECONDS))
  until curl --connect-timeout 5 --max-time 15 --fail --silent --show-error "$url" >/dev/null; do
    (( SECONDS < deadline )) || return 1
    sleep 3
  done
}
expect_http_status() {
  local url="$1" expected="$2" actual
  actual="$(curl --connect-timeout 5 --max-time 15 --silent --output /dev/null --write-out '%{http_code}' "$url")" || return 1
  [[ "$actual" == "$expected" ]]
}
env_value() {
  local service="$1" key="$2"
  "${compose[@]}" config --format json | python3 -c 'import json,sys; d=json.load(sys.stdin); e=d["services"][sys.argv[1]].get("environment", {}); v=e.get(sys.argv[2], ""); print(v if v is not None else "")' "$service" "$key"
}
validate_npgsql_connection() {
  # Npgsql keys are case-insensitive and values may be quoted or contain ';'.
  # Send all values over stdin so the password never appears in argv.
  printf '%s\0%s\0%s\0%s' "$1" "$2" "$3" "$4" | python3 -c '
import sys

raw, expected_db, expected_user, expected_password = sys.stdin.buffer.read().split(b"\0")
s = raw.decode()
items = {}
i = 0
while i < len(s):
    while i < len(s) and s[i] in "; \t\r\n": i += 1
    if i == len(s): break
    start = i
    while i < len(s) and s[i] not in "=;": i += 1
    if i >= len(s) or s[i] != "=": raise SystemExit(1)
    key = s[start:i].strip().lower()
    if not key or key in items: raise SystemExit(1)
    i += 1
    while i < len(s) and s[i] in " \t": i += 1
    if i < len(s) and s[i] in chr(34) + chr(39):
        quote = s[i]; i += 1; value = []
        while i < len(s):
            if s[i] == quote:
                if i + 1 < len(s) and s[i + 1] == quote: value.append(quote); i += 2; continue
                i += 1; break
            value.append(s[i]); i += 1
        else: raise SystemExit(1)
        if i < len(s) and s[i] not in "; \t\r\n": raise SystemExit(1)
        value = "".join(value)
    else:
        start = i
        while i < len(s) and s[i] != ";": i += 1
        value = s[start:i].strip()
    items[key] = value

def get(*names):
    values = [items.get(name) for name in names if name in items]
    if len(values) != 1: raise SystemExit(1)
    return values[0]

if get("host", "server", "data source") != "postgres": raise SystemExit(1)
if get("port") != "5432": raise SystemExit(1)
if get("database", "initial catalog") != expected_db.decode(): raise SystemExit(1)
if get("username", "user id", "user") != expected_user.decode(): raise SystemExit(1)
if get("password", "pwd") != expected_password.decode(): raise SystemExit(1)
' \
    && return 0
  return 1
}
check_fresh_authenticated_acceptance() {
  [[ -x "$AUTH_ACCEPTANCE_SCRIPT" ]] || die 'Fresh authentication acceptance script is missing or not executable.'
  "$AUTH_ACCEPTANCE_SCRIPT" || die 'Fresh authenticated acceptance failed before application mutation.'
}
verify_authenticated_acceptance() {
  [[ "$(stat -c '%a' "$AUTH_ACCEPTANCE_COOKIE_FILE" 2>/dev/null || true)" == 600 ]] || return 1
  local status
  status="$(curl --connect-timeout 5 --max-time 15 --silent --output /dev/null --write-out '%{http_code}' --cookie "$AUTH_ACCEPTANCE_COOKIE_FILE" "$AUTH_ACCEPTANCE_URL")" || return 1
  [[ "$status" == 200 ]]
}

exec 9>"$LOCK_FILE"
flock -n 9 || die 'Another EMG deployment is already running.'
[[ "$COMPOSE_PROJECT_NAME" =~ ^[a-z0-9][a-z0-9_-]+$ ]] || die 'Invalid Compose project identity.'
[[ -f "$COMPOSE_FILE" && -f "$ENV_FILE" ]] || die 'Compose or environment file is missing.'
[[ -s "$BACKUP_MARKER" && -s "$BACKUP_MANIFEST" ]] || die 'Verified backup evidence is incomplete.'
[[ "$(dirname -- "$BACKUP_MARKER")" == "$(dirname -- "$BACKUP_MANIFEST")" ]] || die 'Backup marker and manifest are from different sets.'
[[ "$EXPECTED_SOURCE_SHA" =~ ^[0-9a-fA-F]{40}$ ]] || die 'Invalid source commit SHA.'
[[ "$BACKEND_IMAGE" =~ ^[^[:space:]]+@sha256:[0-9a-fA-F]{64}$ ]] || die 'Backend is not immutable.'
[[ "$FRONTEND_IMAGE" =~ ^[^[:space:]]+@sha256:[0-9a-fA-F]{64}$ ]] || die 'Frontend is not immutable.'

grep -Eiq '^[[:space:]]*DATABASE_APPLY_MIGRATIONS=(true|1|yes)[[:space:]]*$' "$ENV_FILE" && die 'Migrations are enabled.' || true
grep -Eiq '^[[:space:]]*SECURITY_BOOTSTRAP_ADMIN_ENABLED=(true|1|yes)[[:space:]]*$' "$ENV_FILE" && die 'Admin bootstrap is enabled.' || true
backup_mtime="$(stat -c '%Y' "$BACKUP_MARKER")"
backup_age=$(( $(date +%s) - backup_mtime ))
(( backup_age >= 0 && backup_age <= BACKUP_MAX_AGE_SECONDS )) || die 'Backup is too old.'
( cd -- "$(dirname -- "$BACKUP_MANIFEST")" && sha256sum -c "$(basename -- "$BACKUP_MANIFEST")" >/dev/null ) || die 'Backup checksum validation failed.'
IFS=',' read -r -a required_backup_files <<< "$BACKUP_REQUIRED_FILES"
for required_file in "${required_backup_files[@]}"; do
  [[ -s "$required_file" ]] || die "Required backup artifact is missing."
done

mkdir -p "$STATE_DIR"
chmod 700 "$STATE_DIR"
free_kb="$(df -Pk "$STATE_DIR" | awk 'NR==2 {print $4}')"
if ! [[ "$free_kb" =~ ^[0-9]+$ ]] || ! (( free_kb >= 1048576 )); then die 'Less than 1 GiB free.'; fi
available_memory_mb="$(awk '/MemAvailable:/ {printf "%d", $2 / 1024}' /proc/meminfo 2>/dev/null || printf '0')"
if ! [[ "$available_memory_mb" =~ ^[0-9]+$ ]] || ! (( available_memory_mb >= MIN_AVAILABLE_MEMORY_MB )); then die 'Insufficient available memory.'; fi

compose=(docker compose -p "$COMPOSE_PROJECT_NAME" --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
"${compose[@]}" config >/dev/null || die 'Effective Compose configuration is invalid.'
pending="$(read_state_value pending)"
[[ "$pending" != true ]] || die 'A previous deployment is pending recovery.'

api_id="$(service_container api)"
frontend_id="$(service_container frontend)"
postgres_id="$(service_container postgres)"
[[ "$(mount_volume "$postgres_id" /var/lib/postgresql/data)" == "$EXPECTED_POSTGRES_VOLUME" ]] || die 'PostgreSQL volume mismatch.'
[[ "$(mount_volume "$api_id" /app/uploads)" == "$EXPECTED_UPLOADS_VOLUME" ]] || die 'Uploads volume mismatch.'
[[ "$(mount_volume "$api_id" /app/data-protection-keys)" == "$EXPECTED_KEYS_VOLUME" ]] || die 'Data Protection volume mismatch.'

db_user="$(env_value postgres POSTGRES_USER)"
db_name="$(env_value postgres POSTGRES_DB)"
db_password="$(env_value postgres POSTGRES_PASSWORD)"
[[ -n "$db_user" && -n "$db_name" && -n "$db_password" ]] || die 'Database credential configuration is incomplete.'
api_connection="$(env_value api ConnectionStrings__EmgDb)"
validate_npgsql_connection "$api_connection" "$db_name" "$db_user" "$db_password" || die 'Effective API database configuration does not match parsed Npgsql credentials.'
printf '%s\n' "postgres:5432:$db_name:$db_user:$db_password" |
  docker exec -i "$postgres_id" sh -ceu 'umask 077; cat > /tmp/emg-pgpass; trap "rm -f /tmp/emg-pgpass" EXIT; PGPASSFILE=/tmp/emg-pgpass psql -h postgres -U "$1" -d "$2" -Atqc "select 1" >/dev/null' sh "$db_user" "$db_name" || die 'Password-authenticated PostgreSQL readiness check failed.'
check_fresh_authenticated_acceptance

current_backend="$(docker inspect "$api_id" --format '{{.Config.Image}}')"
current_frontend="$(docker inspect "$frontend_id" --format '{{.Config.Image}}')"
current_source="$(docker inspect "$api_id" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}')"

docker pull "$BACKEND_IMAGE" >/dev/null
docker pull "$FRONTEND_IMAGE" >/dev/null
for image in "$BACKEND_IMAGE" "$FRONTEND_IMAGE"; do
  revision="$(docker image inspect "$image" --format '{{index .Config.Labels "org.opencontainers.image.revision"}}')"
  os="$(docker image inspect "$image" --format '{{.Os}}')"
  arch="$(docker image inspect "$image" --format '{{.Architecture}}')"
  [[ "$revision" == "$EXPECTED_SOURCE_SHA" ]] || die 'Candidate image source revision mismatch.'
  [[ "$os" == linux && "$arch" == amd64 ]] || die 'Candidate image architecture mismatch.'
done

atomic_state true "$current_backend" "$current_frontend" "$current_source" "$current_backend" "$current_frontend" "$current_source" "$BACKEND_IMAGE" "$FRONTEND_IMAGE" "$EXPECTED_SOURCE_SHA" pending
deployment_mutated=false
deployment_succeeded=false
rollback_in_progress=false
rollback_application() {
  rollback_in_progress=true
  export BACKEND_IMAGE="$current_backend" FRONTEND_IMAGE="$current_frontend"
  "${compose[@]}" up -d --no-build --no-deps api frontend || return 1
  wait_http http://127.0.0.1:5121/api/health || return 1
  wait_http http://127.0.0.1:3100/ || return 1
  wait_http "$PUBLIC_API_HEALTH_URL" || return 1
  wait_http "$PUBLIC_HEALTH_URL" || return 1
  expect_http_status "$PUBLIC_ACCOUNT_URL" 401 || return 1
  check_fresh_authenticated_acceptance || return 1
  verify_authenticated_acceptance || return 1
  return 0
}
on_exit() {
  local status=$?
  trap - EXIT INT TERM
  if [[ "$deployment_mutated" == true && "$deployment_succeeded" != true && "$rollback_in_progress" != true ]]; then
    echo 'Deployment failed after mutation; attempting application-only rollback.' >&2
    if rollback_application; then
      if atomic_state false "$current_backend" "$current_frontend" "$current_source" "$current_backend" "$current_frontend" "$current_source" "$BACKEND_IMAGE" "$FRONTEND_IMAGE" "$EXPECTED_SOURCE_SHA" rollback-success; then
        echo 'ROLLBACK: SUCCESS' >&2
        exit 10
      fi
      echo 'ROLLBACK: FAILED — state persistence failed.' >&2
      exit 90
    fi
    echo 'ROLLBACK: FAILED — application health did not recover.' >&2
    exit 90
  fi
  exit "$status"
}
trap on_exit EXIT
trap 'exit 130' INT TERM

export BACKEND_IMAGE FRONTEND_IMAGE DATABASE_APPLY_MIGRATIONS=false SECURITY_BOOTSTRAP_ADMIN_ENABLED=false
deployment_mutated=true
"${compose[@]}" up -d --pull never --no-build --no-deps api frontend
wait_http http://127.0.0.1:5121/api/health || die 'Backend health timeout.'
wait_http http://127.0.0.1:3100/ || die 'Frontend health timeout.'
wait_http "$PUBLIC_API_HEALTH_URL" || die 'Public API routing health check failed.'
wait_http "$PUBLIC_HEALTH_URL" || die 'Public frontend routing health check failed.'
expect_http_status "$PUBLIC_ACCOUNT_URL" 401 || die 'Authenticated database readiness check failed.'
verify_authenticated_acceptance || die 'Authenticated functional acceptance failed.'

api_id="$(service_container api)"
frontend_id="$(service_container frontend)"
[[ "$(docker inspect "$api_id" --format '{{.Config.Image}}')" == "$BACKEND_IMAGE" ]] || die 'Running Backend image mismatch.'
[[ "$(docker inspect "$frontend_id" --format '{{.Config.Image}}')" == "$FRONTEND_IMAGE" ]] || die 'Running Frontend image mismatch.'

atomic_state false "$BACKEND_IMAGE" "$FRONTEND_IMAGE" "$EXPECTED_SOURCE_SHA" "$current_backend" "$current_frontend" "$current_source" "$BACKEND_IMAGE" "$FRONTEND_IMAGE" "$EXPECTED_SOURCE_SHA" success || die 'Release state persistence failed.'
deployment_succeeded=true
echo 'DEPLOYMENT: SUCCESS'
