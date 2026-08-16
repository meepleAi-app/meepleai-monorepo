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
# Exit: 0 = all queries match baseline (or baseline updated); 1 = a query drifted / no citations;
#       3 = baseline stale (captured against a different snapshot — nothing to compare).
set -uo pipefail

BASE_URL="${API_BASE_URL:-http://localhost:8080}"
EMAIL="${SMOKE_EMAIL:-${TEST_EMAIL:-}}"
PASSWORD="${SMOKE_PASSWORD:-${TEST_PASSWORD:-}}"
UPDATE_BASELINE=false

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"   # infra/
QUERIES="$DIR/fixtures/rag-canonical-queries.json"
BASELINE="$DIR/fixtures/rag-golden-baseline.json"
# Stessa convenzione di snapshot-verify.sh / snapshot-restore.sh / seed-index-publish.sh:
# il percorso degli snapshot è configurabile, non dedotto dalla posizione dello script.
OUT_DIR="${SEED_INDEX_OUT_DIR:-$DIR/../data/snapshots}"

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

# --- guardia: la baseline appartiene allo snapshot in esecuzione? (#3645) ---
# La baseline fissa i chunk {source,page} di un corpus preciso. Su un corpus diverso il
# confronto non produce informazione: ogni query "drifta" e il gate riporta una regressione
# di retrieval che non è avvenuta. È successo dal 2026-07-20 al 2026-08-10 — tre settimane di
# rosso settimanale in cui una regressione autentica sarebbe passata inosservata.
#
# Confrontiamo solo quando entrambi i lati sono noti: eseguire contro un'API remota senza
# snapshot locale, o con una baseline anteriore all'introduzione del campo, resta legittimo.
BASELINE_SCHEMA=$(jq -r '.schemaVersion // 1' "$BASELINE")

if [ "$UPDATE_BASELINE" = false ]; then
  # --- baseline v1: chiave fisica, non più confrontabile (#3666) ---
  # La v1 fissava `{source,page}` dove `source` è `pdf_documents.Id`, un `Guid.NewGuid()`
  # rigenerato a ogni ingest: il re-bake lo cambiava e la baseline scadeva per costruzione,
  # non per una regressione. La v2 asserisce sul NOME del documento, che il re-bake conserva.
  # Questa migrazione si fa UNA volta, non a ogni bake.
  if [ "$BASELINE_SCHEMA" -lt 2 ]; then
    echo "::error:: baseline in formato v1 (id fisici) — va rigenerata una volta sola" >&2
    echo "  La v1 pinnava pdf_documents.Id, che cambia a ogni ingest: è la ragione per cui" >&2
    echo "  il gate è stato rosso 7 run su 8 fra il 2026-07-20 e il 2026-08-10 (#3666)." >&2
    echo "  Rigenera con --update-baseline: la v2 pinna il nome del documento e sopravvive" >&2
    echo "  ai re-bake — docs/for-developers/operations/rag-smoke-runbook.md" >&2
    exit 3
  fi

  # --- snapshot diverso: con la v2 è un'informazione, non un blocco (#3666) ---
  # Con la chiave stabile il confronto resta significativo attraverso un re-bake: un corpus
  # davvero diverso si manifesta come documenti diversi, cioè come il drift che il gate deve
  # rilevare. Restava exit 3 finché la chiave era fisica (#3645).
  baseline_snap=$(jq -r '.snapshot // empty' "$BASELINE")
  current_snap=$(cat "$OUT_DIR/.latest" 2>/dev/null || true)

  if [ -n "$baseline_snap" ] && [ -n "$current_snap" ] && [ "$baseline_snap" != "$current_snap" ]; then
    echo "::notice:: baseline catturata su un altro snapshot ($baseline_snap → $current_snap)." >&2
    echo "  Con la chiave v2 (nome documento) il confronto resta valido: un fallimento qui" >&2
    echo "  è drift del retrieval, non una baseline scaduta." >&2
  fi
fi

