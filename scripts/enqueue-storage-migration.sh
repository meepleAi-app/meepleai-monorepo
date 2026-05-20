#!/usr/bin/env bash
# Storage layout migration — Phase 1 enqueue (issue #1333).
#
# Wrapper around POST /api/v1/admin/storage/migration/enqueue. Enumerates
# S3 objects under --prefix and inserts storage_operation_outbox rows so
# the StorageOperationOutboxBackgroundService drainer can move them to the
# new categorized layout (issue #1314 PR 2).
#
# Idempotent: re-running with the same --migration-id produces 0 enqueued,
# N skipped (the UNIQUE LegacyKey constraint in the outbox dedups).
#
# Usage:
#   MEEPLEAI_ADMIN_TOKEN=...                     # admin bearer (required)
#   CF_ACCESS_CLIENT_ID=...                      # CF Access service token (optional)
#   CF_ACCESS_CLIENT_SECRET=...                  # CF Access service secret (optional)
#   ./scripts/enqueue-storage-migration.sh \
#     --bucket meepleai-uploads \
#     --migration-id $(uuidgen) \
#     [--prefix pdf_uploads/] \
#     [--category Pdf] \
#     [--api-url https://meepleai.app] \
#     [--dry-run]
#
# CF Access: when the API sits behind Cloudflare Access (staging.meepleai.app /
# meepleai.app), set CF_ACCESS_CLIENT_ID + CF_ACCESS_CLIENT_SECRET env vars to
# pass the service-token headers. Without them you'll get a Cloudflare login
# HTML page instead of a JSON response. Both vars together; either alone is
# treated as a config error.
#
# Exit codes:
#   0 → success (rows enqueued or dry-run completed)
#   1 → validation failure (missing flags, invalid category)
#   2 → API error (4xx/5xx from admin endpoint)

set -euo pipefail

API_URL="${API_URL:-https://meepleai.app}"
PREFIX="pdf_uploads/"
CATEGORY="Pdf"
DRY_RUN="false"
BUCKET=""
MIGRATION_ID=""

ALLOWED_CATEGORIES=("Pdf" "SessionPhoto" "GameImage" "VisionSnapshot" "GamebookPhoto" "PhotoBatch")

usage() {
    sed -n '2,22p' "$0" >&2
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --bucket)
            [[ $# -lt 2 ]] && { echo "--bucket needs a value" >&2; exit 1; }
            BUCKET="$2"; shift 2 ;;
        --migration-id)
            [[ $# -lt 2 ]] && { echo "--migration-id needs a value" >&2; exit 1; }
            MIGRATION_ID="$2"; shift 2 ;;
        --prefix)
            [[ $# -lt 2 ]] && { echo "--prefix needs a value" >&2; exit 1; }
            PREFIX="$2"; shift 2 ;;
        --category)
            [[ $# -lt 2 ]] && { echo "--category needs a value" >&2; exit 1; }
            CATEGORY="$2"; shift 2 ;;
        --api-url)
            [[ $# -lt 2 ]] && { echo "--api-url needs a value" >&2; exit 1; }
            API_URL="$2"; shift 2 ;;
        --dry-run) DRY_RUN="true"; shift ;;
        -h|--help) usage ;;
        *) echo "Unknown flag: $1" >&2; usage ;;
    esac
done

if [[ -z "$BUCKET" ]]; then
    echo "ERROR: --bucket is required" >&2
    exit 1
fi
if [[ -z "$MIGRATION_ID" ]]; then
    echo "ERROR: --migration-id is required (use \$(uuidgen))" >&2
    exit 1
fi
# Defense-in-depth: validate UUID format locally so a malformed value
# cannot break the JSON payload or shell-inject the audit log echo
# (the backend FluentValidation rejects Guid.Empty but accepts any
# parseable shape — script-side regex is stricter).
if ! [[ "$MIGRATION_ID" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
    echo "ERROR: --migration-id must be a valid UUID (use \$(uuidgen))" >&2
    exit 1
fi
if [[ -z "${MEEPLEAI_ADMIN_TOKEN:-}" ]]; then
    echo "ERROR: MEEPLEAI_ADMIN_TOKEN env var is required" >&2
    exit 1
fi

# CF Access service token: either both vars or neither (config error otherwise).
CF_ID="${CF_ACCESS_CLIENT_ID:-}"
CF_SECRET="${CF_ACCESS_CLIENT_SECRET:-}"
if [[ -n "$CF_ID" && -z "$CF_SECRET" ]]; then
    echo "ERROR: CF_ACCESS_CLIENT_ID set but CF_ACCESS_CLIENT_SECRET missing" >&2
    exit 1
fi
if [[ -z "$CF_ID" && -n "$CF_SECRET" ]]; then
    echo "ERROR: CF_ACCESS_CLIENT_SECRET set but CF_ACCESS_CLIENT_ID missing" >&2
    exit 1
fi

# Validate category against enum
valid_category="false"
for c in "${ALLOWED_CATEGORIES[@]}"; do
    [[ "$CATEGORY" == "$c" ]] && valid_category="true"
done
if [[ "$valid_category" != "true" ]]; then
    echo "ERROR: invalid --category '$CATEGORY'. Allowed: ${ALLOWED_CATEGORIES[*]}" >&2
    exit 1
fi

# Validate prefix ends with /
case "$PREFIX" in
    */) ;;
    *)
        echo "ERROR: --prefix must end with '/' (got '$PREFIX')" >&2
        exit 1
        ;;
esac

echo "Storage migration enqueue:"
echo "  api-url:      $API_URL"
echo "  bucket:       $BUCKET"
echo "  migration-id: $MIGRATION_ID"
echo "  prefix:       $PREFIX"
echo "  category:     $CATEGORY"
echo "  dry-run:      $DRY_RUN"
echo "  cf-access:    $([[ -n "$CF_ID" ]] && echo "service-token (id=${CF_ID:0:8}...)" || echo "none")"
echo ""

payload=$(cat <<EOF
{
  "migrationId": "$MIGRATION_ID",
  "legacyPrefix": "$PREFIX",
  "category": "$CATEGORY",
  "dryRun": $DRY_RUN
}
EOF
)

curl_args=(
    -sS
    -X POST "$API_URL/api/v1/admin/storage/migration/enqueue"
    -H "Authorization: Bearer $MEEPLEAI_ADMIN_TOKEN"
    -H "Content-Type: application/json"
    -d "$payload"
    -w "\nHTTP_STATUS:%{http_code}"
)
if [[ -n "$CF_ID" ]]; then
    curl_args+=(
        -H "CF-Access-Client-Id: $CF_ID"
        -H "CF-Access-Client-Secret: $CF_SECRET"
    )
fi
response=$(curl "${curl_args[@]}")

http_status=$(echo "$response" | sed -n 's/^HTTP_STATUS://p' | tail -1)
body=$(echo "$response" | sed '$d')

if [[ "$http_status" != "200" ]]; then
    echo "ERROR: API returned HTTP $http_status" >&2
    echo "$body" >&2
    exit 2
fi

echo "Response:"
echo "$body" | jq '.'
echo ""
echo "Next steps:"
echo "  1. Verify drainer is enabled: kubectl get pods -l app=api -o yaml | grep STORAGE_MIGRATION_ENABLED"
echo "  2. Monitor progress: psql -c \"SELECT status, COUNT(*) FROM storage_operation_outbox WHERE migration_id = '$MIGRATION_ID' GROUP BY status;\""
echo "  3. Grafana: $API_URL/grafana/d/storage-migration"
