#!/usr/bin/env bash
set -Eeuo pipefail

# Refreshes an owner-authorized session without exposing credentials in argv.
# The password is read from a mode-0600 file and the login JSON is streamed to
# curl through stdin. The resulting cookie jar is used for a real /me request.

: "${AUTH_ACCEPTANCE_LOGIN_URL:?AUTH_ACCEPTANCE_LOGIN_URL is required}"
: "${AUTH_ACCEPTANCE_CSRF_URL:?AUTH_ACCEPTANCE_CSRF_URL is required}"
: "${AUTH_ACCEPTANCE_URL:?AUTH_ACCEPTANCE_URL is required}"
: "${AUTH_ACCEPTANCE_USERNAME:?AUTH_ACCEPTANCE_USERNAME is required}"
: "${AUTH_ACCEPTANCE_PASSWORD_FILE:?AUTH_ACCEPTANCE_PASSWORD_FILE is required}"
: "${AUTH_ACCEPTANCE_COOKIE_FILE:?AUTH_ACCEPTANCE_COOKIE_FILE is required}"

[[ -f "$AUTH_ACCEPTANCE_PASSWORD_FILE" ]] || { echo 'AUTH acceptance password file is missing.' >&2; exit 1; }
[[ "$(stat -c '%a' "$AUTH_ACCEPTANCE_PASSWORD_FILE" 2>/dev/null || true)" == 600 ]] || { echo 'AUTH acceptance password file must be mode 600.' >&2; exit 1; }
umask 077
mkdir -p -- "$(dirname -- "$AUTH_ACCEPTANCE_COOKIE_FILE")"
cookie_tmp="${AUTH_ACCEPTANCE_COOKIE_FILE}.tmp.$$"
trap 'rm -f -- "$cookie_tmp"' EXIT

status="$(curl --connect-timeout 5 --max-time 15 --silent --show-error --output /dev/null --write-out '%{http_code}' --cookie-jar "$cookie_tmp" "$AUTH_ACCEPTANCE_CSRF_URL")" || exit 1
[[ "$status" == 200 ]] || { echo 'AUTH acceptance CSRF request failed.' >&2; exit 1; }
csrf_token="$(awk '$6 == "emg_csrf" { print $7; exit }' "$cookie_tmp")"
[[ -n "$csrf_token" ]] || { echo 'AUTH acceptance CSRF cookie was not issued.' >&2; exit 1; }

status="$(printf '%s\0%s' "$AUTH_ACCEPTANCE_USERNAME" "$(cat -- "$AUTH_ACCEPTANCE_PASSWORD_FILE")" |
  python3 -c 'import json,sys; email,password=sys.stdin.buffer.read().split(b"\0",1); print(json.dumps({"email":email.decode(),"password":password.decode(),"rememberMe":True}))' |
  curl --connect-timeout 5 --max-time 15 --silent --show-error --output /dev/null --write-out '%{http_code}' \
    --cookie "$cookie_tmp" --cookie-jar "$cookie_tmp" \
    -H 'Content-Type: application/json' -H "X-CSRF-TOKEN: $csrf_token" \
    --data-binary @- "$AUTH_ACCEPTANCE_LOGIN_URL")" || exit 1
[[ "$status" == 200 ]] || { echo 'AUTH acceptance login failed.' >&2; exit 1; }

status="$(curl --connect-timeout 5 --max-time 15 --silent --show-error --output /dev/null --write-out '%{http_code}' --cookie "$cookie_tmp" "$AUTH_ACCEPTANCE_URL")" || exit 1
[[ "$status" == 200 ]] || { echo 'AUTH acceptance authenticated request failed.' >&2; exit 1; }

mv -f -- "$cookie_tmp" "$AUTH_ACCEPTANCE_COOKIE_FILE"
chmod 600 "$AUTH_ACCEPTANCE_COOKIE_FILE"
