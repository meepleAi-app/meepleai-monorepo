#!/bin/bash
#
# Runbook Validation Test Script
# Issue #2004: Enable testing of high-error-rate.md and error-spike.md runbooks
#
# Prerequisites:
# - API running on localhost:8080
# - Admin user credentials (default: admin@meepleai.dev / admin123)
# - Prometheus on localhost:9090
# - jq installed for JSON parsing
#
# Usage:
#   ./scripts/test-runbooks.sh high-error-rate
#   ./scripts/test-runbooks.sh error-spike
#

set -e

# Configuration
API_BASE="http://localhost:8080"
PROMETHEUS_URL="http://localhost:9090"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@meepleai.dev}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-admin123}"
COOKIE_FILE="/tmp/meepleai-test-session.txt"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Helper functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    # Check jq
    if ! command -v jq &> /dev/null; then
        log_error "jq is not installed. Install with: sudo apt-get install jq (Ubuntu) or brew install jq (Mac)"
        exit 1
    fi

    # Check API
    if ! curl -f -s "$API_BASE/health" > /dev/null; then
        log_error "API is not running on $API_BASE"
        exit 1
    fi

    # Check Prometheus
    if ! curl -f -s "$PROMETHEUS_URL/-/ready" > /dev/null; then
        log_warning "Prometheus is not running on $PROMETHEUS_URL (alert validation will be skipped)"
    fi

    log_success "Prerequisites check passed"
}

# Login as admin and get session cookie
login_admin() {
    log_info "Logging in as admin..."

    # Remove old cookie file
    rm -f "$COOKIE_FILE"

    # Login and save cookie
    RESPONSE=$(curl -s -c "$COOKIE_FILE" -X POST "$API_BASE/api/v1/auth/login" \
        -H "Content-Type: application/json" \
        -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\"}" \
        -w "\n%{http_code}")

    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
    BODY=$(echo "$RESPONSE" | head -n-1)

    if [ "$HTTP_CODE" != "200" ]; then
        log_error "Login failed with status $HTTP_CODE: $BODY"
        exit 1
    fi

    log_success "Logged in as admin"
}

# Test high-error-rate runbook (HighErrorRate alert)
test_high_error_rate() {
    log_info "Testing high-error-rate.md runbook..."
    log_info "Objective: Trigger HighErrorRate alert (> 1 error/sec)"

    # Generate 200 errors over 120 seconds (1.67 errors/sec avg)
    local TOTAL_ERRORS=200
    local DURATION_SECONDS=120
    local DELAY=$(echo "scale=3; $DURATION_SECONDS / $TOTAL_ERRORS" | bc)

    log_info "Generating $TOTAL_ERRORS errors over $DURATION_SECONDS seconds..."
    log_info "Delay between requests: ${DELAY}s (1.67 errors/sec)"

    local SUCCESS_COUNT=0
    local ERROR_COUNT=0

    for i in $(seq 1 $TOTAL_ERRORS); do
        RESPONSE=$(curl --max-time 30 --connect-timeout 10 -s -b "$COOKIE_FILE" -X POST "$API_BASE/api/v1/test/error" \
            -H "Content-Type: application/json" \
            -d '{"errorType":"500"}' \
            -w "\n%{http_code}" \
            2>&1)

        HTTP_CODE=$(echo "$RESPONSE" | tail -n1)

        if [ "$HTTP_CODE" == "500" ] || [ "$HTTP_CODE" == "400" ]; then
            ((SUCCESS_COUNT++))
        else
            ((ERROR_COUNT++))
            log_warning "Request $i failed with status $HTTP_CODE"
        fi

        # Progress indicator every 20 requests
        if [ $((i % 20)) -eq 0 ]; then
            log_info "Progress: $i/$TOTAL_ERRORS errors generated"
        fi

        # Delay between requests (respect rate limiting)
        sleep "$DELAY"
    done

    log_success "Error generation complete: $SUCCESS_COUNT simulated, $ERROR_COUNT failed"

    # Wait for alert to fire (2 minutes for HighErrorRate)
    log_info "Waiting 130 seconds for HighErrorRate alert to fire..."
    sleep 130

    # Check Prometheus alerts
    check_prometheus_alert "HighErrorRate"
}