COOKIE=$(mktemp); trap 'rm -f "$COOKIE" "${TMP_BASELINE:-}" "${DOC_MAP:-}"' EXIT

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
# $2 = per-query language override; falls back to the global $LANG default.
fetch_top_chunks() {
  local query="$1"
  local lang="${2:-$LANG}"
  curl -sN -b "$COOKIE" -X POST "$BASE_URL$ENDPOINT" \
    -H 'Content-Type: application/json' \
    -d "$(jq -n --arg q "$query" --arg l "$lang" --argjson k "$TOPK" '{query:$q, language:$l, topK:$k}')" 2>/dev/null \
    | grep '^data: ' | sed 's/^data: //' \
    | jq -c 'select(.type==1) | .data.citations' 2>/dev/null | head -1 \
    | jq -c "if . == null then [] else (sort_by(-.score, .source, .page) | .[0:$TOPK] | map({source, page})) end" 2>/dev/null
}

# --- risoluzione id → documento (#3666) -------------------------------------------------
# `citations[].source` è `pdf_documents.Id` (StreamQaQueryHandler:396-399), generato con
# `Guid.NewGuid()` a ogni ingest: pinnarlo significava una baseline che scade a ogni re-bake.
# Una SOLA chiamata costruisce la mappa id → nome file, che il re-bake conserva perché deriva
# dal PDF sorgente e non dalla riga di database.
DOC_MAP=$(mktemp); echo '{}' > "$DOC_MAP"

build_document_map() {
  local body
  body=$(curl -s -b "$COOKIE" "$BASE_URL/api/v1/admin/pdfs?pageSize=500" 2>/dev/null) || true
  if ! echo "$body" | jq -e '.items' >/dev/null 2>&1; then
    fail "impossibile costruire la mappa documenti da GET /api/v1/admin/pdfs — serve un account admin (SMOKE_EMAIL). Senza mappa il confronto v2 non è possibile."
  fi
  echo "$body" | jq -c '[.items[] | {key: (.id|tostring), value: .fileName}] | from_entries' > "$DOC_MAP"
  log "mappa documenti: $(jq -r 'length' "$DOC_MAP") pdf"
}

# $1 = [{source,page}] → [{document,page}]. Un id non risolto resta `null` e viene
# segnalato dal chiamante: meglio un fallimento nominato che un confronto su una chiave vuota.
resolve_documents() {
  jq -c --slurpfile m "$DOC_MAP" 'map({document: ($m[0][.source] // null), page})' <<<"$1"
}

build_document_map

PASS=0; FAIL=0; SKIPPED=0
TMP_BASELINE=$(mktemp); echo '{}' > "$TMP_BASELINE"

