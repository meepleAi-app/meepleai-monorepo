#!/usr/bin/env bash
# 40-agents.sh — Create 5 SP4 agents (multi-login per ownership).
#
# Each agent in data.json has ownerSlug → agent created under that user's session.
# Uses POST /agents/create-with-setup (same as nanolith pattern).
# kbSlugs resolved to documentIds via state file.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "40 — Agents (5 SP4, multi-login owners)"

total=0; created=0; existing=0; failed=0; skipped=0

while IFS= read -r a; do
  slug=$(jq -r '.slug'      <<< "$a")
  owner=$(jq -r '.ownerSlug' <<< "$a")
  name=$(jq -r '.name'       <<< "$a")
  game_slug=$(jq -r '.gameSlug // empty' <<< "$a")
  agent_type=$(jq -r '.agentType'  <<< "$a")
  strategy=$(jq -r '.strategyName' <<< "$a")
  total=$((total + 1))

  # Resolve owner cookie jar
  owner_jar=$(cookie_jar_for "$owner")
  if [[ ! -s "$owner_jar" ]]; then
    warn "[$slug] no session for owner '$owner' — run 10-users first; skipping"
    skipped=$((skipped + 1)); continue
  fi

  # Resolve game_id (null gameSlug means universal agent — skip per current BE constraint)
  if [[ -z "$game_slug" || "$game_slug" == "null" ]]; then
    skip "$slug: universal agent (no gameSlug) — skipping, BE requires gameId for create-with-setup"
    skipped=$((skipped + 1)); continue
  fi
  game_id=$(state_get "games" "$game_slug")
  [[ -n "$game_id" ]] || { warn "[$slug] missing game_id for $game_slug"; failed=$((failed + 1)); continue; }

  # Pre-check: does owner already have an agent with this name+game? (GET /agents,
  # filtered client-side by gameId+name — there's no /agents?gameId filter param)
  list_resp=$(curl_get "/agents" "$owner_jar")
  list_body=$(echo "$list_resp" | sed '$d')
  list_code=$(echo "$list_resp" | tail -n1)
  if [[ "$list_code" == "200" ]]; then
    existing_id=$(echo "$list_body" | jq -r --arg n "$name" --arg g "$game_id" \
      '.agents[]? | select(.name==$n and .gameId==$g) | .id' 2>/dev/null | head -1)
    if [[ -n "$existing_id" && "$existing_id" != "null" ]]; then
      skip "$slug already exists ($existing_id)"
      state_set "agents" "$slug" "$existing_id"
      existing=$((existing + 1)); continue
    fi
  fi

  # Resolve kb document IDs (optional)
  kb_doc_ids=$(jq -r '.kbSlugs[]?' <<< "$a" | while IFS= read -r kb_slug; do
    [[ -z "$kb_slug" ]] && continue
    state_get "kbDocs" "$kb_slug"
  done | jq -R . | jq -sc 'map(select(. != ""))')

  # Build create-with-setup payload
  payload=$(jq -nc \
    --arg name "$name" \
    --arg game_id "$game_id" \
    --arg agent_type "$agent_type" \
    --arg strategy "$strategy" \
    --argjson docs "${kb_doc_ids:-[]}" \
    '{
      gameId: $game_id,
      addToCollection: true,
      agentType: $agent_type,
      agentName: $name,
      strategyName: $strategy,
      documentIds: $docs
    }')

  cr=$(curl_json POST "/agents/create-with-setup" "$owner_jar" "$payload")
  cr_body=$(echo "$cr" | sed '$d')
  cr_code=$(echo "$cr" | tail -n1)

  if ! http_check "201|200" "$cr_code" "$cr_body" "create agent $slug"; then
    failed=$((failed + 1)); continue
  fi

  new_id=$(echo "$cr_body" | jq -r '.agentId // .id // .agent.id // empty')
  [[ -n "$new_id" && "$new_id" != "null" ]] || { warn "[$slug] no agentId in response: $cr_body"; failed=$((failed + 1)); continue; }

  state_set "agents" "$slug" "$new_id"
  ok "Created agent $slug (owner=$owner, game=$game_slug) → $new_id"
  created=$((created + 1))
done < <(data_get_compact '.agents[]')

log "Summary: total=$total  created=$created  existing=$existing  skipped=$skipped  failed=$failed"
[[ $failed -gt 0 ]] && fail "40-agents completed with $failed failures"
ok "40-agents complete"
