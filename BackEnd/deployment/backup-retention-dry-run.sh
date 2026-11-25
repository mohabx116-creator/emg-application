#!/usr/bin/env bash
set -Eeuo pipefail

: "${BACKUP_ROOT:?BACKUP_ROOT is required}"
: "${WEEKLY_RETENTION:=4}"
: "${MONTHLY_RETENTION:=12}"

for category in weekly monthly; do
  keep="$WEEKLY_RETENTION"
  [[ "$category" == monthly ]] && keep="$MONTHLY_RETENTION"
  mapfile -t sets < <(find "$BACKUP_ROOT/$category" -mindepth 1 -maxdepth 1 -type d -name 'emg-*' -printf '%T@ %p\n' | sort -rn | awk '{print $2}')
  if (( ${#sets[@]} > keep )); then
    printf 'DRY-RUN would remove (%s):\n' "$category"
    printf '%s\n' "${sets[@]:keep}"
  else
    printf 'DRY-RUN no removals (%s): %d sets, retention %d.\n' "$category" "${#sets[@]}" "$keep"
  fi
done
