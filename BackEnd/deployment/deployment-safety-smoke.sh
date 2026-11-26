#!/usr/bin/env bash
set -Eeuo pipefail

# Purely local control-flow test. It mocks Docker and HTTP; it never connects to
# a registry, database, Caddy or production host.

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
readonly DEPLOY_SCRIPT="$SCRIPT_DIR/deploy-approved-images.sh"
TEST_ROOT="$(mktemp -d)"
readonly TEST_ROOT
readonly MOCK_BIN="$TEST_ROOT/bin"
readonly ARTIFACTS="$TEST_ROOT/artifacts"
readonly STATE_DIR="$TEST_ROOT/state"
readonly RUNTIME_DIR="$TEST_ROOT/runtime"
EXPECTED_SOURCE_SHA="d960e5510835f13b1aedf2622990539d6043c02c"
readonly EXPECTED_SOURCE_SHA
readonly OLD_BACKEND="emg-dep03b-api:previous"
readonly OLD_FRONTEND="emg-dep04-frontend:previous"
readonly CANDIDATE_BACKEND="ghcr.io/mohabx116-creator/emg-application/backend@sha256:87ea9a65cdd6be02db5863c540c7d6ad9f91d71c28fbac65cbfd39b31e822e0e"
readonly CANDIDATE_FRONTEND="ghcr.io/mohabx116-creator/emg-application/frontend@sha256:0d77ae306d690f0ec42c919b03d39fac327184884ad4a49ef89429c8ec3bbafa"

cleanup() { rm -rf -- "$TEST_ROOT"; }
trap cleanup EXIT
mkdir -p "$MOCK_BIN" "$ARTIFACTS" "$STATE_DIR" "$RUNTIME_DIR"

for required in database.enc uploads.enc keys.enc config.enc; do printf 'fixture\n' >"$ARTIFACTS/$required"; done
printf 'COMPLETE\n' >"$ARTIFACTS/COMPLETE"
( cd "$ARTIFACTS" && sha256sum database.enc uploads.enc keys.enc config.enc > SHA256SUMS )
touch "$TEST_ROOT/docker-compose.prod.yml" "$TEST_ROOT/.env"

cat >"$MOCK_BIN/docker" <<'MOCK_DOCKER'
#!/usr/bin/env bash
set -Eeuo pipefail
runtime="${FAKE_RUNTIME_DIR:?}"
if [[ "${1:-}" == compose ]]; then
  shift
  args=" $* "
  if [[ "$args" == *' config '* ]]; then exit 0; fi
  if [[ "$args" == *' ps -q api '* ]]; then echo api-id; exit 0; fi
  if [[ "$args" == *' ps -q frontend '* ]]; then echo frontend-id; exit 0; fi
  if [[ "$args" == *' ps -q postgres '* ]]; then echo postgres-id; exit 0; fi
  if [[ "$args" == *' pull '* ]]; then exit 0; fi
  if [[ "$args" == *' up '* ]]; then
    [[ "${FAKE_UP_FAIL:-0}" != 1 ]] || exit 1
    printf '%s' "${BACKEND_IMAGE:?}" >"$runtime/backend"
    printf '%s' "${FRONTEND_IMAGE:?}" >"$runtime/frontend"
    exit 0
  fi
  exit 0
fi
if [[ "${1:-}" == pull ]]; then exit 0; fi
if [[ "${1:-}" == exec ]]; then
  [[ "${FAKE_DB_AUTH_FAIL:-0}" != 1 ]] || exit 1
  printf '1\n'
  exit 0
fi
if [[ "${1:-}" == image && "${2:-}" == inspect ]]; then
  ref="$3"; fmt="$5"
  if [[ "$fmt" == *'.Config.Labels'* ]]; then printf '%s\n' "$EXPECTED_SOURCE_SHA"; elif [[ "$fmt" == *'.Os'* ]]; then printf 'linux\n'; elif [[ "$fmt" == *'.Architecture'* ]]; then printf 'amd64\n'; fi
  exit 0
