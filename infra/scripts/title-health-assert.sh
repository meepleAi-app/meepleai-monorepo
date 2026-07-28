#!/usr/bin/env bash
# infra/scripts/title-health-assert.sh
# RAG extraction-quality regression guard (Epic #3338 WP3).
#
# Fetches the per-game title-health metric from GET /api/v1/admin/kb/title-health
# (TitleHealthMetric over each shared game's distinct text_chunks.Heading values) and
# asserts no game REGRESSED versus the committed baseline in
# fixtures/title-health-baseline.json:
#   - a band DOWNGRADE (green→yellow/red, yellow→red) FAILs — the acceptance guard
#     ("a previously-green English game's health must not drop after a chunking change");
#   - a plausible-fraction drop beyond `fractionRegressionTolerance` FAILs even within
#     the same band (catches within-band erosion before it tips a band);
#   - a baselined game ABSENT from the current corpus FAILs (its chunks/headings vanished);
#   - a game present now but NOT in the baseline is a NOTICE (newly-indexed), never a FAIL.
#
# The endpoint is admin-only, so SMOKE_EMAIL/SMOKE_PASSWORD (admin creds) are REQUIRED.
#
# Usage:
#   API_BASE_URL=http://localhost:8080 SMOKE_EMAIL=admin@… SMOKE_PASSWORD=… \
#     bash scripts/title-health-assert.sh                    # assert against baseline
#   … bash scripts/title-health-assert.sh --update-baseline  # capture baseline from current corpus
#
# Exit: 0 = no regression (or baseline updated / baseline empty); 1 = a game regressed / fetch failed.
set -uo pipefail

BASE_URL="${API_BASE_URL:-http://localhost:8080}"
EMAIL="${SMOKE_EMAIL:-${TEST_EMAIL:-}}"
PASSWORD="${SMOKE_PASSWORD:-${TEST_PASSWORD:-}}"
UPDATE_BASELINE=false

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"   # infra/
BASELINE="$DIR/fixtures/title-health-baseline.json"
ENDPOINT="/api/v1/admin/kb/title-health"

for arg in "$@"; do
  case "$arg" in
    --update-baseline) UPDATE_BASELINE=true ;;
    --base-url=*) BASE_URL="${arg#*=}" ;;
    *) echo "::error:: unknown arg: $arg" >&2; exit 2 ;;
  esac
done

log()  { echo "[title-health] $*" >&2; }
fail() { echo "::error:: $*" >&2; exit 1; }

command -v jq   >/dev/null || fail "jq is required"
command -v curl >/dev/null || fail "curl is required"
[ -f "$BASELINE" ] || fail "missing $BASELINE"

TOLERANCE=$(jq -r '.fractionRegressionTolerance // 0.05' "$BASELINE")
TOLERANCE=${TOLERANCE%$'\r'}   # native Windows jq.exe emits CRLF; strip the CR so --arg/comparisons are clean

COOKIE=$(mktemp); trap 'rm -f "$COOKIE" "${CURRENT:-}"' EXIT

# --- login (admin required: the endpoint is behind RequireAdminSessionFilter) ---
[ -n "$EMAIL" ] && [ -n "$PASSWORD" ] || fail "SMOKE_EMAIL/SMOKE_PASSWORD (admin creds) are required for the admin endpoint"
code=$(curl -s -o /dev/null -w '%{http_code}' -c "$COOKIE" -X POST "$BASE_URL/api/v1/auth/login" \
  -H 'Content-Type: application/json' \
  -d "$(jq -n --arg e "$EMAIL" --arg p "$PASSWORD" '{email:$e, password:$p}')") || true
[ "$code" = "200" ] || fail "login failed (HTTP ${code:-000})"
log "login OK ($EMAIL)"

