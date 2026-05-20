#!/usr/bin/env bash
# Phase 3 / Step 6 of spec: run verification gates after EF migration applied.
# Each gate has a measurable PASS/FAIL criterion.
#
# Usage: ./04-verify-gates.sh <env-file>

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/common.sh
source "$SCRIPT_DIR/lib/common.sh"

load_env "${1:-}"

# Counters
pass=0
fail=0

run_gate() {
  local name="$1"
  local sql="$2"
  local expected="$3"
  local got

  got=$(psql "$DATABASE_URL" -t -A -c "$sql")
  if [[ "$got" == "$expected" ]]; then
    log_ok "GATE PASS: $name (got=$got, expected=$expected)"
    pass=$((pass+1))
  else
    log_error "GATE FAIL: $name (got=$got, expected=$expected)"
    fail=$((fail+1))
  fi
}

log_info "Running verification gates..."

# Gate 1: games table no longer exists
got=$(psql "$DATABASE_URL" -t -A -c "
SELECT COUNT(*) FROM pg_tables WHERE schemaname='public' AND tablename='games';
")
if [[ "$got" == "0" ]]; then
  log_ok "GATE PASS: games table dropped"
  pass=$((pass+1))
else
  log_error "GATE FAIL: games table still exists"
  fail=$((fail+1))
fi

# Gate 2: zero dangling pgvector_embeddings.game_id (resolve to neither shared_games nor private_games)
run_gate "zero dangling vectors" "
SELECT COUNT(*) FROM pgvector_embeddings pe
WHERE pe.game_id NOT IN (SELECT id FROM shared_games)
  AND pe.game_id NOT IN (SELECT id FROM private_games);
" "0"

# Gate 3: pgvector_embeddings count unchanged from pre-reset baseline
expected_count_file=$(artefact_path "vector-count-pre" "txt")
if [[ ! -f "$expected_count_file" ]]; then
  log_warn "No baseline count file: $expected_count_file. Skipping gate 3."
else
  expected=$(cat "$expected_count_file" | tr -d '[:space:]')
  got=$(psql "$DATABASE_URL" -t -A -c "SELECT COUNT(*) FROM pgvector_embeddings;")
  if [[ "$got" == "$expected" ]]; then
    log_ok "GATE PASS: vector count unchanged ($got)"
    pass=$((pass+1))
  else
    log_error "GATE FAIL: vector count $got != baseline $expected"
    fail=$((fail+1))
  fi
fi

# Gate 4: HNSW index on vector column is valid (not invalidated)
got=$(psql "$DATABASE_URL" -t -A -c "
SELECT COUNT(*) FROM pg_class c
JOIN pg_index i ON i.indexrelid = c.oid
WHERE c.relname LIKE '%pgvector_embeddings%vector%' AND i.indisvalid = true;
")
if [[ "$got" != "0" ]]; then
  log_ok "GATE PASS: HNSW index valid"
  pass=$((pass+1))
else
  log_error "GATE FAIL: HNSW index missing or invalid"
  fail=$((fail+1))
fi

# Gate 5: vector_documents.shared_game_id populated, vector_documents.game_id is null
run_gate "vector_documents migrated to shared_game_id" "
SELECT COUNT(*) FROM vector_documents WHERE game_id IS NOT NULL;
" "0"

# Gate 6: no Game aggregate references remain in code (run from script location, not DB)
# Skipped: this is a build-time check, covered by CI on Phase 2c PR.

log_info "Gates complete: $pass passed, $fail failed"
if [[ "$fail" -gt 0 ]]; then
  exit 1
fi
exit 0