fi
if [[ "${1:-}" == inspect ]]; then
  id="$2"; fmt="$4"
  if [[ "$fmt" == *'com.docker.compose.project'* ]]; then printf '%s\n' "$COMPOSE_PROJECT_NAME";
  elif [[ "$fmt" == *'com.docker.compose.service'* ]]; then [[ "$id" == api-id ]] && printf 'api\n' || ([[ "$id" == frontend-id ]] && printf 'frontend\n' || printf 'postgres\n');
  elif [[ "$fmt" == *'.Config.Image'* ]]; then [[ "$id" == api-id ]] && cat "$runtime/backend" || cat "$runtime/frontend";
  elif [[ "$fmt" == *'.Config.Labels'* ]]; then printf '%s\n' "$EXPECTED_SOURCE_SHA";
  elif [[ "$fmt" == *'.Os'* ]]; then printf 'linux\n';
  elif [[ "$fmt" == *'.Architecture'* ]]; then printf 'amd64\n';
  elif [[ "$fmt" == *'.Destination'* ]]; then
    if [[ "$fmt" == *'/var/lib/postgresql/data'* ]]; then printf 'emg_postgres_data\n';
    elif [[ "$fmt" == *'/app/uploads'* ]]; then printf 'emg_uploads\n';
    else printf 'data_protection_keys\n'; fi
  fi
  exit 0
fi
exit 0
MOCK_DOCKER

cat >"$MOCK_BIN/curl" <<'MOCK_CURL'
#!/usr/bin/env bash
set -Eeuo pipefail
count_file="${FAKE_RUNTIME_DIR:?}/curl-count"
count=0; [[ -f "$count_file" ]] && count="$(cat "$count_file")"
count=$((count + 1)); printf '%s' "$count" >"$count_file"
if [[ "${FAKE_CURL_MODE:-pass}" == always-fail ]]; then exit 22; fi
if [[ "${FAKE_CURL_MODE:-pass}" == fail-candidate && -f "$FAKE_RUNTIME_DIR/backend" && "$(cat "$FAKE_RUNTIME_DIR/backend")" == "$FAKE_CANDIDATE_BACKEND" ]]; then exit 22; fi
if [[ "${FAKE_CURL_MODE:-pass}" == fail-first && "$count" -eq 1 ]]; then exit 22; fi
if [[ " $* " == *'https://emg.example.test/authenticated/me'* ]]; then
  printf '%s' "${FAKE_AUTH_STATUS:-200}"
elif [[ " $* " == *'https://emg.example.test/api/account/me'* ]]; then
  printf '401'
else
  printf '200'
fi
exit 0
MOCK_CURL

chmod 700 "$MOCK_BIN/docker" "$MOCK_BIN/curl"
cat >"$MOCK_BIN/python3" <<'MOCK_PYTHON'
#!/usr/bin/env bash
set -Eeuo pipefail
if [[ "${1:-}" == -c ]]; then
  case "${3:-}:${4:-}" in
    postgres:POSTGRES_USER) printf 'emg\n' ;;
    postgres:POSTGRES_DB) printf 'emg\n' ;;
    postgres:POSTGRES_PASSWORD) printf 'synthetic\n' ;;
    api:ConnectionStrings__EmgDb) printf 'Host=postgres;Port=5432;Database=emg;Username=emg;Password=synthetic\n' ;;
    *) printf '\n' ;;
  esac
fi
MOCK_PYTHON
chmod 700 "$MOCK_BIN/python3"
export PATH="$MOCK_BIN:$PATH"
export FAKE_RUNTIME_DIR="$RUNTIME_DIR" FAKE_CANDIDATE_BACKEND="$CANDIDATE_BACKEND" COMPOSE_PROJECT_NAME=emg-test
export COMPOSE_FILE="$TEST_ROOT/docker-compose.prod.yml" ENV_FILE="$TEST_ROOT/.env"
export STATE_DIR BACKUP_MARKER="$ARTIFACTS/COMPLETE" BACKUP_MANIFEST="$ARTIFACTS/SHA256SUMS"
export BACKUP_REQUIRED_FILES="$ARTIFACTS/database.enc,$ARTIFACTS/uploads.enc,$ARTIFACTS/keys.enc,$ARTIFACTS/config.enc"
export EXPECTED_SOURCE_SHA EXPECTED_POSTGRES_VOLUME=emg_postgres_data EXPECTED_UPLOADS_VOLUME=emg_uploads EXPECTED_KEYS_VOLUME=data_protection_keys
export PUBLIC_HEALTH_URL=https://emg.example.test/ PUBLIC_API_HEALTH_URL=https://emg.example.test/api/health PUBLIC_ACCOUNT_URL=https://emg.example.test/api/account/me
export AUTH_ACCEPTANCE_URL=https://emg.example.test/authenticated/me AUTH_ACCEPTANCE_COOKIE_FILE="$TEST_ROOT/auth.cookies"
export BACKUP_MAX_AGE_SECONDS=86400 HEALTH_TIMEOUT_SECONDS=2
printf 'POSTGRES_USER=emg\nPOSTGRES_DB=emg\nPOSTGRES_PASSWORD=synthetic\n' >"$TEST_ROOT/.env"
printf '# synthetic authenticated cookie\n' >"$TEST_ROOT/auth.cookies"
chmod 600 "$TEST_ROOT/auth.cookies"

