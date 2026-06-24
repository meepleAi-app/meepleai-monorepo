#!/usr/bin/env bash
# infra/scripts/rag-smoke-assert.sh
# RAG retrieval smoke test (#2480, Tier 3 D8 of #2126).
#
# Runs the canonical queries in fixtures/rag-canonical-queries.json against the
# /knowledge-base/ask/global SSE endpoint and asserts the top-K retrieved chunks
# (Citations event, type=1 — produced by the vector search BEFORE the LLM, so the
# assertion is independent of OpenRouter/LLM availability) match the golden baseline
# in fixtures/rag-golden-baseline.json.
#
# Usage:
#   bash scripts/rag-smoke-assert.sh                 # assert against the golden baseline
#   bash scripts/rag-smoke-assert.sh --update-baseline  # capture the baseline from current retrieval
#   API_BASE_URL=http://localhost:8080 SMOKE_EMAIL=... SMOKE_PASSWORD=... bash scripts/rag-smoke-assert.sh
#
# Exit: 0 = all queries match baseline (or baseline updated); 1 = a query drifted / no citations.
set -uo pipefail

BASE_URL="${API_BASE_URL:-http://localhost:8080}"
EMAIL="${SMOKE_EMAIL:-${TEST_EMAIL:-}}"
PASSWORD="${SMOKE_PASSWORD:-${TEST_PASSWORD:-}}"
UPDATE_BASELINE=false

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"   # infra/
QUERIES="$DIR/fixtures/rag-canonical-queries.json"
BASELINE="$DIR/fixtures/rag-golden-baseline.json"

for arg in "$@"; do
  case "$arg" in
    --update-baseline) UPDATE_BASELINE=true ;;
    --base-url=*) BASE_URL="${arg#*=}" ;;
    *) echo "::error:: unknown arg: $arg" >&2; exit 2 ;;
  esac
done

log()  { echo "[rag-smoke] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

command -v jq   >/dev/null || fail "jq is required"
command -v curl >/dev/null || fail "curl is required"
[ -f "$QUERIES" ]  || fail "missing $QUERIES"
[ -f "$BASELINE" ] || fail "missing $BASELINE"

ENDPOINT=$(jq -r '.endpoint' "$QUERIES")
TOPK=$(jq -r '.topK' "$QUERIES")
LANG=$(jq -r '.language' "$QUERIES")

COOKIE=$(mktemp); trap 'rm -f "$COOKIE" "${TMP_BASELINE:-}"' EXIT

# --- login (optional: endpoint may accept a preset session) ---
if [ -n "$EMAIL" ] && [ -n "$PASSWORD" ]; then
  code=$(curl -s -o /dev/null -w '%{http_code}' -c "$COOKIE" -X POST "$BASE_URL/api/v1/auth/login" \
    -H 'Content-Type: application/json' \
    -d "$(jq -n --arg e "$EMAIL" --arg p "$PASSWORD" '{email:$e, password:$p}')") || true
  [ "$code" = "200" ] || fail "login failed (HTTP ${code:-000})"
  log "login OK ($EMAIL)"
else
  log "no SMOKE_EMAIL/SMOKE_PASSWORD set — calling endpoint without an explicit login"
fi

# Extract the top-K {source,page} from the first Citations SSE event (type=1).
fetch_top_chunks() {
  local query="$1"
  curl -sN -b "$COOKIE" -X POST "$BASE_URL$ENDPOINT" \
    -H 'Content-Type: application/json' \
    -d "$(jq -n --arg q "$query" --arg l "$LANG" --argjson k "$TOPK" '{query:$q, language:$l, topK:$k}')" 2>/dev/null \
    | grep '^data: ' | sed 's/^data: //' \
    | jq -c 'select(.type==1) | .data.citations' 2>/dev/null | head -1 \
    | jq -c "if . == null then [] else (sort_by(-.score, .source, .page) | .[0:$TOPK] | map({source, page})) end" 2>/dev/null
}

PASS=0; FAIL=0
TMP_BASELINE=$(mktemp); echo '{}' > "$TMP_BASELINE"

while IFS= read -r qid; do
  query=$(jq -r --arg id "$qid" '.queries[] | select(.queryId==$id) | .query' "$QUERIES")
  top=$(fetch_top_chunks "$query")

  if [ -z "$top" ] || [ "$top" = "[]" ]; then
    echo "FAIL  $qid — no citations (type=1 empty/absent: cold index, auth failure, or non-SSE error body). Check login + that the snapshot is loaded."
    FAIL=$((FAIL+1)); continue
  fi

  if [ "$UPDATE_BASELINE" = true ]; then
    jq --arg id "$qid" --argjson v "$top" '.[$id]=$v' "$TMP_BASELINE" > "$TMP_BASELINE.2" \
      && mv "$TMP_BASELINE.2" "$TMP_BASELINE"
    log "captured $qid → $top"
    continue
  fi

  expected=$(jq -c --arg id "$qid" '.baseline[$id] // empty' "$BASELINE")
  if [ -z "$expected" ]; then
    echo "FAIL  $qid — no golden baseline entry (run with --update-baseline first)"
    FAIL=$((FAIL+1)); continue
  fi

  # Ordered comparison: both sides come from the same sort_by(-.score, .source, .page)
  # pipeline, so rank matters — a chunk dropping from #1 to #3 must fail (real drift),
  # not pass as it would with an order-insensitive set compare.
  if [ "$top" = "$expected" ]; then
    echo "PASS  $qid"; PASS=$((PASS+1))
  else
    echo "FAIL  $qid — top-$TOPK retrieval drifted"
    echo "  expected: $expected"
    echo "  got:      $top"
    FAIL=$((FAIL+1))
  fi
done < <(jq -r '.queries[].queryId' "$QUERIES")

if [ "$UPDATE_BASELINE" = true ]; then
  # Never write a partial baseline: a query that returned no citations would be
  # silently omitted and later assert runs would blame the operator. Fail loudly.
  [ "$FAIL" -eq 0 ] || fail "$FAIL query(ies) returned no citations — baseline NOT written (fix retrieval/auth first)"
  snap=$(cat "$DIR/../data/snapshots/.latest" 2>/dev/null || echo "unknown")
  model=$(jq -r '.embedding_model // "unknown"' "$DIR/../data/snapshots/$snap.meta.json" 2>/dev/null || echo "unknown")
  ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  jq --slurpfile b "$TMP_BASELINE" --arg snap "$snap" --arg model "$model" --arg ts "$ts" \
     '.baseline=$b[0] | .snapshot=$snap | .embeddingModel=$model | .capturedAt=$ts' \
     "$BASELINE" > "$BASELINE.2" && mv "$BASELINE.2" "$BASELINE"
  log "golden baseline updated → $BASELINE (snapshot=$snap, model=$model)"
  exit 0
fi

log "result: $PASS passed, $FAIL failed"
[ "$FAIL" -eq 0 ]
