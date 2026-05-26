#!/usr/bin/env bash
# 30-kb.sh — Upload PDFs for SP4 KB docs (rename source PDFs to mock filenames).
#
# Each KB doc in data.json has:
#   uploadName: target filename (e.g. "azul-regole-ita.pdf")
#   sourcePdf:  source file in data/rulebook/ (e.g. "azul_rulebook.pdf")
#   gameSlug:   resolves to gameId via state file
#
# Idempotent: API returns existingKb on duplicate (filename + gameId match).
# Polls /admin/pdfs for processing state until Ready/Failed/timeout.

set -euo pipefail
source "$(dirname "$(readlink -f "$0")")/lib/common.sh"

banner "30 — KB PDFs (6 SP4)"
ADMIN_JAR=$(cookie_jar_for "admin")
[[ -s "$ADMIN_JAR" ]] || { admin_login; }

POLL_MAX_SEC="${POLL_MAX_SEC:-1200}"   # 20 min total per PDF (large PDF + indexing)
POLL_INTERVAL_SEC="${POLL_INTERVAL_SEC:-15}"

total=0; uploaded=0; existing=0; ready=0; failed=0

while IFS= read -r kb; do
  slug=$(jq -r '.slug'       <<< "$kb")
  upload_name=$(jq -r '.uploadName' <<< "$kb")
  source_pdf=$(jq -r '.sourcePdf'  <<< "$kb")
  game_slug=$(jq -r '.gameSlug'   <<< "$kb")
  total=$((total + 1))

  game_id=$(state_get "games" "$game_slug")
  [[ -n "$game_id" ]] || { warn "[$slug] no game_id for $game_slug (run 20-games.sh first)"; failed=$((failed + 1)); continue; }

  source_path="$RULEBOOK_DIR/$source_pdf"
  [[ -f "$source_path" ]] || { warn "[$slug] missing source $source_path"; failed=$((failed + 1)); continue; }

  size_bytes=$(stat -c %s "$source_path" 2>/dev/null || stat -f %z "$source_path")
  log "[$slug] uploading $upload_name (~$((size_bytes/1024/1024)) MB, source=$source_pdf) for game=$game_slug"

  # curl on mingw64 (Git Bash Windows) can't read POSIX paths like /d/... — convert to D:\...
  curl_path="$source_path"
  if command -v cygpath >/dev/null 2>&1; then
    curl_path=$(cygpath -w "$source_path")
  fi

  # POST /ingest/pdf (multipart). curl -F filename= override sends the mock filename
  # without needing a tmpdir+cp (avoids cross-platform path issues on Git Bash Windows).
  up=$(curl -sS -X POST "$API_BASE/ingest/pdf" \
    -b "$ADMIN_JAR" -c "$ADMIN_JAR" \
    -F "file=@$curl_path;filename=$upload_name;type=application/pdf" \
    -F "gameId=$game_id" \
    -w "\n%{http_code}")
  up_body=$(echo "$up" | sed '$d')
  up_code=$(echo "$up"  | tail -n1)

  if ! http_check "200|201" "$up_code" "$up_body" "upload $slug"; then
    failed=$((failed + 1))
    continue
  fi

  # Response may be { documentId } (new) or { existingKb: { pdfDocumentId } } (duplicate)
  pdf_id=$(echo "$up_body" | jq -r '.documentId // .existingKb.pdfDocumentId // empty')
  if [[ -z "$pdf_id" ]]; then
    # Fallback parse if BE returns nested under .data or similar
    pdf_id=$(echo "$up_body" | jq -r '.. | objects | (.documentId // .pdfDocumentId)? // empty' 2>/dev/null | head -1)
  fi
  [[ -n "$pdf_id" && "$pdf_id" != "null" ]] || { warn "[$slug] no pdfId in response: $up_body"; failed=$((failed + 1)); continue; }

  # Was it existing?
  if echo "$up_body" | jq -e '.existingKb' >/dev/null 2>&1; then
    existing=$((existing + 1))
    skip "$slug already exists ($pdf_id)"
  else
    uploaded=$((uploaded + 1))
    ok "Uploaded $slug → $pdf_id"
  fi

  state_set "kbDocs" "$slug" "$pdf_id"

  # Poll until Ready or Failed
  poll_state() {
    local resp=$(curl_get "/admin/pdfs" "$ADMIN_JAR")
    local body=$(echo "$resp" | sed '$d')
    local code=$(echo "$resp" | tail -n1)
    [[ "$code" == "200" ]] || return 1
    local state=$(echo "$body" | jq -r --arg id "$pdf_id" '.items[]? | select(.id==$id) | .processingState' | head -1)
    case "$state" in
      Ready) return 0 ;;
      Failed) return 0 ;;   # also done (treat as terminal)
      *) return 1 ;;
    esac
  }

  if poll_until poll_state "$POLL_MAX_SEC" "$POLL_INTERVAL_SEC" "indexing $slug"; then
    # Get final state
    final_resp=$(curl_get "/admin/pdfs" "$ADMIN_JAR")
    final_state=$(echo "$final_resp" | sed '$d' | jq -r --arg id "$pdf_id" '.items[]? | select(.id==$id) | .processingState' | head -1)
    case "$final_state" in
      Ready)
        ready=$((ready + 1))
        ok "$slug indexed (Ready)"
        ;;
      Failed)
        warn "$slug processing Failed (likely too large, no OCR text, etc.) — keeping PDF row for mock state visualization"
        ;;
    esac
  else
    warn "$slug polling timed out — current state unknown, leaving PDF row"
  fi
done < <(data_get_compact '.kbDocs[]')

log "Summary: total=$total  uploaded=$uploaded  existing=$existing  ready=$ready  failed_upload=$failed"
[[ $failed -gt 0 ]] && fail "30-kb completed with $failed upload failures"
ok "30-kb complete"