# Test error-spike runbook (ErrorSpike alert)
test_error_spike() {
    log_info "Testing error-spike.md runbook..."
    log_info "Objective: Trigger ErrorSpike alert (3x baseline increase)"

    # Generate burst of 200 errors rapidly (spike)
    local TOTAL_ERRORS=200

    log_info "Generating $TOTAL_ERRORS errors in rapid burst..."

    local SUCCESS_COUNT=0
    local ERROR_COUNT=0
    local PIDS=()

    for i in $(seq 1 $TOTAL_ERRORS); do
        curl --max-time 30 --connect-timeout 10 -s -b "$COOKIE_FILE" -X POST "$API_BASE/api/v1/test/error" \
            -H "Content-Type: application/json" \
            -d '{"errorType":"exception"}' \
            > /dev/null 2>&1 &

        PIDS+=($!)

        # Progress indicator every 50 requests
        if [ $((i % 50)) -eq 0 ]; then
            log_info "Progress: $i/$TOTAL_ERRORS errors generated"
        fi
    done

    # Wait for all background requests with timeout (3 min max)
    log_info "Waiting for background requests to complete (timeout: 180s)..."

    if timeout 180 bash -c 'wait'; then
        SUCCESS_COUNT=$TOTAL_ERRORS
        log_success "Error spike generation complete: $SUCCESS_COUNT errors sent"
    else
        log_warning "Some requests timed out, but spike test may still be valid"
        # Count how many PIDs are still running
        local RUNNING=0
        for pid in "${PIDS[@]}"; do
            if kill -0 "$pid" 2>/dev/null; then
                ((RUNNING++))
                kill "$pid" 2>/dev/null || true
            fi
        done
        SUCCESS_COUNT=$((TOTAL_ERRORS - RUNNING))
        log_warning "$RUNNING requests killed due to timeout, $SUCCESS_COUNT completed"
    fi

    # Wait for alert to fire (5 minutes for ErrorSpike)
    log_info "Waiting 310 seconds for ErrorSpike alert to fire..."
    sleep 310

    # Check Prometheus alerts
    check_prometheus_alert "ErrorSpike"
}

# Check Prometheus alert status
check_prometheus_alert() {
    local ALERT_NAME=$1

    log_info "Checking Prometheus alert: $ALERT_NAME..."

    if ! curl -f -s "$PROMETHEUS_URL/-/ready" > /dev/null; then
        log_warning "Prometheus not available, skipping alert validation"
        return
    fi

    # Query Prometheus alerts API
    ALERTS=$(curl -s "$PROMETHEUS_URL/api/v1/alerts" | jq -r ".data.alerts[] | select(.labels.alertname == \"$ALERT_NAME\") | .state")

    if [ -z "$ALERTS" ]; then
        log_warning "Alert $ALERT_NAME not found in Prometheus"
        log_info "Check Prometheus manually: $PROMETHEUS_URL/alerts"
    elif echo "$ALERTS" | grep -q "firing"; then
        log_success "Alert $ALERT_NAME is FIRING"
    elif echo "$ALERTS" | grep -q "pending"; then
        log_warning "Alert $ALERT_NAME is PENDING (may fire soon)"
    else
        log_warning "Alert $ALERT_NAME state: $ALERTS"
    fi
}

# Cleanup: Clear error logs and reset state
cleanup() {
    log_info "Cleaning up..."

    # Remove cookie file
    rm -f "$COOKIE_FILE"

    # Optional: Clear API logs (if needed)
    # docker compose logs api --tail 0 > /dev/null 2>&1

    log_success "Cleanup complete"
}

# Main execution
main() {
    local TEST_TYPE=${1:-}

    if [ -z "$TEST_TYPE" ]; then
        echo "Usage: $0 <high-error-rate|error-spike|all>"
        echo ""
        echo "Examples:"
        echo "  $0 high-error-rate    # Test high-error-rate.md runbook"
        echo "  $0 error-spike        # Test error-spike.md runbook"
        echo "  $0 all                # Test all runbooks"
        exit 1
    fi

    check_prerequisites
    login_admin

    case "$TEST_TYPE" in
        high-error-rate)
            test_high_error_rate
            ;;
        error-spike)
            test_error_spike
            ;;
        all)
            test_high_error_rate
            echo ""
            test_error_spike
            ;;
        *)
            log_error "Invalid test type: $TEST_TYPE"
            echo "Valid options: high-error-rate, error-spike, all"
            exit 1
            ;;
    esac

    cleanup

    log_success "Runbook test complete!"
    log_info "Next steps:"
    log_info "1. Check Prometheus alerts: $PROMETHEUS_URL/alerts"
    log_info "2. Check Grafana dashboards: http://localhost:3001"
    log_info "3. Verify API logs: docker compose logs api --tail 50"
}

# Run main with all arguments
main "$@"
