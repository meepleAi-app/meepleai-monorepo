#!/bin/bash
# HyperDX Integration Testing Script
# Issue #1565: Verify backend telemetry ingestion
#
# Usage: bash tools/verify-hyperdx-ingestion.sh

set -e

API_BASE="${API_BASE:-http://localhost:8080}"
HYPERDX_UI="${HYPERDX_UI:-http://localhost:8180}"

echo "=========================================="
echo "HyperDX Integration Test Suite"
echo "=========================================="
echo ""
echo "API Base: $API_BASE"
echo "HyperDX UI: $HYPERDX_UI"
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test Results
PASS=0
FAIL=0

test_case() {
    local name="$1"
    echo ""
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "TEST: $name"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
}

pass() {
    echo -e "${GREEN}✓ PASS${NC}: $1"
    ((PASS++))
}

fail() {
    echo -e "${RED}✗ FAIL${NC}: $1"
    ((FAIL++))
}

warn() {
    echo -e "${YELLOW}⚠ WARN${NC}: $1"
}

# ==========================================
# Test 1: Log Ingestion
# ==========================================
test_case "1. Error Log Ingestion"

echo "Generating test error log..."
RESPONSE=$(curl -s -X POST "$API_BASE/api/v1/test/error")
CORRELATION_ID=$(echo "$RESPONSE" | jq -r '.correlationId')

echo "Response: $RESPONSE"
echo "Correlation ID: $CORRELATION_ID"

if [ -n "$CORRELATION_ID" ] && [ "$CORRELATION_ID" != "null" ]; then
    pass "Test error endpoint responded successfully"
    echo ""
    echo "Manual Verification Steps:"
    echo "1. Open HyperDX UI: $HYPERDX_UI"
    echo "2. Navigate to 'Logs' tab"
    echo "3. Search: service.name:meepleai-api AND level:error"
    echo "4. Find log with correlation_id: $CORRELATION_ID"
    echo "5. Verify log structure:"
    echo "   - timestamp: present"
    echo "   - level: error"
    echo "   - message: contains 'Test error generated'"
    echo "   - service.name: meepleai-api"
    echo "   - correlation_id: $CORRELATION_ID"
    echo ""
    warn "Wait 10 seconds for log ingestion before checking HyperDX UI"
else
    fail "Test error endpoint failed or returned null correlation ID"
fi

# ==========================================
# Test 2: Trace Generation
# ==========================================
test_case "2. Distributed Trace Generation"

echo "Generating test trace..."
RESPONSE=$(curl -s -X GET "$API_BASE/api/v1/test/trace")
TRACE_ID=$(echo "$RESPONSE" | jq -r '.traceId')

echo "Response: $RESPONSE"
echo "Trace ID: $TRACE_ID"

if [ -n "$TRACE_ID" ] && [ "$TRACE_ID" != "null" ] && [ "$TRACE_ID" != "no-trace" ]; then
    pass "Test trace endpoint responded successfully"
    echo ""
    echo "Manual Verification Steps:"
    echo "1. Open HyperDX UI: $HYPERDX_UI"
    echo "2. Navigate to 'Traces' tab"
    echo "3. Search: trace_id:$TRACE_ID"
    echo "4. Verify trace structure:"
    echo "   - Parent span: TelemetryTest.GenerateTrace"
    echo "   - Child span: TelemetryTest.ChildOperation"
    echo "   - Service name: meepleai-api"
    echo ""
    warn "Wait 10 seconds for trace ingestion before checking HyperDX UI"
else
    fail "Test trace endpoint failed or returned invalid trace ID"
fi

# ==========================================
# Test 3: Log-Trace Correlation
# ==========================================
test_case "3. Log-Trace Correlation"

echo "Testing log-trace correlation..."
echo ""
echo "Manual Verification Steps:"
echo "1. Open HyperDX UI: $HYPERDX_UI"
echo "2. Navigate to 'Logs' tab"
echo "3. Find any log entry from previous tests"
echo "4. Click 'View Trace' button (if available)"
echo "5. Verify it auto-opens the correlated distributed trace"
echo "6. Verify trace ID matches between log and trace view"
echo ""
warn "This test requires manual verification in HyperDX UI"

# ==========================================
# Test 4: Sensitive Data Redaction
# ==========================================
test_case "4. Sensitive Data Redaction"

echo "Testing sensitive data redaction..."
RESPONSE=$(curl -s -X POST "$API_BASE/api/v1/test/sensitive")
TRACE_ID=$(echo "$RESPONSE" | jq -r '.traceId')

echo "Response: $RESPONSE"
echo "Trace ID for sensitive data test: $TRACE_ID"

