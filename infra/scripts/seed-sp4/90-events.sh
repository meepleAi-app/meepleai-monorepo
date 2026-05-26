#!/usr/bin/env bash
# 90-events.sh — Create 4 SP4 game nights + invites.
#
# POST /game-nights creates draft; POST /game-nights/{id}/publish flips to live.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "90 — Game Nights (4 SP4)"

total=0; created=0; failed=0; skipped=0

slot_offset=0
while IFS= read -r e; do
  slug=$(jq -r '.slug'      <<< "$e")
  owner=$(jq -r '.ownerSlug' <<< "$e")
  title=$(jq -r '.title'    <<< "$e")
  loc=$(jq -r '.location'  <<< "$e")
  do_publish=$(jq -r '._publish' <<< "$e")
  total=$((total + 1))

  # Mock dateTime values are static and may be in the past; BE requires scheduledAt >= NOW+1h.
  # Generate forward-looking timestamps spaced 4 days apart to preserve mock semantics.
  dt=$(date -u -d "+$((4 + slot_offset)) days" +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null \
    || date -u -v+$((4 + slot_offset))d +"%Y-%m-%dT%H:%M:%SZ")
  slot_offset=$((slot_offset + 4))

  owner_jar=$(cookie_jar_for "$owner")
  [[ -s "$owner_jar" ]] || { warn "[$slug] no session for owner '$owner'"; failed=$((failed + 1)); continue; }

  # Build participant + game id arrays from state file
  participant_ids=$(jq -r '.participantSlugs[]?' <<< "$e" | while IFS= read -r p; do state_get "users" "$p"; done | jq -R . | jq -sc 'map(select(. != ""))')
  game_ids=$(jq -r '.gameSlugs[]?' <<< "$e" | while IFS= read -r g; do state_get "games" "$g"; done | jq -R . | jq -sc 'map(select(. != ""))')

  payload=$(jq -nc \
    --arg title "$title" --arg dt "$dt" --arg loc "$loc" \
    --argjson participants "${participant_ids:-[]}" \
    --argjson games "${game_ids:-[]}" \
    '{ title: $title, scheduledAt: $dt, location: $loc, participantIds: $participants, gameIds: $games }')

  cr=$(curl_json POST "/game-nights" "$owner_jar" "$payload")
  cr_body=$(echo "$cr" | sed '$d')
  cr_code=$(echo "$cr" | tail -n1)
  if ! http_check "201|200" "$cr_code" "$cr_body" "create game-night $slug"; then
    failed=$((failed + 1)); continue
  fi
  gn_id=$(echo "$cr_body" | jq -r 'if type=="string" then . else (.id // empty) end')
  [[ -n "$gn_id" && "$gn_id" != "null" ]] || { warn "[$slug] no id: $cr_body"; failed=$((failed + 1)); continue; }
  state_set "events" "$slug" "$gn_id"

  # Publish?
  if [[ "$do_publish" == "true" ]]; then
    pub=$(curl_json POST "/game-nights/$gn_id/publish" "$owner_jar")
    pub_code=$(echo "$pub" | tail -n1)
    grep -qE "^(200|204|400|409)$" <<< "$pub_code" || warn "[$slug] publish HTTP $pub_code"
  fi

  ok "Created game-night $slug → $gn_id"
  created=$((created + 1))
done < <(data_get_compact '.events[]')

log "Summary: total=$total  created=$created  skipped=$skipped  failed=$failed"
[[ $failed -gt 0 ]] && warn "90-events: $failed failures (non-fatal)"
ok "90-events complete"
