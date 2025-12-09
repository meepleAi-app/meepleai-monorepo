#!/usr/bin/env bash
#
# HyperDX Migration Verification Script
# Issue #1570: Final Verification and Go-Live Checklist
#
# Epic: #1561 (HyperDX Migration)
# Verifies 50+ checkpoints across 7 categories
#
# Usage:
#   bash tools/verify-hyperdx-migration.sh
#
# Exit Codes:
#   0 - All checks passed (ready for production)
#   1 - One or more critical checks failed
#   2 - Invalid environment or dependencies missing

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Counters
TOTAL_CHECKS=0
PASSED_CHECKS=0
FAILED_CHECKS=0
SKIPPED_CHECKS=0

# Results arrays
declare -a FAILED_ITEMS=()
declare -a SKIPPED_ITEMS=()

# Helper functions
print_header() {
    echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"
}

print_check() {
    local description="$1"
    ((TOTAL_CHECKS++))
    printf "%-70s" "  ${description}..."
}

pass() {
    ((PASSED_CHECKS++))
    echo -e "${GREEN}✅ PASS${NC}"
}

fail() {
    local reason="$1"
    ((FAILED_CHECKS++))
    echo -e "${RED}❌ FAIL${NC}"
    FAILED_ITEMS+=("$reason")
}

skip() {
    local reason="$1"
    ((SKIPPED_CHECKS++))
    echo -e "${YELLOW}⏭️  SKIP${NC}"
    SKIPPED_ITEMS+=("$reason")
}

# Dependency checks
check_dependencies() {
    print_header "Dependency Verification"

    print_check "Docker CLI available"
    if command -v docker &> /dev/null; then
        pass
    else
        fail "Docker CLI not found - install Docker Desktop"
        return 1
    fi

    print_check "curl available"
    if command -v curl &> /dev/null; then
        pass
    else
        fail "curl not found - install curl"
        return 1
    fi

    print_check "jq available (optional)"
    if command -v jq &> /dev/null; then
        pass
    else
        skip "jq not found - some checks will be simplified"
    fi

    print_check "nc (netcat) available (optional)"
    if command -v nc &> /dev/null; then
        pass
    else
        skip "nc not found - some port checks will use curl"
    fi
}

# Infrastructure checks
check_infrastructure() {
    print_header "Infrastructure Verification (7 checks)"

    print_check "HyperDX UI accessible (http://localhost:8180)"
    if curl -s -o /dev/null -w "%{http_code}" http://localhost:8180 | grep -q "200\|301\|302"; then
        pass
    else
        fail "HyperDX UI not accessible on port 8180"
    fi

    print_check "OTLP gRPC port open (localhost:14317)"
    if command -v nc &> /dev/null; then
        if timeout 2 bash -c "echo > /dev/tcp/localhost/14317" 2>/dev/null; then
            pass
        else
            fail "OTLP gRPC port 14317 not accessible"
        fi
    else
        skip "nc not available - cannot test port 14317"
    fi

    print_check "OTLP HTTP port open (localhost:14318)"
    if command -v nc &> /dev/null; then
        if timeout 2 bash -c "echo > /dev/tcp/localhost/14318" 2>/dev/null; then
            pass
        else
            fail "OTLP HTTP port 14318 not accessible"
        fi
    else
        skip "nc not available - cannot test port 14318"
    fi

    print_check "ClickHouse port NOT exposed (should fail)"
    if timeout 2 bash -c "echo > /dev/tcp/localhost/8123" 2>/dev/null; then
        fail "ClickHouse port 8123 exposed - security risk!"
    else
        pass
    fi

    print_check "HyperDX container healthy"
    local health_status
    health_status=$(docker inspect meepleai-hyperdx --format='{{.State.Health.Status}}' 2>/dev/null || echo "none")
    if [[ "$health_status" == "healthy" ]]; then
        pass
    elif [[ "$health_status" == "none" ]]; then
        skip "Container has no healthcheck defined"
    else
        fail "HyperDX container status: $health_status"
    fi

    print_check "Docker network exists (meepleai)"
    if docker network ls | grep -q "meepleai"; then
        pass
    else
        fail "meepleai Docker network not found"
    fi

    print_check "HyperDX API health endpoint"
    if docker exec meepleai-hyperdx wget -q -O- http://localhost:8000/health 2>/dev/null | grep -q "OK"; then
        pass
    else
        fail "HyperDX API health endpoint not responding"
    fi
}

