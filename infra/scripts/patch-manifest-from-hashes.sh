#!/usr/bin/env bash
# Rewrites the YAML manifests in
# apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/ by converting
# existing `pdf:` entries to `pdfBlobKey:` + `pdfSha256:` based on
# the .seed-hashes.tsv file produced by upload-seed-pdfs.sh.
#
# For slugs that exist in .seed-hashes.tsv but have no matching manifest entry,
# appends a skeleton entry at the bottom of staging.yml for the developer to
# fill in manually.
#
# Usage:
#   ./infra/scripts/patch-manifest-from-hashes.sh [HASHES_TSV]
#
# Requires: yq (mikefarah/yq v4+)
# Assumes slugs contain no shell-special characters (safe for the current
# controlled fileset).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

HASHES_TSV="${1:-${REPO_ROOT}/data/rulebook/.seed-hashes.tsv}"
MANIFEST_DIR="${REPO_ROOT}/apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests"
STAGING="${MANIFEST_DIR}/staging.yml"
DEV="${MANIFEST_DIR}/dev.yml"
PROD="${MANIFEST_DIR}/prod.yml"

if [[ ! -f "${HASHES_TSV}" ]]; then
  echo "ERROR: hashes file not found: ${HASHES_TSV}" >&2
  exit 1
fi

if ! command -v yq >/dev/null; then
  echo "ERROR: yq (mikefarah/yq v4+) is required" >&2
  exit 1
fi

# Build a slug→hash lookup from the TSV
declare -A HASH_FOR
while IFS=$'\t' read -r slug sha; do
  [[ -n "$slug" && -n "$sha" ]] || continue
  HASH_FOR["$slug"]="$sha"
done < "${HASHES_TSV}"

patch_file() {
  local yml="$1"
  [[ -f "$yml" ]] || { echo "SKIP (missing): $yml"; return; }

  echo "Patching: $yml"
  local matched=0

  for slug in "${!HASH_FOR[@]}"; do
    local hash="${HASH_FOR[$slug]}"
    local expected_pdf="${slug}_rulebook.pdf"
    local blob_key="rulebooks/v1/${expected_pdf}"

    # Find the entry by matching the legacy pdf field (just the filename)
    local idx
    idx="$(yq eval "(.catalog.games[] | select(.pdf == \"${expected_pdf}\")) | path | .[-1]" "$yml" 2>/dev/null || echo "")"

    # yq emits "null" (as a literal string) when select() has no match; treat as missing
    if [[ -n "$idx" && "$idx" != "null" && "$idx" =~ ^[0-9]+$ ]]; then
      yq eval -i "
        .catalog.games[${idx}].pdfBlobKey = \"${blob_key}\" |
        .catalog.games[${idx}].pdfSha256  = \"${hash}\" |
        .catalog.games[${idx}].pdfVersion = \"1.0\" |
        del(.catalog.games[${idx}].pdf)
      " "$yml"
      matched=$((matched + 1))
    fi
  done

  echo "  matched ${matched} entries"
}

append_missing() {
  local yml="$1"
  local appended=0

  for slug in "${!HASH_FOR[@]}"; do
    local hash="${HASH_FOR[$slug]}"
    local blob_key="rulebooks/v1/${slug}_rulebook.pdf"

    # Skip if already present via pdfBlobKey
    if yq eval "[.catalog.games[] | select(.pdfBlobKey == \"${blob_key}\")] | length" "$yml" | grep -q '^0$'; then
      # Not present — append skeleton
      yq eval -i "
        .catalog.games += [{
          \"title\": \"TODO: fill from manifest.json\",
          \"bggId\": 0,
          \"language\": \"en\",
          \"pdfBlobKey\": \"${blob_key}\",
          \"pdfSha256\":  \"${hash}\",
          \"pdfVersion\": \"1.0\"
        }]
      " "$yml"
      appended=$((appended + 1))
    fi
  done
  echo "  appended ${appended} skeleton entries to $(basename "$yml")"
}

patch_file "${STAGING}"
patch_file "${DEV}"
patch_file "${PROD}"

# Skeleton append is staging-only (developer completes metadata there first)
append_missing "${STAGING}"

echo ""
echo "Done. Review diffs with: git diff apps/api/src/Api/Infrastructure/Seeders/Catalog/Manifests/"
