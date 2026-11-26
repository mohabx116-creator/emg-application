#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
readonly COMPOSE_FILE="${COMPOSE_FILE:-$SCRIPT_DIR/docker-compose.prod.yml}"
readonly ENV_FILE="${ENV_FILE:-$SCRIPT_DIR/.env}"
readonly COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME:?COMPOSE_PROJECT_NAME must identify the existing production stack}"
readonly STATE_DIR="${STATE_DIR:-/opt/emg/deploy-state}"
readonly STATE_FILE="$STATE_DIR/release-state.env"
readonly LOCK_FILE="${LOCK_FILE:-/run/lock/emg-deploy.lock}"
readonly PUBLIC_HEALTH_URL="${PUBLIC_HEALTH_URL:?PUBLIC_HEALTH_URL is required}"
readonly PUBLIC_API_HEALTH_URL="${PUBLIC_API_HEALTH_URL:?PUBLIC_API_HEALTH_URL is required}"
readonly PUBLIC_ACCOUNT_URL="${PUBLIC_ACCOUNT_URL:?PUBLIC_ACCOUNT_URL is required}"
readonly AUTH_ACCEPTANCE_COOKIE_FILE="${AUTH_ACCEPTANCE_COOKIE_FILE:?AUTH_ACCEPTANCE_COOKIE_FILE is required}"
readonly AUTH_ACCEPTANCE_URL="${AUTH_ACCEPTANCE_URL:?AUTH_ACCEPTANCE_URL is required}"
readonly AUTH_ACCEPTANCE_SCRIPT="${AUTH_ACCEPTANCE_SCRIPT:-$SCRIPT_DIR/authenticated-acceptance.sh}"
readonly HEALTH_TIMEOUT_SECONDS="${HEALTH_TIMEOUT_SECONDS:-120}"