# --- fetch current corpus title-health (array of GameTitleHealthDto) ---
CURRENT=$(mktemp)
code=$(curl -s -o "$CURRENT" -w '%{http_code}' -b "$COOKIE" "$BASE_URL$ENDPOINT") || true
[ "$code" = "200" ] || fail "GET $ENDPOINT failed (HTTP ${code:-000}) — admin session/deploy issue"
jq -e 'type == "array"' "$CURRENT" >/dev/null 2>&1 || fail "unexpected response (not a JSON array): $(head -c 200 "$CURRENT")"
COUNT=$(jq 'length' "$CURRENT")
log "fetched $COUNT game(s) from the corpus"

# --- capture mode ---
if [ "$UPDATE_BASELINE" = true ]; then
  [ "$COUNT" -gt 0 ] || fail "corpus returned 0 games — baseline NOT written (index cold / wrong env?)"
  ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  games=$(jq 'map({key: .gameTitle, value: {gameId, language, band, plausibleFraction, canonicalCoverage, distinctHeadings}}) | from_entries' "$CURRENT")
  jq --argjson g "$games" --arg ts "$ts" '.games=$g | .capturedAt=$ts' "$BASELINE" > "$BASELINE.2" && mv "$BASELINE.2" "$BASELINE"
  log "baseline updated → $BASELINE ($COUNT games, capturedAt=$ts)"
  exit 0
fi

# --- assert mode ---
BASELINE_N=$(jq '.games | length' "$BASELINE")
if [ "$BASELINE_N" -eq 0 ]; then
  # An empty baseline is a wired-but-dormant gate (the endpoint ships in this PR; the baseline is
  # captured on staging post-deploy via --update-baseline). Report a visible NOTICE, never green theatre.
  echo "::notice:: SKIP — title-health baseline is empty (pending --update-baseline capture on staging post-deploy)"
  log "result: baseline empty → gate dormant (no regression possible)"
  exit 0
fi

# band ordinal: red < yellow < green. A current ordinal below the baseline ordinal is a downgrade.
ORD='{"red":0,"yellow":1,"green":2}'

PASS=0; FAIL=0; NEW=0
while IFS= read -r title; do
  title=${title%$'\r'}
  base=$(jq -c --arg t "$title" '.games[$t]' "$BASELINE")
  cur=$(jq -c --arg t "$title" '.[] | select(.gameTitle==$t)' "$CURRENT" | head -1)

  if [ -z "$cur" ] || [ "$cur" = "null" ]; then
    echo "FAIL  $title — baselined game absent from the current corpus (chunks/headings vanished)"
    FAIL=$((FAIL+1)); continue
  fi

  verdict=$(jq -n --argjson base "$base" --argjson cur "$cur" --argjson ord "$ORD" --argjson tol "$TOLERANCE" '
    ($ord[$base.band]) as $bo | ($ord[$cur.band]) as $co
    | ($base.plausibleFraction - $cur.plausibleFraction) as $drop
    | if $co < $bo then "BAND \($base.band)→\($cur.band)"
      elif $drop > $tol then "FRACTION \($base.plausibleFraction)→\($cur.plausibleFraction) (−\($drop|.*10000|round/10000) > \($tol))"
      else "OK" end')
  verdict=${verdict//\"/}

  if [ "$verdict" = "OK" ]; then
    echo "PASS  $title — $(jq -r '.band' <<<"$cur") $(jq -r '.plausibleFraction' <<<"$cur")"
    PASS=$((PASS+1))
  else
    echo "FAIL  $title — regressed: $verdict"
    FAIL=$((FAIL+1))
  fi
done < <(jq -r '.games | keys[]' "$BASELINE")

# New (unbaselined) games: informational only.
while IFS= read -r title; do
  title=${title%$'\r'}
  echo "::notice:: NEW $title — not in baseline (newly-indexed; capture with --update-baseline to guard it)"
  NEW=$((NEW+1))
done < <(jq -r --slurpfile b "$BASELINE" '.[] | select((.gameTitle) as $t | ($b[0].games | has($t)) | not) | .gameTitle' "$CURRENT")

log "result: $PASS passed, $FAIL regressed, $NEW new (unguarded)"
[ "$FAIL" -eq 0 ]
