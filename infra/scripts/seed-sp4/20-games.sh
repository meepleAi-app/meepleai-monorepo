#!/usr/bin/env bash
# 20-games.sh — Create the 8 SP4 SharedGames + quick-publish.
#
# Idempotent: GET /admin/shared-games?search=title to find existing.
# Stores slug → gameId in $STATE_FILE under "games".

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "20 — Shared Games (8 SP4)"
ADMIN_JAR=$(cookie_jar_for "admin")
[[ -s "$ADMIN_JAR" ]] || { admin_login; }

total=0; created=0; existing=0; published=0; failed=0

while IFS= read -r g; do
  slug=$(jq -r '.slug'     <<< "$g")
  title=$(jq -r '.title'    <<< "$g")
  total=$((total + 1))

  # Lookup (search by title, exact match in result)
  search_resp=$(curl_get "/admin/shared-games?search=$(jq -rn --arg t "$title" '$t|@uri')&limit=10" "$ADMIN_JAR")
  search_body=$(echo "$search_resp" | sed '$d')
  search_code=$(echo "$search_resp" | tail -n1)

  game_id=""
  if [[ "$search_code" == "200" ]]; then
    game_id=$(echo "$search_body" | jq -r --arg t "$title" '.items[]? | select(.title==$t) | .id' | head -1)
  fi

  if [[ -n "$game_id" && "$game_id" != "null" ]]; then
    skip "$slug already exists ($game_id) — will ensure published"
    existing=$((existing + 1))
  else
    # Create draft
    create_payload=$(jq -c '{
      title:.title, yearPublished:.year,
      description:.description,
      imageUrl:.imageUrl, thumbnailUrl:.thumbnailUrl,
      minPlayers:.minPlayers, maxPlayers:.maxPlayers,
      playingTimeMinutes:.playingTimeMinutes,
      complexityRating:.complexityRating, averageRating:.averageRating,
      designers:.designers, publishers:[.publisher]
    }' <<< "$g")

    cr=$(curl_json POST "/admin/shared-games" "$ADMIN_JAR" "$create_payload")
    cr_body=$(echo "$cr" | sed '$d')
    cr_code=$(echo "$cr" | tail -n1)

    if ! http_check "201|200" "$cr_code" "$cr_body" "create $slug"; then
      failed=$((failed + 1)); continue
    fi

    # Response can be raw GUID or { id: GUID }
    game_id=$(echo "$cr_body" | jq -r 'if type=="string" then . else (.id // .gameId // .game.id // empty) end' 2>/dev/null)
    [[ -n "$game_id" && "$game_id" != "null" ]] || { warn "[$slug] no id in response: $cr_body"; failed=$((failed + 1)); continue; }
    ok "Created $slug → $game_id"
    created=$((created + 1))
  fi

  state_set "games" "$slug" "$game_id"

  # Quick-publish (idempotent server-side; 200/204 = success, 400/409 = already published)
  pub=$(curl_json POST "/admin/shared-games/$game_id/quick-publish" "$ADMIN_JAR")
  pub_code=$(echo "$pub" | tail -n1)
  if grep -qE "^(200|204)$" <<< "$pub_code"; then
    published=$((published + 1))
  elif grep -qE "^(400|409)$" <<< "$pub_code"; then
    # Already published / draft state already complete — fine
    skip "$slug publish: HTTP $pub_code (already published)"
    published=$((published + 1))
  else
    warn "[$slug] quick-publish HTTP $pub_code (unexpected)"
  fi
done < <(data_get_compact '.games[]')

log "Summary: total=$total  created=$created  existing=$existing  published=$published  failed=$failed"
[[ $failed -gt 0 ]] && fail "20-games completed with $failed failures"
ok "20-games complete"
