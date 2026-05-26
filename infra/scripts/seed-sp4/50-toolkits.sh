#!/usr/bin/env bash
# 50-toolkits.sh — Create 4 SP4 toolkits + their tools (multi-login per ownership).
#
# POST /game-toolkits creates the toolkit; POST /game-toolkits/{id}/{kind}-tools
# adds each tool. POST /game-toolkits/{id}/publish flips _publish=true ones.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "50 — Toolkits + Tools (4 SP4, multi-login owners)"

total=0; created=0; existing=0; failed=0; skipped=0; tool_total=0; tool_created=0

while IFS= read -r tk; do
  slug=$(jq -r '.slug'        <<< "$tk")
  owner=$(jq -r '.ownerSlug'  <<< "$tk")
  name=$(jq -r '.name'         <<< "$tk")
  game_slug=$(jq -r '.gameSlug // empty' <<< "$tk")
  do_publish=$(jq -r '._publish' <<< "$tk")
  total=$((total + 1))

  owner_jar=$(cookie_jar_for "$owner")
  if [[ ! -s "$owner_jar" ]]; then
    warn "[$slug] no session for owner '$owner' — run 10-users first; skipping"
    skipped=$((skipped + 1)); continue
  fi

  # Universal toolkit (no gameSlug): pick any game user owns — fall back to azul
  if [[ -z "$game_slug" || "$game_slug" == "null" ]]; then
    game_slug="azul"
    log "[$slug] universal toolkit — defaulting gameSlug=azul (BE requires gameId)"
  fi
  game_id=$(state_get "games" "$game_slug")
  [[ -n "$game_id" ]] || { warn "[$slug] missing game_id for $game_slug"; failed=$((failed + 1)); continue; }

  # Pre-check: any existing toolkit with this name for this user/game?
  list_resp=$(curl_get "/game-toolkits?gameId=$game_id" "$owner_jar")
  list_body=$(echo "$list_resp" | sed '$d')
  list_code=$(echo "$list_resp" | tail -n1)
  existing_id=""
  if [[ "$list_code" == "200" ]]; then
    existing_id=$(echo "$list_body" | jq -r --arg n "$name" '(.items // .)[]? | select(.name==$n) | .id' 2>/dev/null | head -1)
  fi

  if [[ -n "$existing_id" && "$existing_id" != "null" ]]; then
    skip "$slug already exists ($existing_id)"
    state_set "toolkits" "$slug" "$existing_id"
    existing=$((existing + 1))
    toolkit_id="$existing_id"
  else
    create_payload=$(jq -nc --arg game_id "$game_id" --arg name "$name" '{ gameId: $game_id, name: $name }')
    cr=$(curl_json POST "/game-toolkits" "$owner_jar" "$create_payload")
    cr_body=$(echo "$cr" | sed '$d')
    cr_code=$(echo "$cr" | tail -n1)
    if ! http_check "201|200" "$cr_code" "$cr_body" "create toolkit $slug"; then
      failed=$((failed + 1)); continue
    fi
    toolkit_id=$(echo "$cr_body" | jq -r '.id // .toolkitId // empty')
    [[ -n "$toolkit_id" && "$toolkit_id" != "null" ]] || { warn "[$slug] no id in response: $cr_body"; failed=$((failed + 1)); continue; }
    state_set "toolkits" "$slug" "$toolkit_id"
    ok "Created toolkit $slug (owner=$owner, game=$game_slug) → $toolkit_id"
    created=$((created + 1))
  fi

  # Add tools (nested endpoints)
  while IFS= read -r tool; do
    kind=$(jq -r '.kind' <<< "$tool")
    tool_total=$((tool_total + 1))

    case "$kind" in
      timer)
        tool_payload=$(jq -c '{
          name:.name, durationSeconds:.durationSeconds, timerType:.timerType,
          autoStart:false, color:"#3b82f6", isPerPlayer:.isPerPlayer,
          warningThresholdSeconds:.warningThresholdSeconds
        }' <<< "$tool")
        endpoint="timer-tools"
        ;;
      counter)
        tool_payload=$(jq -c '{
          name:.name, minValue:.minValue, maxValue:.maxValue,
          defaultValue:.defaultValue, isPerPlayer:.isPerPlayer,
          icon:"🔢", color:"#22c55e"
        }' <<< "$tool")
        endpoint="counter-tools"
        ;;
      dice)
        tool_payload=$(jq -c '{
          name:.name, diceType:.diceType, quantity:.quantity,
          isInteractive:.isInteractive, color:"#f59e0b"
        }' <<< "$tool")
        endpoint="dice-tools"
        ;;
      *)
        skip "[$slug] tool kind '$kind' not yet wired — skipping"
        continue
        ;;
    esac

    add=$(curl_json POST "/game-toolkits/$toolkit_id/$endpoint" "$owner_jar" "$tool_payload")
    add_code=$(echo "$add" | tail -n1)
    if grep -qE "^(200|201|409)$" <<< "$add_code"; then
      tool_created=$((tool_created + 1))
    else
      warn "[$slug] add $kind tool HTTP $add_code"
    fi
  done < <(jq -c '.tools[]?' <<< "$tk" | tr -d '\r')

  # Publish (if requested)
  if [[ "$do_publish" == "true" ]]; then
    pub=$(curl_json POST "/game-toolkits/$toolkit_id/publish" "$owner_jar")
    pub_code=$(echo "$pub" | tail -n1)
    grep -qE "^(200|201|204|400|409)$" <<< "$pub_code" || warn "[$slug] publish HTTP $pub_code"
  fi
done < <(data_get_compact '.toolkits[]')

log "Summary: toolkits=$total (created=$created existing=$existing skipped=$skipped failed=$failed)  tools=$tool_total (created/ok=$tool_created)"
if [[ $failed -gt 0 ]]; then
  # KNOWN BE BUG: POST /game-toolkits returns 500 because "RowVersion" column is not-null
  # without DB default on Postgres (IsRowVersion() works on SQL Server only). Logging as
  # warning + continuing — toolkits page will show empty state, but other pages aren't
  # blocked. Tracked separately (file BE issue if needed). To re-enable strict mode:
  # set SEED_STRICT=true to make this step fail-fast.
  if [[ "${SEED_STRICT:-false}" == "true" ]]; then
    fail "50-toolkits completed with $failed failures (strict mode)"
  else
    warn "50-toolkits completed with $failed failures — continuing (BE RowVersion bug, see comment)"
  fi
fi
ok "50-toolkits complete"
