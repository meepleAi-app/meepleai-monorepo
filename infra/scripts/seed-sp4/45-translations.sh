#!/usr/bin/env bash
# 45-translations.sh — Seed IT translation rows for SP4 games (Issue #2339 sub-PR 3/3).
#
# Reads infra/scripts/seed-sp4/translations.json (publisher-verified rows; see
# translations-research.md for the decision rationale).
#
# Each translation is POSTed to:
#   POST /api/v1/admin/games/{gameId}/translations
#   body: { locale, title, description, source }
#
# Idempotent: lookup first via GET, only POST when the row is missing.
# Tolerant: a missing game in state is logged + skipped (downstream steps continue).
#
# Endpoint shipped via Wave 5 of sub-PR 1/3 → SharedGameTranslationEndpoints.cs.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "45 — Translations (IT seed, publisher-verified)"

translations_file="$(dirname "$(readlink -f "$0")")/translations.json"
if [[ ! -f "$translations_file" ]]; then
  warn "translations.json missing — nothing to seed; skipping"
  exit 0
fi

# Need an admin cookie jar to POST. The 10-users.sh step creates an admin
# session at admin@meepleai.test (see data.json users[].slug=admin).
admin_jar=$(cookie_jar_for "admin")
if [[ ! -s "$admin_jar" ]]; then
  warn "no admin session — run 10-users first; aborting translations seed"
  exit 0
fi

total=0; created=0; existing=0; failed=0; skipped=0

while IFS= read -r entry; do
  game_slug=$(jq -r '.gameSlug' <<< "$entry")
  locale=$(jq -r '.locale'     <<< "$entry")
  title=$(jq -r '.title'      <<< "$entry")
  description=$(jq -r '.description' <<< "$entry")
  source=$(jq -r '.source'     <<< "$entry")
  total=$((total + 1))

  # Resolve game_id via state file (20-games.sh indexes by slug).
  game_id=$(state_get "games" "$game_slug")
  if [[ -z "$game_id" ]]; then
    warn "[$game_slug/$locale] no game_id in state — run 20-games first; skipping"
    skipped=$((skipped + 1)); continue
  fi

  # Pre-check: does this translation already exist for the (gameId, locale)?
  existing_check=$(curl -sS -b "$admin_jar" \
    -o /dev/null -w "%{http_code}" \
    "$API_BASE/admin/games/$game_id/translations/$locale" || echo "000")
  if [[ "$existing_check" == "200" ]]; then
    ok "[$game_slug/$locale] already exists — skipping"
    existing=$((existing + 1)); continue
  fi

  # POST the translation row.
  payload=$(jq -nc \
    --arg locale "$locale" \
    --arg title "$title" \
    --arg source "$source" \
    --argjson description "$([[ "$description" == "null" ]] && echo "null" || jq -nc --arg d "$description" '$d')" \
    '{locale: $locale, title: $title, description: $description, source: $source}')

  resp=$(curl -sS -b "$admin_jar" \
    -X POST -H "Content-Type: application/json" \
    -d "$payload" \
    -o /tmp/seed-sp4-translation-resp.json -w "%{http_code}" \
    "$API_BASE/admin/games/$game_id/translations" || echo "000")

  case "$resp" in
    201)
      ok "[$game_slug/$locale] created → $title"
      created=$((created + 1))
      ;;
    409)
      ok "[$game_slug/$locale] already exists (race) — treating as success"
      existing=$((existing + 1))
      ;;
    *)
      warn "[$game_slug/$locale] POST returned $resp — body: $(cat /tmp/seed-sp4-translation-resp.json 2>/dev/null || echo '<empty>')"
      failed=$((failed + 1))
      ;;
  esac

  rm -f /tmp/seed-sp4-translation-resp.json
done < <(jq -c '.translations[]' "$translations_file")

log "Translations: total=$total created=$created existing=$existing failed=$failed skipped=$skipped"

if [[ $failed -gt 0 ]]; then
  warn "Translation seed had $failed failure(s) — review log above"
  exit 1
fi
