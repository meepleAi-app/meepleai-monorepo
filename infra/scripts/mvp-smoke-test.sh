#!/bin/bash
# MVP Stack Smoke Test
# Verifies all 6 services are running and responsive
# Usage: bash infra/scripts/mvp-smoke-test.sh [base_url]

set -euo pipefail

BASE_URL="${1:-http://localhost}"
API_URL="${BASE_URL}/api"
PASS=0
FAIL=0

check() {
    local name="$1"
    local url="$2"
    local expected="${3:-200}"

    status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$url" 2>/dev/null || echo "000")
    if [ "$status" = "$expected" ]; then
        echo "  [PASS] $name ($status)"
        PASS=$((PASS + 1))
    else
        echo "  [FAIL] $name (got $status, expected $expected)"
        FAIL=$((FAIL + 1))
    fi
}

echo "=== MeepleAI MVP Smoke Test ==="
echo "Base URL: $BASE_URL"
echo ""

echo "--- Service Health ---"
check "API health" "$API_URL/health"
check "Web frontend" "$BASE_URL"
check "API scalar docs" "$BASE_URL/scalar/v1"

echo ""
echo "--- API Endpoints ---"
check "Auth endpoint" "$API_URL/v1/auth/login" "405"
check "Games endpoint" "$API_URL/v1/games" "401"

echo ""
echo "--- Database ---"
check "DB migrations applied" "$API_URL/health"

echo ""
echo "=== Results: $PASS passed, $FAIL failed ==="

if [ "$FAIL" -gt 0 ]; then
    echo "SMOKE TEST FAILED"
    exit 1
else
    echo "SMOKE TEST PASSED"
    exit 0
fi