if [ -n "$TRACE_ID" ] && [ "$TRACE_ID" != "null" ]; then
    pass "Sensitive data test endpoint responded successfully"
    echo ""
    echo "Manual Verification Steps:"
    echo "1. Open HyperDX UI: $HYPERDX_UI"
    echo "2. Navigate to 'Traces' tab"
    echo "3. Search: trace_id:$TRACE_ID"
    echo "4. Inspect span attributes"
    echo "5. Verify sensitive fields are redacted:"
    echo "   - test.password: should show [REDACTED] or be absent"
    echo "   - test.apiKey: should show [REDACTED] or be absent"
    echo "   - test.token: should show [REDACTED] or be absent"
    echo "   - test.username: should be VISIBLE (not sensitive)"
    echo ""
    warn "Wait 10 seconds for trace ingestion before checking HyperDX UI"
else
    fail "Sensitive data test endpoint failed"
fi

# ==========================================
# Test 5: Bulk Telemetry Generation
# ==========================================
test_case "5. Performance Test - Bulk Telemetry"

echo "Generating 100 logs and traces..."
START_TIME=$(date +%s%3N)
RESPONSE=$(curl -s -X POST "$API_BASE/api/v1/test/bulk?count=100")
END_TIME=$(date +%s%3N)
DURATION=$((END_TIME - START_TIME))

LOGS_GENERATED=$(echo "$RESPONSE" | jq -r '.logsGenerated')
TRACES_GENERATED=$(echo "$RESPONSE" | jq -r '.tracesGenerated')

echo "Response: $RESPONSE"
echo "Client-side duration: ${DURATION}ms"
echo "Logs generated: $LOGS_GENERATED"
echo "Traces generated: $TRACES_GENERATED"

if [ "$LOGS_GENERATED" == "100" ] && [ "$TRACES_GENERATED" == "100" ]; then
    pass "Bulk telemetry generation successful (100 logs + 100 traces)"

    if [ "$DURATION" -lt 5000 ]; then
        pass "Performance: Completed in ${DURATION}ms (< 5s threshold)"
    else
        warn "Performance: Took ${DURATION}ms (> 5s, may need optimization)"
    fi

    echo ""
    echo "Manual Verification Steps:"
    echo "1. Open HyperDX UI: $HYPERDX_UI"
    echo "2. Navigate to 'Logs' tab"
    echo "3. Search: test.type:bulk"
    echo "4. Verify all 100 logs appear within 10 seconds"
    echo "5. Navigate to 'Traces' tab"
    echo "6. Search: test.type:bulk"
    echo "7. Verify all 100 traces appear"
    echo "8. Test search performance:"
    echo "   - Perform search query"
    echo "   - Verify response time < 1 second"
    echo "9. Check HyperDX resource usage:"
    echo "   - Run: docker stats meepleai-hyperdx --no-stream"
    echo "   - Verify RAM usage < 4GB"
    echo ""
    warn "Wait 10 seconds for full ingestion before checking HyperDX UI"
else
    fail "Bulk telemetry generation failed (expected 100/100, got $LOGS_GENERATED/$TRACES_GENERATED)"
fi

# ==========================================
# Test 6: No OTel Errors in API Logs
# ==========================================
test_case "6. OpenTelemetry Exporter Health"

echo "Checking API logs for OTel errors..."
OTEL_ERRORS=$(docker logs meepleai-api 2>&1 | grep -i "otel.*error" || echo "")

if [ -z "$OTEL_ERRORS" ]; then
    pass "No OpenTelemetry exporter errors detected in API logs"
else
    fail "OpenTelemetry errors found in API logs:"
    echo "$OTEL_ERRORS"
fi

# ==========================================
# Test 7: HyperDX Resource Usage
# ==========================================
test_case "7. HyperDX Resource Usage"

echo "Checking HyperDX container resource usage..."
STATS=$(docker stats meepleai-hyperdx --no-stream --format "{{.MemUsage}}")

echo "Current memory usage: $STATS"

# Extract memory value (e.g., "1.5GiB / 4GiB" -> "1.5")
MEM_USED=$(echo "$STATS" | awk '{print $1}' | sed 's/GiB//g')

if (( $(echo "$MEM_USED < 4.0" | bc -l) )); then
    pass "HyperDX memory usage under 4GB limit ($STATS)"
else
    warn "HyperDX memory usage high: $STATS"
fi

# ==========================================
# Summary
# ==========================================
echo ""
echo "=========================================="
echo "Test Summary"
echo "=========================================="
echo -e "${GREEN}Passed: $PASS${NC}"
echo -e "${RED}Failed: $FAIL${NC}"
echo ""

if [ $FAIL -eq 0 ]; then
    echo -e "${GREEN}✓ All automated tests passed!${NC}"
    echo ""
    echo "Next Steps:"
    echo "1. Complete manual verification in HyperDX UI: $HYPERDX_UI"
    echo "2. Verify all acceptance criteria from Issue #1565"
    echo "3. Run k6 performance test: k6 run tests/k6/hyperdx-ingestion-test.js"
    exit 0
else
    echo -e "${RED}✗ Some tests failed. Review errors above.${NC}"
    exit 1
fi
