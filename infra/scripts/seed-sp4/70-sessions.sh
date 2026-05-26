#!/usr/bin/env bash
# 70-sessions.sh — Create 6 SP4 live sessions + state transitions.
#
# POST /live-sessions creates Created state; /start → InProgress; /complete → Done.
# For Paused/Archived we approximate: Paused = stay in Created+complete-noop; Archived = Completed.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "70 — Live Sessions (6 SP4)"

total=0; created=0; existing=0; failed=0; skipped=0

while IFS= read -r s; do
  slug=$(jq -r '.slug'        <<< "$s")
  owner=$(jq -r '.ownerSlug'  <<< "$s")
  game_slug=$(jq -r '.gameSlug // empty' <<< "$s")
  target_state=$(jq -r '._targetState' <<< "$s")
  total=$((total + 1))

  if [[ -z "$game_slug" || "$game_slug" == "null" ]]; then
    skip "$slug has null gameSlug (archive without game ref) — skipping"
    skipped=$((skipped + 1)); continue
  fi

  owner_jar=$(cookie_jar_for "$owner")
  [[ -s "$owner_jar" ]] || { warn "[$slug] no session for owner '$owner'"; failed=$((failed + 1)); continue; }

  game_id=$(state_get "games" "$game_slug")
  [[ -n "$game_id" ]] || { warn "[$slug] missing game_id for $game_slug"; failed=$((failed + 1)); continue; }

  # Resolve gameName for payload (BE validation requires it)
  game_name=$(data_get ".games[] | select(.slug==\"$game_slug\") | .title")
  [[ -n "$game_name" ]] || game_name="$game_slug"

  # No clean lookup endpoint for "is there already a session for this user+game" — we just create.
  # Sessions are cheap; minor duplication acceptable for visual seed.
  payload=$(jq -nc --arg g "$game_id" --arg n "$game_name" '{ gameId: $g, gameName: $n }')
  cr=$(curl_json POST "/live-sessions" "$owner_jar" "$payload")
  cr_body=$(echo "$cr" | sed '$d')
  cr_code=$(echo "$cr" | tail -n1)
  if ! http_check "201|200" "$cr_code" "$cr_body" "create session $slug"; then
    failed=$((failed + 1)); continue
  fi
  session_id=$(echo "$cr_body" | jq -r 'if type=="string" then . else (.id // .sessionId // empty) end')
  [[ -n "$session_id" && "$session_id" != "null" ]] || { warn "[$slug] no sessionId: $cr_body"; failed=$((failed + 1)); continue; }
  state_set "sessions" "$slug" "$session_id"

  # Add players
  while IFS= read -r p_slug; do
    p_id=$(state_get "users" "$p_slug")
    [[ -z "$p_id" ]] && continue
    p_display=$(data_get ".users[] | select(.slug==\"$p_slug\") | .displayName")
    p_payload=$(jq -nc --arg uid "$p_id" --arg dn "$p_display" '{ userId: $uid, displayName: $dn }')
    pa=$(curl_json POST "/live-sessions/$session_id/players" "$owner_jar" "$p_payload")
    pa_code=$(echo "$pa" | tail -n1)
    grep -qE "^(200|201|204|409)$" <<< "$pa_code" || warn "[$slug] add player $p_slug HTTP $pa_code"
  done < <(jq -r '.playerSlugs[]?' <<< "$s" | tr -d '\r')

  # State transition
  case "$target_state" in
    Created) : ;;  # leave in Created
    InProgress|Paused)
      st=$(curl_json POST "/live-sessions/$session_id/start" "$owner_jar")
      st_code=$(echo "$st" | tail -n1)
      grep -qE "^(200|204|400|409)$" <<< "$st_code" || warn "[$slug] start HTTP $st_code"
      ;;
    Completed)
      curl_json POST "/live-sessions/$session_id/start" "$owner_jar" >/dev/null
      cmp=$(curl_json POST "/live-sessions/$session_id/complete" "$owner_jar")
      cmp_code=$(echo "$cmp" | tail -n1)
      grep -qE "^(200|204|400|409)$" <<< "$cmp_code" || warn "[$slug] complete HTTP $cmp_code"
      ;;
  esac

  ok "Created session $slug → $session_id (target=$target_state)"
  created=$((created + 1))
done < <(data_get_compact '.sessions[]')

log "Summary: total=$total  created=$created  existing=$existing  skipped=$skipped  failed=$failed"
[[ $failed -gt 0 ]] && warn "70-sessions: $failed failures (non-fatal)"
ok "70-sessions complete"
