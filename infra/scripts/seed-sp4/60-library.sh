#!/usr/bin/env bash
# 60-library.sh — Add SP4 games to each user's library (Owned / Wishlist).
#
# POST /library/games/{gameId} per user × per gameSlug.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "60 — User Library Entries"

total=0; added=0; existing=0; failed=0

# library is an object keyed by user slug
while IFS= read -r user_slug; do
  user_jar=$(cookie_jar_for "$user_slug")
  # Re-login if cookie jar missing/empty/stale (prior step may have overwritten with empty
  # response after a 5xx error — curl -c truncates the file before writing Set-Cookies).
  if [[ ! -s "$user_jar" ]] || ! grep -qE "meepleai_session\s" "$user_jar" 2>/dev/null; then
    user_email=$(jq -r --arg s "$user_slug" '.users[] | select(.slug==$s) | .email' "$DATA_FILE")
    user_password=$(seed_password)
    if [[ -n "$user_email" ]]; then
      log "[$user_slug] session cookie missing — re-logging in"
      login_user "$user_email" "$user_password" "$user_slug" 2>/dev/null || true
    fi
    if [[ ! -s "$user_jar" ]] || ! grep -qE "meepleai_session\s" "$user_jar" 2>/dev/null; then
      warn "[$user_slug] no session even after re-login — skipping library entries"
      continue
    fi
  fi

  entries=$(data_get_compact ".library.\"$user_slug\"[]?")
  [[ -z "$entries" ]] && continue

  while IFS= read -r e; do
    game_slug=$(jq -r '.gameSlug' <<< "$e")
    status=$(jq -r '.status'   <<< "$e")
    game_id=$(state_get "games" "$game_slug")
    [[ -n "$game_id" ]] || { warn "[$user_slug][$game_slug] missing game_id"; failed=$((failed + 1)); continue; }
    total=$((total + 1))

    payload=$(jq -nc --arg s "$status" '{ status: $s }')
    add=$(curl_json POST "/library/games/$game_id" "$user_jar" "$payload")
    add_body=$(echo "$add" | sed '$d')
    add_code=$(echo "$add" | tail -n1)

    case "$add_code" in
      200|201) added=$((added + 1)) ;;
      409)     existing=$((existing + 1)) ;;
      *)       warn "[$user_slug][$game_slug] HTTP $add_code body=$add_body" ; failed=$((failed + 1)) ;;
    esac
  done <<< "$entries"
done < <(jq -r '.library | keys[] | select(startswith("_") | not)' "$DATA_FILE" | tr -d '\r')

log "Summary: total=$total  added=$added  existing=$existing  failed=$failed"
[[ $failed -gt 0 ]] && warn "60-library: $failed failures (non-fatal)"
ok "60-library complete"
