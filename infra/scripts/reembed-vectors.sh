#!/usr/bin/env bash
# =============================================================================
# Vector Re-embedding Script (e5-large → mxbai-embed-large)
#
# Triggers a VectorReembedding batch job via the admin API.
# The API processes all VectorDocuments: deletes old embeddings,
# re-generates using the configured embedding model (mxbai-embed-large),
# and writes to pgvector.
#
# Prerequisites:
#   - API running with Ollama (mxbai-embed-large model pulled)
#   - Admin credentials configured
#   - All VectorDocuments have TextChunks in the database
#
# Usage:
#   ./reembed-vectors.sh [API_BASE_URL] [ADMIN_TOKEN]
#
# Examples:
#   ./reembed-vectors.sh http://localhost:8080 eyJhbGciOiJIUzI1NiIs...
#   ./reembed-vectors.sh  # Uses defaults from environment
# =============================================================================

set -euo pipefail

API_BASE="${1:-${API_BASE_URL:-http://localhost:8080}}"
TOKEN="${2:-${ADMIN_TOKEN:-}}"

if [ -z "$TOKEN" ]; then
    echo "ERROR: Admin token required."
    echo "Usage: $0 [API_BASE_URL] ADMIN_TOKEN"
    echo "  or set ADMIN_TOKEN environment variable"
    exit 1
fi

echo "=== MeepleAI Vector Re-embedding ==="
echo "API: $API_BASE"
echo ""

# 1. Check API health
echo "Checking API health..."
if ! curl -sf "$API_BASE/" > /dev/null 2>&1; then
    echo "ERROR: API not reachable at $API_BASE"
    exit 1
fi
echo "API is healthy."

# 2. Create re-embedding batch job
echo ""
echo "Creating VectorReembedding batch job..."
JOB_RESPONSE=$(curl -sf -X POST "$API_BASE/api/v1/admin/operations/batch-jobs" \
    -H "Content-Type: application/json" \
    -H "Cookie: meepleai_session=$TOKEN" \
    -d '{"type": "VectorReembedding", "parameters": "{}"}')

JOB_ID=$(echo "$JOB_RESPONSE" | grep -o '"jobId":"[^"]*"' | cut -d'"' -f4)

if [ -z "$JOB_ID" ]; then
    echo "ERROR: Failed to create batch job."
    echo "Response: $JOB_RESPONSE"
    exit 1
fi

echo "Batch job created: $JOB_ID"

# 3. Poll for completion
echo ""
echo "Monitoring progress..."
while true; do
    JOB_STATUS=$(curl -sf "$API_BASE/api/v1/admin/operations/batch-jobs/$JOB_ID" \
        -H "Cookie: meepleai_session=$TOKEN")

    STATUS=$(echo "$JOB_STATUS" | grep -o '"status":"[^"]*"' | cut -d'"' -f4)
    PROGRESS=$(echo "$JOB_STATUS" | grep -o '"progress":[0-9]*' | cut -d: -f2)

    echo "  Status: $STATUS | Progress: ${PROGRESS:-0}%"

    case "$STATUS" in
        Completed)
            SUMMARY=$(echo "$JOB_STATUS" | grep -o '"resultSummary":"[^"]*"' | cut -d'"' -f4)
            echo ""
            echo "=== Re-embedding Complete ==="
            echo "$SUMMARY"
            exit 0
            ;;
        Failed)
            ERROR=$(echo "$JOB_STATUS" | grep -o '"errorMessage":"[^"]*"' | cut -d'"' -f4)
            echo ""
            echo "=== Re-embedding FAILED ==="
            echo "Error: $ERROR"
            echo ""
            echo "Retry with: curl -X PUT '$API_BASE/api/v1/admin/operations/batch-jobs/$JOB_ID/retry' -H 'Cookie: meepleai_session=$TOKEN'"
            exit 1
            ;;
        Cancelled)
            echo ""
            echo "Job was cancelled."
            exit 1
            ;;
    esac

    sleep 10
done
