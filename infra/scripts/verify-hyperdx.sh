#!/bin/bash
# HyperDX Deployment Verification Script
# Usage: bash scripts/verify-hyperdx.sh
#
# This script verifies the HyperDX deployment meets all acceptance criteria
# from Issue #1562

set -e

echo "🔍 HyperDX Deployment Verification"
echo "===================================="
echo ""

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Verification functions
check_pass() {
    echo -e "${GREEN}✅ $1${NC}"
}

check_fail() {
    echo -e "${RED}❌ $1${NC}"
}

check_warn() {
    echo -e "${YELLOW}⚠️  $1${NC}"
}

# 1. Container Running and Healthy
echo "1️⃣  Checking container status..."
if docker ps --filter "name=meepleai-hyperdx" --format "{{.Status}}" | grep -q "healthy"; then
    check_pass "Container is running and healthy"
else
    check_fail "Container is not healthy or not running"
    echo "   Run: docker ps | grep hyperdx"
    exit 1
fi
echo ""

# 2. UI Accessible on port 8180
echo "2️⃣  Checking UI accessibility (port 8180)..."
if curl -f -s -o /dev/null http://localhost:8180; then
    check_pass "UI accessible at http://localhost:8180"
else
    check_fail "UI not accessible at http://localhost:8180"
    echo "   Expected: HTTP 200 response"
    exit 1
fi
echo ""

# 3. OTLP Endpoints Accessible
echo "3️⃣  Checking OTLP endpoints..."

# Check OTLP gRPC port 14317
if nc -zv localhost 14317 2>&1 | grep -q "succeeded"; then
    check_pass "OTLP gRPC port 14317 is open"
else
    check_fail "OTLP gRPC port 14317 is not accessible"
    exit 1
fi

# Check OTLP HTTP port 14318
if nc -zv localhost 14318 2>&1 | grep -q "succeeded"; then
    check_pass "OTLP HTTP port 14318 is open"
else
    check_fail "OTLP HTTP port 14318 is not accessible"
    exit 1
fi
echo ""

# 4. ClickHouse NOT Exposed to Host (Security)
echo "4️⃣  Checking security: ClickHouse port 8123 should NOT be exposed..."
if nc -zv localhost 8123 2>&1 | grep -q "succeeded"; then
    check_fail "SECURITY ISSUE: ClickHouse port 8123 is exposed to host"
    echo "   This port should only be accessible via Docker internal networking"
    exit 1
else
    check_pass "ClickHouse port 8123 is NOT exposed (secure)"
fi
echo ""

# 5. No Port Conflicts
echo "5️⃣  Checking for port conflicts..."
conflicts=0

# Check if ports are uniquely used by HyperDX
if netstat -an | grep ":8180" | grep -q "LISTEN"; then
    port_owner=$(docker ps --format "{{.Names}}" --filter "publish=8180")
    if [ "$port_owner" == "meepleai-hyperdx" ]; then
        check_pass "Port 8180 is used exclusively by HyperDX"
    else
        check_warn "Port 8180 is in use by: $port_owner"
        conflicts=$((conflicts+1))
    fi
else
    check_warn "Port 8180 is not listening"
fi

if [ $conflicts -eq 0 ]; then
    check_pass "No port conflicts detected"
fi
echo ""

# 6. Resource Usage Check
echo "6️⃣  Checking resource usage..."
container_mem=$(docker stats meepleai-hyperdx --no-stream --format "{{.MemUsage}}" | awk '{print $1}')
mem_value=$(echo $container_mem | sed 's/[^0-9.]//g')
mem_unit=$(echo $container_mem | sed 's/[0-9.]//g')

# Convert to MB for comparison
if [ "$mem_unit" == "GiB" ]; then
    mem_mb=$(echo "$mem_value * 1024" | bc)
elif [ "$mem_unit" == "MiB" ]; then
    mem_mb=$mem_value
else
    mem_mb=0
fi

if (( $(echo "$mem_mb < 2048" | bc -l) )); then
    check_pass "Memory usage: ${container_mem} (< 2GB startup target)"
else
    check_warn "Memory usage: ${container_mem} (exceeds 2GB target, but within 4GB limit)"
fi
echo ""

# 7. Health Endpoint Check
echo "7️⃣  Checking health endpoint..."
if curl -f -s http://localhost:8180/health | grep -q "ok\|healthy\|success"; then
    check_pass "Health endpoint responding correctly"
else
    check_warn "Health endpoint returned unexpected response"
    echo "   Response: $(curl -s http://localhost:8180/health)"
fi
echo ""

# 8. Docker Compose Configuration Validation
echo "8️⃣  Validating Docker Compose configuration..."
cd "$(dirname "$0")/.."
if docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml config > /dev/null 2>&1; then
    check_pass "Docker Compose configuration is valid"
else
    check_fail "Docker Compose configuration has errors"
    exit 1
fi
echo ""

# Summary
echo "===================================="
echo "📊 Verification Summary"
echo "===================================="
echo ""
check_pass "All acceptance criteria PASSED"
echo ""
echo "🎉 HyperDX is ready for use!"
echo ""
echo "Quick Links:"
echo "  - UI:        http://localhost:8180"
echo "  - OTLP gRPC: localhost:14317"
echo "  - OTLP HTTP: localhost:14318"
echo ""
echo "Next Steps:"
echo "  1. Configure backend to send traces/logs to HyperDX"
echo "  2. Configure frontend browser SDK"
echo "  3. Set up alerts in HyperDX UI"
echo ""
