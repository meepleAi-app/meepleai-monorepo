#!/usr/bin/env bash
# 10-users.sh — Create the 5 SP4 users (Marco, Sara, Luca, Giulia, Andrea).
#
# Idempotent: looks up existing users via GET /admin/users?search= before creating.
# Stores slug → userId in $STATE_FILE under "users".
# Requires admin session (00-bootstrap.sh must run first).

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "10 — Users (5 SP4)"

ADMIN_JAR=$(cookie_jar_for "admin")
[[ -s "$ADMIN_JAR" ]] || fail "admin cookie jar empty — run 00-bootstrap.sh first"

# ---- Verify admin session still valid ----
verify_resp=$(curl_get "/auth/me" "$ADMIN_JAR")
verify_code=$(echo "$verify_resp" | tail -n1)
if [[ "$verify_code" != "200" ]]; then
  log "Admin session stale (HTTP $verify_code), re-logging in"
  admin_login
fi

# ---- Iterate SP4 users ----
total=0; created=0; existing=0; failed=0

password=$(seed_password)
while IFS= read -r u; do
  slug=$(jq -r '.slug'        <<< "$u")
  email=$(jq -r '.email'       <<< "$u")
  display=$(jq -r '.displayName' <<< "$u")
  role=$(jq -r '.role'         <<< "$u")
  total=$((total + 1))

  # Lookup: existing user?
  search_resp=$(curl_get "/admin/users?search=$(jq -rn --arg e "$email" '$e|@uri')&limit=5" "$ADMIN_JAR")
  search_body=$(echo "$search_resp" | sed '$d')
  search_code=$(echo "$search_resp" | tail -n1)

  if [[ "$search_code" != "200" ]]; then
    warn "[$slug] search failed (HTTP $search_code) — skipping"
    failed=$((failed + 1))
    continue
  fi

  # Response shape: { items: [ { id, email, displayName, role } ] } — find by exact email
  user_id=$(echo "$search_body" | jq -r --arg e "$email" '.items[]? | select(.email==$e) | .id' | head -1)

  if [[ -n "$user_id" && "$user_id" != "null" ]]; then
    skip "$slug already exists ($user_id)"
    state_set "users" "$slug" "$user_id"
    existing=$((existing + 1))
    continue
  fi

  # Create
  payload=$(jq -n \
    --arg e "$email" --arg p "$password" --arg d "$display" --arg r "$role" \
    '{email:$e, password:$p, displayName:$d, role:$r}')
  create_resp=$(curl_json POST "/admin/users" "$ADMIN_JAR" "$payload")
  create_body=$(echo "$create_resp" | sed '$d')
  create_code=$(echo "$create_resp" | tail -n1)

  if ! http_check "201|200" "$create_code" "$create_body" "create $slug"; then
    failed=$((failed + 1))
    continue
  fi

  new_id=$(echo "$create_body" | jq -r '.id // .userId // .user.id // empty')
  if [[ -z "$new_id" ]]; then
    warn "[$slug] created but no id in response: $create_body"
    failed=$((failed + 1))
    continue
  fi

  state_set "users" "$slug" "$new_id"
  ok "Created $slug ($email) → $new_id"
  created=$((created + 1))
done < <(data_get_compact '.users[]')

# ---- Force email-verification + tier upgrade on dev seeded users ----
# Two gates need bypassing for SP4 dataset:
#   1) EmailVerificationMiddleware blocks mutations with 403 until EmailVerified=true
#      (production flow needs token via email; no admin-bypass endpoint exists)
#   2) Free tier limits max_agents=1, blocking multi-agent owners (Marco has 2) with
#      HTTP 402 AGENT_SLOT_QUOTA_EXCEEDED. Premium tier raises to max_agents=10.
# Both are direct DB UPDATEs, idempotent, and match the pattern of C# seed handlers
# (SeedBadswormUserCommand, SeedAdminUserCommand) that bypass production gates.
if [[ "${TARGET:-local}" == "local" ]]; then
  emails_csv=$(data_get_compact '.users[]' | jq -r '.email' | awk '{printf "%s'\''%s'\''", sep, $0; sep=","}')
  if [[ -n "$emails_csv" ]]; then
    log "Force-verifying email + upgrading tier=premium for SP4 dev users (direct DB UPDATE, idempotent)…"
    sql="UPDATE users SET
           \"EmailVerified\" = true,
           \"EmailVerifiedAt\" = COALESCE(\"EmailVerifiedAt\", NOW()),
           \"Tier\" = 'premium'
         WHERE \"Email\" IN ($emails_csv)
           AND (\"EmailVerified\" = false OR \"Tier\" <> 'premium')"
    pg_out=$(docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -At -c "$sql" 2>&1) \
      && log "  DB UPDATE: $pg_out" \
      || warn "  DB UPDATE failed: $pg_out (later steps may hit 403/402 gates)"
  fi
else
  warn "TARGET=$TARGET: skipping direct DB email-verify+tier-upgrade (staging needs separate SSH+SQL flow — see docs)"
fi

# ---- Login each user (warm cookie jar for later steps) ----
log "Warming login cookie jars for each user (needed by agent/toolkit/library steps)…"
while IFS= read -r u; do
  slug=$(jq -r '.slug'     <<< "$u")
  email=$(jq -r '.email'    <<< "$u")
  if login_user "$email" "$password" "$slug" 2>/dev/null; then :; else
    warn "[$slug] cookie warm failed (login error) — later steps may need to retry"
  fi
done < <(data_get_compact '.users[]')

log "Summary: total=$total  created=$created  existing=$existing  failed=$failed"
if [[ $failed -gt 0 ]]; then
  fail "10-users completed with $failed failures (see warnings above)"
fi
ok "10-users complete"