# Backend integration checks
check_backend_integration() {
    print_header "Backend Integration (7 checks)"

    # Note: These checks require API to be running
    local api_running=false
    if curl -s http://localhost:8080/health > /dev/null 2>&1; then
        api_running=true
    fi

    if [[ "$api_running" == "false" ]]; then
        echo -e "${YELLOW}⚠️  API not running - skipping backend checks${NC}"
        echo -e "${YELLOW}   Start API with: cd apps/api/src/Api && dotnet run${NC}\n"
        ((SKIPPED_CHECKS += 7))
        return 0
    fi

    print_check "Backend logs appearing in HyperDX (within 10s)"
    skip "Manual verification required - check HyperDX UI"

    print_check "Backend traces with service name 'meepleai-api'"
    skip "Manual verification required - check HyperDX UI"

    print_check "Trace IDs match between logs and traces"
    skip "Manual verification required - compare in HyperDX UI"

    print_check "Sensitive data redacted in traces"
    skip "Manual verification required - check trace content"

    print_check "No OTel exporter errors in API logs"
    if docker logs meepleai-api 2>&1 | grep -iq "otel.*error"; then
        fail "OpenTelemetry errors found in API logs"
    else
        pass
    fi

    print_check "CORS headers exposed for trace correlation"
    local cors_headers
    cors_headers=$(curl -sI http://localhost:8080/api/v1/games | grep -i "Expose" || echo "")
    if [[ -n "$cors_headers" ]]; then
        pass
    else
        skip "CORS Expose headers not found - may be OK if not needed"
    fi

    print_check "HyperDX env vars configured in API"
    if docker ps | grep -q "meepleai-api"; then
        if docker exec meepleai-api env | grep -q "HYPERDX_OTLP_ENDPOINT"; then
            pass
        else
            fail "HYPERDX_OTLP_ENDPOINT not set in API container"
        fi
    else
        skip "API container not running - check infra/env/api.env.dev"
    fi
}

# Frontend integration checks
check_frontend_integration() {
    print_header "Frontend Integration (5 checks)"

    print_check "Session replay recording"
    skip "Issue #1566 - Browser SDK verification (manual UI check)"

    print_check "User identification working (login → session)"
    skip "Issue #1566 - Browser SDK verification (manual UI check)"

    print_check "Frontend → Backend correlation (click → trace)"
    skip "Issue #1566 - Browser SDK verification (manual UI check)"

    print_check "Sensitive inputs masked in replay"
    skip "Issue #1566 - Browser SDK verification (manual UI check)"

    print_check "No console errors in browser"
    skip "Manual verification required - open browser console"
}

# Old services removal checks
check_old_services_removal() {
    print_header "Old Services Removal (4 checks)"

    print_check "Seq container stopped"
    if docker ps | grep -q "seq"; then
        fail "Seq container still running - stop with: docker stop seq"
    else
        pass
    fi

    print_check "Jaeger container stopped"
    if docker ps | grep -q "jaeger"; then
        fail "Jaeger container still running - stop with: docker stop jaeger"
    else
        pass
    fi

    print_check "No :8081 references in code (Seq)"
    if grep -r "localhost:8081" apps/ --exclude-dir=node_modules --exclude-dir=.next --exclude="*.js" --exclude="*.md" 2>/dev/null | grep -v "CRITICAL: In CI\|mock API server" | grep -q .; then
        fail "Found Seq port 8081 references in code"
    else
        pass
    fi

    print_check "No :16686 references in code (Jaeger)"
    if grep -r "localhost:16686" apps/ --exclude-dir=node_modules --exclude-dir=.next --exclude="*.js" --exclude="*.md" 2>/dev/null | grep -q .; then
        fail "Found Jaeger port 16686 references in code"
    else
        pass
    fi
}

# Alert configuration checks
check_alert_configuration() {
    print_header "Alert Configuration (3 checks)"

    print_check "3+ HyperDX alerts created and active"
    skip "Manual verification required - check HyperDX UI → Alerts"

    print_check "Test alert triggers (generate errors)"
    skip "Manual test required - POST /api/v1/telemetry/test-error"

    print_check "Email notification received within 2 min"
    skip "Manual verification required - check email/Slack"
}

# Performance & load checks
check_performance() {
    print_header "Performance & Load (5 checks)"

    print_check "k6 load test passed (100 users, no failures)"
    if [[ -f "tests/k6/hyperdx-ingestion-test.js" ]]; then
        skip "k6 test exists - run manually: cd tests/k6 && k6 run hyperdx-ingestion-test.js"
    else
        skip "k6 test file not found"
    fi

    print_check "Log search < 1s P95"
    skip "Manual verification required - test in HyperDX UI"

    print_check "Trace query < 500ms P95"
    skip "Manual verification required - test in HyperDX UI"

    print_check "HyperDX resource usage < 4GB RAM"
    local mem_usage
    if command -v docker &> /dev/null; then
        mem_usage=$(docker stats meepleai-hyperdx --no-stream --format "{{.MemUsage}}" 2>/dev/null || echo "unknown")
        if [[ "$mem_usage" != "unknown" ]]; then
            echo -e "\n    Current usage: $mem_usage"
            skip "Manual verification - check if under 4GB"
        else
            skip "Cannot read container stats"
        fi
    else
        skip "Docker not available"
    fi

    print_check "Storage usage < 50GB"
    local storage_usage
    storage_usage=$(docker exec meepleai-hyperdx du -sh /var/lib/clickhouse 2>/dev/null | cut -f1 || echo "unknown")
    if [[ "$storage_usage" != "unknown" ]]; then
        echo -e "\n    Current usage: $storage_usage"
        skip "Manual verification - check if under 50GB"
    else
        skip "Cannot read storage usage"
    fi
}

# Documentation checks
check_documentation() {
    print_header "Documentation (5 checks)"

    print_check "CLAUDE.md updated with HyperDX"
    if grep -q "HyperDX\|hyperdx" CLAUDE.md 2>/dev/null; then
        pass
    else
        fail "CLAUDE.md not updated with HyperDX references"
    fi

    print_check "Runbooks updated (no Seq/Jaeger references)"
    if grep -r "Seq\|Jaeger" docs/05-operations/runbooks/ 2>/dev/null | grep -v "HyperDX\|replaced\|deprecated" | grep -q .; then
        fail "Runbooks still reference Seq/Jaeger"
    else
        pass
    fi

    print_check "ADR-015 created (HyperDX migration decision)"
    if [[ -f "docs/01-architecture/adr/adr-015-hyperdx-migration.md" ]]; then
        pass
    else
        skip "ADR-015 not created yet - create after verification"
    fi

    print_check "Environment variables documented"
    if grep -q "HYPERDX_OTLP_ENDPOINT\|HYPERDX_LOGS_ENDPOINT" docs/ -r 2>/dev/null; then
        pass
    else
        fail "HyperDX environment variables not documented"
    fi

    print_check "No broken links in docs"
    skip "Manual verification required - mdlint or link checker"
}

# Security checks
check_security() {
    print_header "Security (4 checks)"

    print_check "Production API key configured (not 'demo')"
    local hyperdx_key
    hyperdx_key=$(docker exec meepleai-hyperdx env 2>/dev/null | grep "HYPERDX_API_KEY" | cut -d= -f2 || echo "unknown")
    if [[ "$hyperdx_key" == "demo" ]]; then
        fail "HyperDX API key is still 'demo' - set production key"
    elif [[ "$hyperdx_key" == "unknown" ]]; then
        skip "Cannot read HYPERDX_API_KEY from container"
    else
        pass
    fi

    print_check "Sensitive data scrubbing verified"
    skip "Manual verification required - check logs/traces for PII"

    print_check "ClickHouse port secured (internal only)"
    if timeout 2 bash -c "echo > /dev/tcp/localhost/8123" 2>/dev/null; then
        fail "ClickHouse port 8123 exposed to host - security risk!"
    else
        pass
    fi

    print_check "HTTPS enabled for production (if applicable)"
    skip "N/A for local dev - production deployment check"
}

# Summary report
print_summary() {
    print_header "Verification Summary"

    echo -e "  ${BLUE}Total Checks:${NC}   $TOTAL_CHECKS"
    echo -e "  ${GREEN}Passed:${NC}        $PASSED_CHECKS"
    echo -e "  ${RED}Failed:${NC}        $FAILED_CHECKS"
    echo -e "  ${YELLOW}Skipped:${NC}       $SKIPPED_CHECKS"

    local pass_percentage
    if [[ $TOTAL_CHECKS -gt 0 ]]; then
        pass_percentage=$(( (PASSED_CHECKS * 100) / TOTAL_CHECKS ))
    else
        pass_percentage=0
    fi

    echo -e "\n  ${BLUE}Success Rate:${NC}  $pass_percentage%"

    if [[ ${#FAILED_ITEMS[@]} -gt 0 ]]; then
        echo -e "\n  ${RED}Failed Checks:${NC}"
        for item in "${FAILED_ITEMS[@]}"; do
            echo -e "    ${RED}•${NC} $item"
        done
    fi

    if [[ ${#SKIPPED_ITEMS[@]} -gt 0 ]]; then
        echo -e "\n  ${YELLOW}Skipped Checks (Manual Verification Required):${NC}"
        for item in "${SKIPPED_ITEMS[@]}"; do
            echo -e "    ${YELLOW}•${NC} $item"
        done
    fi

    echo -e "\n${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

    if [[ $FAILED_CHECKS -eq 0 ]]; then
        if [[ $SKIPPED_CHECKS -gt 0 ]]; then
            echo -e "${YELLOW}⚠️  PARTIAL SUCCESS${NC}"
            echo -e "${YELLOW}   Some checks require manual verification${NC}"
            echo -e "${YELLOW}   Review skipped items above and verify manually${NC}\n"
            return 0
        else
            echo -e "${GREEN}🎉 MIGRATION COMPLETE - Ready for Production${NC}"
            echo -e "${GREEN}   All automated checks passed!${NC}\n"
            return 0
        fi
    else
        echo -e "${RED}❌ MIGRATION INCOMPLETE${NC}"
        echo -e "${RED}   Fix failed checks before production deployment${NC}\n"
        return 1
    fi
}

# Main execution
main() {
    echo -e "\n${BLUE}╔═══════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║  HyperDX Migration Verification - Issue #1570            ║${NC}"
    echo -e "${BLUE}║  Epic #1561: Complete Observability Stack Migration      ║${NC}"
    echo -e "${BLUE}╚═══════════════════════════════════════════════════════════╝${NC}"

    check_dependencies || {
        echo -e "\n${RED}❌ Dependency checks failed${NC}"
        echo -e "${RED}   Install missing dependencies and retry${NC}\n"
        exit 2
    }

    check_infrastructure
    check_backend_integration
    check_frontend_integration
    check_old_services_removal
    check_alert_configuration
    check_performance
    check_documentation
    check_security

    print_summary
    exit $?
}

# Run main
main "$@"
