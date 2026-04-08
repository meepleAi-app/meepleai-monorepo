#!/usr/bin/env bash
# Uploads rulebook PDFs to the meepleai-seeds bucket with SHA256 object metadata.
# Idempotent: skips uploads whose remote sha256 metadata already matches local.
#
# Usage:
#   ./infra/scripts/upload-seed-pdfs.sh [LOCAL_DIR]
#
# Requires:
#   - aws-cli >= 2.x
#   - SEED_BUCKET_* env vars OR infra/secrets/storage.secret sourced
#   - Write credentials (not the runtime readonly ones)
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
LOCAL_DIR="${1:-${REPO_ROOT}/data/rulebook}"
OUT_TSV="${LOCAL_DIR}/.seed-hashes.tsv"

# Load secrets if env not preset
if [[ -z "${SEED_BUCKET_NAME:-}" ]]; then
  if [[ -f "${REPO_ROOT}/infra/secrets/storage.secret" ]]; then
    # shellcheck disable=SC1091
    source "${REPO_ROOT}/infra/secrets/storage.secret"
  fi
fi

: "${SEED_BUCKET_NAME:?SEED_BUCKET_NAME is required}"
: "${SEED_BUCKET_ENDPOINT:?SEED_BUCKET_ENDPOINT is required}"
: "${SEED_BUCKET_ACCESS_KEY:?SEED_BUCKET_ACCESS_KEY is required}"
: "${SEED_BUCKET_SECRET_KEY:?SEED_BUCKET_SECRET_KEY is required}"

export AWS_ACCESS_KEY_ID="${SEED_BUCKET_ACCESS_KEY}"
export AWS_SECRET_ACCESS_KEY="${SEED_BUCKET_SECRET_KEY}"
export AWS_DEFAULT_REGION="${SEED_BUCKET_REGION:-auto}"

PREFIX="rulebooks/v1"
BUCKET="${SEED_BUCKET_NAME}"
ENDPOINT="${SEED_BUCKET_ENDPOINT}"

if [[ ! -d "${LOCAL_DIR}" ]]; then
  echo "ERROR: local directory not found: ${LOCAL_DIR}" >&2
  exit 1
fi

> "${OUT_TSV}"  # truncate

uploaded=0
skipped=0
for file in "${LOCAL_DIR}"/*_rulebook.pdf; do
  [[ -f "$file" ]] || continue
  base="$(basename "$file")"
  slug="${base%_rulebook.pdf}"

  local_sha="$(sha256sum "$file" | cut -d' ' -f1)"
  if [[ -z "$local_sha" ]]; then
    echo "ERROR: failed to compute sha256 for $file" >&2
    exit 1
  fi

  key="${PREFIX}/${base}"

  remote_sha="$(aws s3api head-object \
      --endpoint-url "${ENDPOINT}" \
      --bucket "${BUCKET}" \
      --key "${key}" \
      --query 'Metadata.sha256' \
      --output text 2>/dev/null || echo "")"

  if [[ "$remote_sha" == "$local_sha" ]]; then
    echo "  SKIP  ${slug} (sha matches)"
    skipped=$((skipped + 1))
  else
    aws s3 cp "$file" "s3://${BUCKET}/${key}" \
      --endpoint-url "${ENDPOINT}" \
      --metadata "sha256=${local_sha}" \
      --no-progress
    echo "  UP    ${slug} (${local_sha:0:12}...)"
    uploaded=$((uploaded + 1))
  fi

  printf "%s\t%s\n" "${slug}" "${local_sha}" >> "${OUT_TSV}"
done

echo ""
echo "Summary: ${uploaded} uploaded, ${skipped} already up to date"
echo "Wrote ${OUT_TSV}"
