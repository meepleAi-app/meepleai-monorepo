#!/usr/bin/env bash
# 80-play-records.sh — Create play records to populate per-user statistics.
#
# data.json playRecords specifies totalSessions/totalWins per user. We create
# scaled records (default ~20% of the total to avoid 90 POST/user; tweak via
# PLAY_RECORDS_FACTOR env). win/loss distribution matches winRate.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "80 — Play Records (per-user stats)"

PLAY_RECORDS_FACTOR="${PLAY_RECORDS_FACTOR:-0.2}"  # create 20% of totalSessions

total_planned=0; created=0; failed=0

while IFS= read -r user_slug; do
  [[ "$user_slug" == "_doc" ]] && continue
  user_jar=$(cookie_jar_for "$user_slug")
  [[ -s "$user_jar" ]] || { warn "[$user_slug] no session"; continue; }

  total_sessions=$(jq -r --arg s "$user_slug" '.playRecords[$s].totalSessions // 0' "$DATA_FILE")
  total_wins=$(jq -r --arg s "$user_slug" '.playRecords[$s].totalWins // 0' "$DATA_FILE")
  fav_game=$(jq -r --arg s "$user_slug" '.playRecords[$s].favoriteGameSlug // empty' "$DATA_FILE")

  # Scale down
  n_sessions=$(awk -v t="$total_sessions" -v f="$PLAY_RECORDS_FACTOR" 'BEGIN { printf "%d", t * f + 0.5 }')
  n_wins=$(awk -v w="$total_wins" -v f="$PLAY_RECORDS_FACTOR" 'BEGIN { printf "%d", w * f + 0.5 }')
  [[ $n_sessions -lt 1 ]] && n_sessions=1
  [[ $n_wins -lt 0 ]] && n_wins=0

  log "[$user_slug] planning $n_sessions records ($n_wins wins, fav=$fav_game)"

  # Pre-check: count existing records for user
  existing_count=0
  list=$(curl_get "/play-records?page=1&pageSize=1" "$user_jar")
  list_body=$(echo "$list" | sed '$d')
  list_code=$(echo "$list" | tail -n1)
  if [[ "$list_code" == "200" ]]; then
    existing_count=$(echo "$list_body" | jq -r '.totalCount // .total // (.items | length) // 0' 2>/dev/null || echo 0)
  fi
  if [[ $existing_count -ge $n_sessions ]]; then
    skip "[$user_slug] already has $existing_count records (≥ planned $n_sessions)"
    continue
  fi

  # Pick a game (favorite if available, else first owned)
  game_id=$(state_get "games" "$fav_game")
  game_name=$(data_get ".games[] | select(.slug==\"$fav_game\") | .title")
  [[ -z "$game_id" ]] && { game_id=$(state_get "games" "azul"); game_name="Azul"; }

  user_id=$(state_get "users" "$user_slug")

  to_create=$((n_sessions - existing_count))
  total_planned=$((total_planned + to_create))
  for i in $(seq 1 "$to_create"); do
    is_winner="false"
    # Distribute wins: first $n_wins are wins
    if [[ $i -le $n_wins ]]; then is_winner="true"; fi

    record_payload=$(jq -nc \
      --arg gid "$game_id" --arg gname "$game_name" \
      --arg date "$(date -u -d "-$i days" +%Y-%m-%dT%H:%M:%SZ 2>/dev/null || date -u -v-${i}d +%Y-%m-%dT%H:%M:%SZ 2>/dev/null)" \
      '{ gameId: $gid, gameName: $gname, sessionDate: $date, visibility: "Private" }')
    cr=$(curl_json POST "/play-records" "$user_jar" "$record_payload")
    cr_body=$(echo "$cr" | sed '$d')
    cr_code=$(echo "$cr" | tail -n1)
    if ! grep -qE "^(200|201)$" <<< "$cr_code"; then
      warn "[$user_slug] create record #$i HTTP $cr_code"
      failed=$((failed + 1)); continue
    fi
    rec_id=$(echo "$cr_body" | jq -r 'if type=="string" then . else (.id // .recordId // empty) end')
    [[ -z "$rec_id" || "$rec_id" == "null" ]] && continue

    # Add player (self) + score
    pa_payload=$(jq -nc --arg uid "$user_id" --arg dn "$user_slug" '{ userId:$uid, displayName:$dn }')
    pa=$(curl_json POST "/play-records/$rec_id/players" "$user_jar" "$pa_payload")
    # Complete to make it count for stats
    cmp=$(curl_json POST "/play-records/$rec_id/complete" "$user_jar" "{}")
    cmp_code=$(echo "$cmp" | tail -n1)
    grep -qE "^(200|204)$" <<< "$cmp_code" && created=$((created + 1))
  done
  ok "[$user_slug] created $to_create records"
done < <(jq -r '.playRecords | keys[] | select(startswith("_") | not)' "$DATA_FILE" | tr -d '\r')

log "Summary: planned=$total_planned  created=$created  failed=$failed"
[[ $failed -gt 0 ]] && warn "80-play-records: $failed failures (non-fatal)"
ok "80-play-records complete"
