#!/usr/bin/env bash
# Issue #2055 Phase G AC-G5 — refresh Wikidata + Commons fixture JSONs from live API.
#
# Purpose
# -------
# Pulls fresh response snapshots from the upstream Wikidata SPARQL endpoint and
# the Wikimedia Commons imageinfo API to detect contract drift before it hits
# production (Crispin C-002 concern).
#
# When to run
# -----------
#   - Quarterly (calendar reminder)
#   - Before each major release (release-manager checklist)
#   - After any upstream incident affecting Wikidata or Commons
#
# Review workflow
# ---------------
# 1. Run this script from the repository root.
# 2. Run `git diff apps/api/tests/Api.Tests/Fixtures/Wikidata/`.
# 3. If the diff is shape-preserving (whitespace, new optional fields, attribution
#    text rewording): commit as-is. The contract tests will pass.
# 4. If the diff renames a required field or changes a required path: STOP. This
#    is upstream contract drift. Open an issue against the consumer
#    (WikidataCatalogProvider / WikimediaCommonsClient), fix the parser, then
#    refresh + commit together.
# 5. NEVER bypass a failing test by editing a fixture to match a broken parser —
#    that defeats the entire AC-G5 drift-detection purpose.
#
# Limitations
# -----------
# - Only refreshes the SPARQL P18 success case + Commons imageinfo for Catan
#   (Q1057010). The synthetic fixtures (P18-no-image, P18-503, malformed,
#   missing-license, cc-by-nc) are hand-crafted adversarial scenarios that
#   live API calls cannot reproduce; review them manually.
# - The Commons imageinfo refresh requires the P18 filename, which changes
#   over time. Update the FILENAME variable below after running the SPARQL
#   refresh.

set -euo pipefail

# Resolve repo root from this script's location (works regardless of cwd).
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../../.." && pwd)"
FIXTURE_DIR="${REPO_ROOT}/apps/api/tests/Api.Tests/Fixtures/Wikidata"

mkdir -p "${FIXTURE_DIR}"

# Mandatory custom UA per the Wikimedia API etiquette — anonymous UAs are
# rate-limited more aggressively and risk a permanent block.
UA="MeepleAIFixtureRefresh/1.0 (https://github.com/meepleAi-app/meepleai-monorepo; contact via repo issues)"

# Catan QID = Q1057010 (same id pinned by FetchCoverImage_SuccessfulP18 test).
QID="Q1057010"

echo "[1/2] Refreshing P18-catan-q1234.json (live SPARQL query for ${QID})..."
SPARQL_QUERY="SELECT ?image WHERE { wd:${QID} wdt:P18 ?image }"
curl --fail --silent --show-error \
  --user-agent "${UA}" \
  --get \
  --data-urlencode "query=${SPARQL_QUERY}" \
  --data "format=json" \
  "https://query.wikidata.org/sparql" \
  > "${FIXTURE_DIR}/P18-catan-q1234.json"

echo "[1/2] OK — wrote $(wc -c < "${FIXTURE_DIR}/P18-catan-q1234.json") bytes."
echo ""

# Read the filename out of the SPARQL response so the Commons refresh below
# stays in sync with the current P18 claim. Parses
#   results.bindings[0].image.value → strips the Special:FilePath/ prefix.
FILENAME="$(
  python3 -c "
import json, sys
with open('${FIXTURE_DIR}/P18-catan-q1234.json') as f:
    doc = json.load(f)
bindings = doc.get('results', {}).get('bindings', [])
if not bindings:
    sys.exit('no P18 binding in SPARQL response')
iri = bindings[0]['image']['value']
prefix = 'http://commons.wikimedia.org/wiki/Special:FilePath/'
if not iri.startswith(prefix):
    sys.exit('unexpected IRI shape: ' + iri)
print(iri[len(prefix):])
"
)"

echo "[2/2] Refreshing commons-license-pd.json (live imageinfo for ${FILENAME})..."
echo "      (review the LicenseShortName carefully — if upstream renamed it,"
echo "       the contract tests will catch it but adversarial fixtures may need rework.)"

curl --fail --silent --show-error \
  --user-agent "${UA}" \
  --get \
  --data-urlencode "titles=File:${FILENAME}" \
  --data "action=query" \
  --data "prop=imageinfo" \
  --data "iiprop=extmetadata" \
  --data "format=json" \
  "https://commons.wikimedia.org/w/api.php" \
  > "${FIXTURE_DIR}/commons-license-pd.json"

echo "[2/2] OK — wrote $(wc -c < "${FIXTURE_DIR}/commons-license-pd.json") bytes."
echo ""
echo "Done. Next steps:"
echo "  git diff ${FIXTURE_DIR}/"
echo "  # Review the shape diff. Commit if shape-preserving; investigate if not."
echo "  # The adversarial fixtures (P18-no-image, P18-503, *malformed*, *missing*,"
echo "  # cc-by-nc) are NOT refreshed — they are hand-crafted scenarios."