die() { echo "ROLLBACK BLOCKED: $*" >&2; exit 1; }
read_state_value() {
  local key="$1"
  awk -F= -v key="$key" '$1 == key { sub(/^[^=]*=/, ""); print; exit }' "$STATE_FILE"
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
verify_authenticated_acceptance() {
  [[ "$(stat -c '%a' "$AUTH_ACCEPTANCE_COOKIE_FILE" 2>/dev/null || true)" == 600 ]] || return 1
  local status
  status="$(curl --connect-timeout 5 --max-time 15 --silent --output /dev/null --write-out '%{http_code}' --cookie "$AUTH_ACCEPTANCE_COOKIE_FILE" "$AUTH_ACCEPTANCE_URL")" || return 1
  [[ "$status" == 200 ]]
}
check_fresh_authenticated_acceptance() {
  [[ -x "$AUTH_ACCEPTANCE_SCRIPT" ]] || die 'Fresh authentication acceptance script is missing or not executable.'
  "$AUTH_ACCEPTANCE_SCRIPT" || die 'Fresh authenticated acceptance failed before rollback mutation.'
}
service_container() {
  local service="$1" id project name
  id="$(docker compose -p "$COMPOSE_PROJECT_NAME" --env-file "$ENV_FILE" -f "$COMPOSE_FILE" ps -q "$service" | tr -d '\r')"
  [[ -n "$id" && "$id" != *$'\n'* ]] || die "Expected exactly one $service service."
  project="$(docker inspect "$id" --format '{{index .Config.Labels "com.docker.compose.project"}}')"
  name="$(docker inspect "$id" --format '{{index .Config.Labels "com.docker.compose.service"}}')"
  [[ "$project" == "$COMPOSE_PROJECT_NAME" && "$name" == "$service" ]] || die "Container identity mismatch for $service."
  printf '%s' "$id"
}

exec 9>"$LOCK_FILE"
flock -n 9 || die 'Another deployment operation is already running.'
[[ -f "$COMPOSE_FILE" && -f "$ENV_FILE" && -s "$STATE_FILE" ]] || die 'Required deployment files are missing.'
[[ "$(stat -c '%a' "$STATE_FILE")" == 600 ]] || die 'Release state permissions must be 0600.'
pending="$(read_state_value pending)"
[[ "$pending" != true ]] || die 'Cannot rollback while a deployment is pending recovery.'
current_backend="$(read_state_value current_backend)"
current_frontend="$(read_state_value current_frontend)"
previous_backend="$(read_state_value previous_backend)"
previous_frontend="$(read_state_value previous_frontend)"
current_source="$(read_state_value current_source)"
previous_source="$(read_state_value previous_source)"
[[ "$current_backend" =~ ^[^[:space:]]+(@sha256:[0-9a-fA-F]{64})?$ ]] || die 'Invalid current Backend state.'
[[ "$current_frontend" =~ ^[^[:space:]]+(@sha256:[0-9a-fA-F]{64})?$ ]] || die 'Invalid current Frontend state.'
[[ "$previous_backend" =~ ^[^[:space:]]+(@sha256:[0-9a-fA-F]{64})?$ ]] || die 'Invalid previous Backend state.'
[[ "$previous_frontend" =~ ^[^[:space:]]+(@sha256:[0-9a-fA-F]{64})?$ ]] || die 'Invalid previous Frontend state.'

compose=(docker compose -p "$COMPOSE_PROJECT_NAME" --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
"${compose[@]}" config >/dev/null || die 'Effective Compose configuration is invalid.'
service_container api >/dev/null
service_container frontend >/dev/null
check_fresh_authenticated_acceptance

export BACKEND_IMAGE="$previous_backend" FRONTEND_IMAGE="$previous_frontend"
export DATABASE_APPLY_MIGRATIONS=false SECURITY_BOOTSTRAP_ADMIN_ENABLED=false
docker image inspect "$previous_backend" >/dev/null 2>&1 || die 'Previous Backend image is not available locally.'
docker image inspect "$previous_frontend" >/dev/null 2>&1 || die 'Previous Frontend image is not available locally.'
"${compose[@]}" up -d --pull never --no-build --no-deps api frontend >/dev/null
wait_http http://127.0.0.1:5121/api/health || die 'Backend rollback health failed.'
wait_http http://127.0.0.1:3100/ || die 'Frontend rollback health failed.'
wait_http "$PUBLIC_API_HEALTH_URL" || die 'Public API rollback health failed.'
wait_http "$PUBLIC_HEALTH_URL" || die 'Public frontend rollback health failed.'
expect_http_status "$PUBLIC_ACCOUNT_URL" 401 || die 'Public account rollback readiness failed.'
check_fresh_authenticated_acceptance
verify_authenticated_acceptance || die 'Authenticated rollback acceptance failed.'

api_id="$(service_container api)"
frontend_id="$(service_container frontend)"
[[ "$(docker inspect "$api_id" --format '{{.Config.Image}}')" == "$previous_backend" ]] || die 'Backend rollback image mismatch.'
[[ "$(docker inspect "$frontend_id" --format '{{.Config.Image}}')" == "$previous_frontend" ]] || die 'Frontend rollback image mismatch.'

temp="$STATE_FILE.tmp.$$"
umask 077
{
  printf 'state_version=1\npending=false\n'
  printf 'current_backend=%s\ncurrent_frontend=%s\ncurrent_source=%s\n' "$previous_backend" "$previous_frontend" "$previous_source"
  printf 'previous_backend=%s\nprevious_frontend=%s\nprevious_source=%s\n' "$current_backend" "$current_frontend" "$current_source"
  printf 'candidate_backend=%s\ncandidate_frontend=%s\ncandidate_source=%s\n' "$previous_backend" "$previous_frontend" "$previous_source"
  printf 'result=rollback-success\ndeployed_at=%s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
} > "$temp"
chmod 600 "$temp"
mv -f -- "$temp" "$STATE_FILE"
echo 'ROLLBACK: SUCCESS'