export BACKEND_IMAGE="$CANDIDATE_BACKEND" FRONTEND_IMAGE="$CANDIDATE_FRONTEND"
printf '%s' "$OLD_BACKEND" >"$RUNTIME_DIR/backend"
printf '%s' "$OLD_FRONTEND" >"$RUNTIME_DIR/frontend"

"$DEPLOY_SCRIPT" >/dev/null
grep -q '^pending=false$' "$STATE_DIR/release-state.env"
grep -q "^current_backend=$CANDIDATE_BACKEND$" "$STATE_DIR/release-state.env"
echo 'successful deployment: PASS'

"$DEPLOY_SCRIPT" >/dev/null
echo 'repeated deployment: PASS'

printf '%s' "$OLD_BACKEND" >"$RUNTIME_DIR/backend"
printf '%s' "$OLD_FRONTEND" >"$RUNTIME_DIR/frontend"
rm -f "$STATE_DIR/release-state.env"
export FAKE_DB_AUTH_FAIL=1
set +e; "$DEPLOY_SCRIPT" >/dev/null 2>&1; code=$?; set -e
[[ "$code" -eq 1 ]]
[[ "$(cat "$RUNTIME_DIR/backend")" == "$OLD_BACKEND" ]]
echo 'invalid database credential blocks deployment: PASS'
unset FAKE_DB_AUTH_FAIL

printf '%s' "$OLD_BACKEND" >"$RUNTIME_DIR/backend"
printf '%s' "$OLD_FRONTEND" >"$RUNTIME_DIR/frontend"
rm -f "$STATE_DIR/release-state.env"
export FAKE_AUTH_STATUS=401
set +e; "$DEPLOY_SCRIPT" >/dev/null 2>&1; code=$?; set -e
[[ "$code" -eq 90 ]]
grep -q '^pending=true$' "$STATE_DIR/release-state.env"
echo 'unauthenticated acceptance blocks success: PASS'
unset FAKE_AUTH_STATUS
rm -f "$STATE_DIR/release-state.env"

printf '%s' "$OLD_BACKEND" >"$RUNTIME_DIR/backend"
printf '%s' "$OLD_FRONTEND" >"$RUNTIME_DIR/frontend"
export FAKE_UP_FAIL=1
set +e; "$DEPLOY_SCRIPT" >/dev/null 2>&1; code=$?; set -e
[[ "$code" -eq 90 ]]
echo 'compose recreation failure is surfaced: PASS'
unset FAKE_UP_FAIL
rm -f "$STATE_DIR/release-state.env"

export FAKE_CURL_MODE=fail-candidate
rm -f "$RUNTIME_DIR/curl-count"
set +e; "$DEPLOY_SCRIPT" >/dev/null 2>&1; code=$?; set -e
[[ "$code" -eq 10 ]]
grep -q '^result=rollback-success$' "$STATE_DIR/release-state.env"
echo 'failed health with successful rollback: PASS'

export FAKE_CURL_MODE=always-fail
rm -f "$RUNTIME_DIR/curl-count"
set +e; "$DEPLOY_SCRIPT" >/dev/null 2>&1; code=$?; set -e
[[ "$code" -eq 90 ]]
grep -q '^pending=true$' "$STATE_DIR/release-state.env"
echo 'rollback failure is surfaced: PASS'

echo 'deployment safety smoke: PASS'
