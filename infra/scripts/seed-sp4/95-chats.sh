#!/usr/bin/env bash
# 95-chats.sh — Create 5 SP4 chat threads (empty; AI responses come from real LLM at runtime).
#
# POST /chat/sessions per chat. agentSlug + gameSlug resolved via state file.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "95 — Chat Threads (5 SP4, empty)"

total=0; created=0; failed=0; skipped=0

while IFS= read -r c; do
  slug=$(jq -r '.slug'        <<< "$c")
  owner=$(jq -r '.ownerSlug'  <<< "$c")
  title=$(jq -r '.title'      <<< "$c")
  agent_slug=$(jq -r '.agentSlug' <<< "$c")
  game_slug=$(jq -r '.gameSlug'  <<< "$c")
  total=$((total + 1))

  owner_jar=$(cookie_jar_for "$owner")
  [[ -s "$owner_jar" ]] || { warn "[$slug] no session for owner '$owner'"; failed=$((failed + 1)); continue; }

  game_id=$(state_get "games" "$game_slug")
  agent_id=$(state_get "agents" "$agent_slug")
  [[ -n "$game_id" ]] || { warn "[$slug] missing game_id for $game_slug"; failed=$((failed + 1)); continue; }
  if [[ -z "$agent_id" ]]; then
    skip "[$slug] missing agent_id for $agent_slug (agent step may have skipped it) — skipping chat"
    skipped=$((skipped + 1)); continue
  fi

  payload=$(jq -nc \
    --arg gid "$game_id" --arg title "$title" --arg aid "$agent_id" \
    '{ gameId: $gid, title: $title, agentId: $aid }')
  cr=$(curl_json POST "/chat/sessions" "$owner_jar" "$payload")
  cr_body=$(echo "$cr" | sed '$d')
  cr_code=$(echo "$cr" | tail -n1)
  if ! http_check "201|200" "$cr_code" "$cr_body" "create chat $slug"; then
    failed=$((failed + 1)); continue
  fi
  ch_id=$(echo "$cr_body" | jq -r '.sessionId // .id // empty')
  [[ -n "$ch_id" && "$ch_id" != "null" ]] || { warn "[$slug] no sessionId: $cr_body"; failed=$((failed + 1)); continue; }
  state_set "chats" "$slug" "$ch_id"
  ok "Created chat thread $slug → $ch_id"
  created=$((created + 1))
done < <(data_get_compact '.chats[]')

log "Summary: total=$total  created=$created  skipped=$skipped  failed=$failed"
[[ $failed -gt 0 ]] && warn "95-chats: $failed failures (non-fatal)"
ok "95-chats complete"