while IFS= read -r qid; do
  query=$(jq -r --arg id "$qid" '.queries[] | select(.queryId==$id) | .query' "$QUERIES")
  # Per-query language: use the query's own `language`, else the top-level default ($LANG).
  qlang=$(jq -r --arg id "$qid" --arg def "$LANG" '.queries[] | select(.queryId==$id) | (.language // $def)' "$QUERIES")
  top=$(fetch_top_chunks "$query" "$qlang")

  if [ -z "$top" ] || [ "$top" = "[]" ]; then
    echo "FAIL  $qid — no citations (type=1 empty/absent: cold index, auth failure, or non-SSE error body). Check login + that the snapshot is loaded."
    FAIL=$((FAIL+1)); continue
  fi

  resolved=$(resolve_documents "$top")
  if echo "$resolved" | jq -e 'any(.[]; .document == null)' >/dev/null 2>&1; then
    # Un id citato che la mappa non conosce non è drift: è un documento sparito o una mappa
    # troncata. Va detto con il suo nome, non confuso con una regressione del retrieval.
    echo "FAIL  $qid — un id citato non compare in GET /api/v1/admin/pdfs (documento rimosso o mappa incompleta)"
    echo "  citazioni: $top"
    FAIL=$((FAIL+1)); continue
  fi

  if [ "$UPDATE_BASELINE" = true ]; then
    jq --arg id "$qid" --argjson v "$resolved" '.[$id]=$v' "$TMP_BASELINE" > "$TMP_BASELINE.2" \
      && mv "$TMP_BASELINE.2" "$TMP_BASELINE"
    log "captured $qid ($qlang) → $resolved"
    continue
  fi

  expected=$(jq -c --arg id "$qid" '.baseline[$id] // empty' "$BASELINE")
  if [ -z "$expected" ]; then
    # A newly-added query without a baseline is a SKIP, not a FAIL: adding queries
    # (e.g. the IT set, #3269) before the ops --update-baseline capture must NOT red
    # the weekly gate. Real drift / no-citations still FAIL below.
    echo "::notice:: SKIP $qid — no baseline yet (pending --update-baseline capture)"
    SKIPPED=$((SKIPPED+1)); continue
  fi

  # Confronto a due livelli (#3666).
  #
  # RIGIDO sui documenti, e ordinato: entrambi i lati vengono dallo stesso
  # sort_by(-.score, .source, .page), quindi il rango conta — un manuale che scende da #1 a #3
  # è drift vero e deve fallire, cosa che un confronto insiemistico lascerebbe passare.
  #
  # ADVISORY sulle pagine: sopravvivono al re-bake molto meno dei documenti, perché dipendono
  # dal chunking. Pinnarle è ciò che rendeva la baseline usa-e-getta. Una pagina diversa dentro
  # il manuale giusto viene segnalata e non fa fallire il gate: è il compromesso scelto in
  # #3666, esplicito e non ereditato.
  exp_docs=$(jq -c 'map(.document)' <<<"$expected")
  got_docs=$(jq -c 'map(.document)' <<<"$resolved")

  if [ "$exp_docs" = "$got_docs" ]; then
    exp_pages=$(jq -c 'map(.page)' <<<"$expected")
    got_pages=$(jq -c 'map(.page)' <<<"$resolved")
    if [ "$exp_pages" = "$got_pages" ]; then
      echo "PASS  $qid"
    else
      echo "::notice:: PASS  $qid — documenti attesi, pagine diverse (advisory): $exp_pages → $got_pages"
    fi
    PASS=$((PASS+1))
  else
    echo "FAIL  $qid — top-$TOPK retrieval drifted (documenti diversi)"
    echo "  expected: $exp_docs"
    echo "  got:      $got_docs"
    FAIL=$((FAIL+1))
  fi
done < <(jq -r '.queries[].queryId' "$QUERIES")

if [ "$UPDATE_BASELINE" = true ]; then
  # Never write a partial baseline: a query that returned no citations would be
  # silently omitted and later assert runs would blame the operator. Fail loudly.
  [ "$FAIL" -eq 0 ] || fail "$FAIL query(ies) returned no citations — baseline NOT written (fix retrieval/auth first)"
  snap=$(cat "$OUT_DIR/.latest" 2>/dev/null || echo "unknown")
  model=$(jq -r '.embedding_model // "unknown"' "$OUT_DIR/$snap.meta.json" 2>/dev/null || echo "unknown")
  ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  # schemaVersion 2 (#3666): le entry sono {document,page}, non più {source,page}. Il campo
  # `snapshot` resta — non serve più a invalidare la baseline, ma a dire su quale corpus è
  # stata catturata quando si legge un fallimento.
  jq --slurpfile b "$TMP_BASELINE" --arg snap "$snap" --arg model "$model" --arg ts "$ts" \
     '.schemaVersion=2 | .baseline=$b[0] | .snapshot=$snap | .embeddingModel=$model | .capturedAt=$ts' \
     "$BASELINE" > "$BASELINE.2" && mv "$BASELINE.2" "$BASELINE"
  log "golden baseline updated → $BASELINE (snapshot=$snap, model=$model)"
  exit 0
fi

log "result: $PASS passed, $FAIL failed, $SKIPPED skipped (pending baseline)"
# Exit non-zero ONLY on a real FAIL (drift / no-citations). SKIP (missing baseline) never fails the gate.
[ "$FAIL" -eq 0 ]
