#!/usr/bin/env bash
# Storage layout migration — Phase B backout (issue #1333).
#
# Wrapper around POST /api/v1/admin/storage/migration/reverse. Reverses an
# in-flight or completed storage migration identified by --migration-id:
# Sent rows get NewKey → LegacyKey + delete NewKey; Pending rows transition
# directly to Reverted state.
#
# Idempotent: a re-run on a fully-reverted migration produces 0 reversals,
# N skipped (rows in FailedPermanent/Reverted are left untouched).
#
# Usage:
#   MEEPLEAI_ADMIN_TOKEN=...                     # admin session cookie VALUE (required)
#   CF_ACCESS_CLIENT_ID=...                      # CF Access service token (optional)
#   CF_ACCESS_CLIENT_SECRET=...                  # CF Access service secret (optional)
#   ./scripts/reverse-storage-migration.sh \
#     --migration-id <UUID> \
#     [--api-url https://meepleai.app] \
#     [--dry-run]
#
# Auth: cookie-based (NOT Bearer). See enqueue-storage-migration.sh for the
# full token recovery procedure.
#
# CF Access: see enqueue-storage-migration.sh for details. Both env vars
# together; either alone is treated as a config error.
#
# Exit codes:
#   0 → success
#   1 → validation failure
#   2 → API error

set -euo pipefail

API_URL="${API_URL:-https://meepleai.app}"
DRY_RUN="false"
MIGRATION_ID=""

usage() {
    sed -n '2,18p' "$0" >&2
    exit 1
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --migration-id)
            [[ $# -lt 2 ]] && { echo "--migration-id needs a value" >&2; exit 1; }
            MIGRATION_ID="$2"; shift 2 ;;
        --api-url)
            [[ $# -lt 2 ]] && { echo "--api-url needs a value" >&2; exit 1; }
            API_URL="$2"; shift 2 ;;
        --dry-run) DRY_RUN="true"; shift ;;
        -h|--help) usage ;;
        *) echo "Unknown flag: $1" >&2; usage ;;
    esac
done

if [[ -z "$MIGRATION_ID" ]]; then
    echo "ERROR: --migration-id is required" >&2
    exit 1
fi
# Defense-in-depth: validate UUID format locally (see enqueue script comment).
if ! [[ "$MIGRATION_ID" =~ ^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$ ]]; then
    echo "ERROR: --migration-id must be a valid UUID" >&2
    exit 1
fi
if [[ -z "${MEEPLEAI_ADMIN_TOKEN:-}" ]]; then
    echo "ERROR: MEEPLEAI_ADMIN_TOKEN env var is required (session cookie value, not a Bearer token)" >&2
    exit 1
fi
SESSION_COOKIE_NAME="${MEEPLEAI_SESSION_COOKIE_NAME:-meepleai_session}"

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

echo "Storage migration reverse:"
echo "  api-url:      $API_URL"
echo "  migration-id: $MIGRATION_ID"
echo "  dry-run:      $DRY_RUN"
echo "  cf-access:    $([[ -n "$CF_ID" ]] && echo "service-token (id=${CF_ID:0:8}...)" || echo "none")"
echo ""

if [[ "$DRY_RUN" != "true" ]]; then
    read -r -p "This will reverse the migration. Type 'reverse' to confirm: " confirm
    if [[ "$confirm" != "reverse" ]]; then
        echo "Aborted." >&2
        exit 1
    fi
fi

payload=$(cat <<EOF
{
  "migrationId": "$MIGRATION_ID",
  "dryRun": $DRY_RUN
}
EOF
)

curl_args=(
    -sS
    -X POST "$API_URL/api/v1/admin/storage/migration/reverse"
    -H "Cookie: ${SESSION_COOKIE_NAME}=${MEEPLEAI_ADMIN_TOKEN}"
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
echo "Audit trail:"
echo "  psql -c \"SELECT status, COUNT(*) FROM storage_operation_outbox WHERE migration_id = '$MIGRATION_ID' GROUP BY status;\""
