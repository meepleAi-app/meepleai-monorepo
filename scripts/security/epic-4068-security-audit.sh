#!/bin/bash
# Epic #4068: Security Audit Script
#
# Comprehensive security audit for permission system
# Run before production deployment
#
# Usage: ./epic-4068-security-audit.sh [environment]
# Example: ./epic-4068-security-audit.sh production

set -e

ENVIRONMENT="${1:-development}"
API_URL="${API_URL:-http://localhost:8080}"
WEB_URL="${WEB_URL:-http://localhost:3000}"

echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║      Epic #4068: Permission System Security Audit            ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo ""
echo "Environment: $ENVIRONMENT"
echo "API URL: $API_URL"
echo "Web URL: $WEB_URL"
echo ""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

FAILED_CHECKS=0
PASSED_CHECKS=0
WARNING_CHECKS=0

check_pass() {
    echo -e "${GREEN}✓${NC} $1"
    ((PASSED_CHECKS++))
}

check_fail() {
    echo -e "${RED}✗${NC} $1"
    ((FAILED_CHECKS++))
}

check_warn() {
    echo -e "${YELLOW}⚠${NC} $1"
    ((WARNING_CHECKS++))
}

# ============================================================================
# 1. Authentication & Authorization
# ============================================================================

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "1. Authentication & Authorization"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Test 1.1: Permission endpoint requires authentication
echo "Test 1.1: /permissions/me requires authentication"
RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" "$API_URL/api/v1/permissions/me")
if [ "$RESPONSE" == "401" ]; then
    check_pass "Unauthenticated request denied (401)"
else
    check_fail "Expected 401, got $RESPONSE (endpoint may be unprotected!)"
fi

# Test 1.2: Invalid token rejected
echo "Test 1.2: Invalid JWT token rejected"
RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
    -H "Authorization: Bearer invalid.token.here" \
    "$API_URL/api/v1/permissions/me")
if [ "$RESPONSE" == "401" ]; then
    check_pass "Invalid token rejected (401)"
else
    check_fail "Expected 401, got $RESPONSE (token validation weak!)"
fi

# Test 1.3: Expired token rejected
echo "Test 1.3: Expired token handling"
# (Requires generating expired token, skipped in basic audit)
check_warn "Expired token test skipped (manual verification required)"

# ============================================================================
# 2. Authorization Logic
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "2. Authorization Logic (Tier/Role/State)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Get test tokens (requires test users in database)
FREE_TOKEN="${FREE_USER_TOKEN:-}"
PRO_TOKEN="${PRO_USER_TOKEN:-}"
ADMIN_TOKEN="${ADMIN_USER_TOKEN:-}"

if [ -z "$FREE_TOKEN" ] || [ -z "$PRO_TOKEN" ] || [ -z "$ADMIN_TOKEN" ]; then
    check_warn "Test tokens not provided (export FREE_USER_TOKEN, PRO_USER_TOKEN, ADMIN_USER_TOKEN)"
    check_warn "Skipping authorization logic tests"
else
    # Test 2.1: Free user cannot bulk-select
    echo "Test 2.1: Free tier user denied Pro features"
    RESPONSE=$(curl -s -H "Authorization: Bearer $FREE_TOKEN" \
        "$API_URL/api/v1/permissions/check?feature=bulk-select")

    HAS_ACCESS=$(echo "$RESPONSE" | jq -r '.hasAccess')
    if [ "$HAS_ACCESS" == "false" ]; then
        check_pass "Free user denied bulk-select"
    else
        check_fail "Free user has bulk-select access (permission leak!)"
    fi

    # Test 2.2: Pro user can bulk-select
    echo "Test 2.2: Pro tier user granted Pro features"
    RESPONSE=$(curl -s -H "Authorization: Bearer $PRO_TOKEN" \
        "$API_URL/api/v1/permissions/check?feature=bulk-select")

    HAS_ACCESS=$(echo "$RESPONSE" | jq -r '.hasAccess')
    if [ "$HAS_ACCESS" == "true" ]; then
        check_pass "Pro user granted bulk-select"
    else
        check_fail "Pro user denied bulk-select (permission too restrictive!)"
    fi

    # Test 2.3: Admin has admin features
    echo "Test 2.3: Admin role grants admin features"
    RESPONSE=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API_URL/api/v1/permissions/check?feature=quick-action.delete")

    HAS_ACCESS=$(echo "$RESPONSE" | jq -r '.hasAccess')
    if [ "$HAS_ACCESS" == "true" ]; then
        check_pass "Admin granted delete feature"
    else
        check_fail "Admin denied delete feature (permission misconfigured!)"
    fi

    # Test 2.4: Non-admin denied admin features
    echo "Test 2.4: Non-admin denied admin features"
    RESPONSE=$(curl -s -H "Authorization: Bearer $FREE_TOKEN" \
        "$API_URL/api/v1/permissions/check?feature=quick-action.delete")

    HAS_ACCESS=$(echo "$RESPONSE" | jq -r '.hasAccess')
    if [ "$HAS_ACCESS" == "false" ]; then
        check_pass "Free user denied admin delete feature"
    else
        check_fail "Free user has admin delete access (privilege escalation!)"
    fi
fi

# ============================================================================
# 3. Input Validation
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "3. Input Validation"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Test 3.1: Missing feature parameter
echo "Test 3.1: Missing 'feature' parameter returns 400"
RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
    -H "Authorization: Bearer $FREE_TOKEN" \
    "$API_URL/api/v1/permissions/check")

if [ "$RESPONSE" == "400" ]; then
    check_pass "Missing parameter returns 400 Bad Request"
elif [ "$RESPONSE" == "401" ] && [ -z "$FREE_TOKEN" ]; then
    check_warn "Skipped (no test token)"
else
    check_fail "Expected 400, got $RESPONSE (input validation weak!)"
fi

# Test 3.2: SQL injection attempt
echo "Test 3.2: SQL injection in feature parameter"
RESPONSE=$(curl -s -H "Authorization: Bearer $FREE_TOKEN" \
    "$API_URL/api/v1/permissions/check?feature=wishlist';DROP TABLE Users;--")

# Should return 404 (unknown feature) or 400 (validation error), NOT 500
STATUS=$(echo "$RESPONSE" | jq -r '.status // empty')
if [ "$STATUS" == "404" ] || [ "$STATUS" == "400" ]; then
    check_pass "SQL injection attempt handled safely"
elif [ -z "$FREE_TOKEN" ]; then
    check_warn "Skipped (no test token)"
else
    check_fail "Unexpected response to SQL injection attempt (status: $STATUS)"
fi

# Test 3.3: XSS attempt in state parameter
echo "Test 3.3: XSS in state parameter"
RESPONSE=$(curl -s -H "Authorization: Bearer $FREE_TOKEN" \
    "$API_URL/api/v1/permissions/check?feature=view-game&state=<script>alert('xss')</script>")

# Should sanitize or reject, not reflect script in response
BODY=$(echo "$RESPONSE" | jq -r 'tostring')
if echo "$BODY" | grep -q "<script>"; then
    check_fail "XSS payload reflected in response (XSS vulnerability!)"
elif [ -z "$FREE_TOKEN" ]; then
    check_warn "Skipped (no test token)"
else
    check_pass "XSS payload sanitized or rejected"
fi

# ============================================================================
# 4. Rate Limiting
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "4. Rate Limiting"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Test 4.1: Rate limit enforced (100 req/min)
echo "Test 4.1: Rate limit enforcement (sending 110 requests)"

if [ -n "$FREE_TOKEN" ]; then
    RATE_LIMITED=false

    for i in {1..110}; do
        RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
            -H "Authorization: Bearer $FREE_TOKEN" \
            "$API_URL/api/v1/permissions/check?feature=wishlist")

        if [ "$RESPONSE" == "429" ]; then
            RATE_LIMITED=true
            echo "  Rate limited after $i requests"
            break
        fi
    done

    if [ "$RATE_LIMITED" == true ]; then
        check_pass "Rate limiting active (429 Too Many Requests)"
    else
        check_warn "Rate limiting not detected (may be disabled in dev)"
    fi
else
    check_warn "Skipped (no test token)"
fi

# ============================================================================
# 5. HTTPS & TLS
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "5. HTTPS & TLS (Production Only)"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ "$ENVIRONMENT" == "production" ]; then
    # Test 5.1: HTTPS redirect
    echo "Test 5.1: HTTP → HTTPS redirect"
    HTTP_URL=$(echo "$API_URL" | sed 's/https/http/')
    RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" -L "$HTTP_URL/health")

    if [ "$RESPONSE" == "200" ]; then
        # Check if redirected to HTTPS
        FINAL_URL=$(curl -s -o /dev/null -w "%{url_effective}" -L "$HTTP_URL/health")
        if echo "$FINAL_URL" | grep -q "https://"; then
            check_pass "HTTP redirects to HTTPS"
        else
            check_fail "HTTP does not redirect to HTTPS (insecure!)"
        fi
    else
        check_warn "Unable to test HTTP redirect"
    fi

    # Test 5.2: TLS version
    echo "Test 5.2: TLS 1.2+ enforced"
    TLS_VERSION=$(openssl s_client -connect "${API_URL#https://}" -tls1_1 2>&1 | grep "Protocol" || echo "")

    if echo "$TLS_VERSION" | grep -q "TLSv1.2\|TLSv1.3"; then
        check_pass "TLS 1.2+ enforced"
    elif [ -z "$TLS_VERSION" ]; then
        check_pass "TLS 1.1 rejected (good)"
    else
        check_fail "Weak TLS version accepted (upgrade required!)"
    fi

    # Test 5.3: HSTS header
    echo "Test 5.3: HSTS (Strict-Transport-Security) header"
    HSTS=$(curl -s -I "$API_URL/health" | grep -i "strict-transport-security" || echo "")

    if [ -n "$HSTS" ]; then
        check_pass "HSTS header present: $HSTS"
    else
        check_warn "HSTS header missing (recommended for production)"
    fi
else
    check_warn "HTTPS tests skipped (not production environment)"
fi

# ============================================================================
# 6. Dependency Vulnerabilities
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "6. Dependency Vulnerabilities"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Test 6.1: Backend NuGet packages
echo "Test 6.1: NuGet package vulnerabilities"
cd apps/api/src/Api

NUGET_AUDIT=$(dotnet list package --vulnerable --include-transitive 2>&1)

if echo "$NUGET_AUDIT" | grep -q "no vulnerable packages"; then
    check_pass "No vulnerable NuGet packages"
elif echo "$NUGET_AUDIT" | grep -q "vulnerable"; then
    check_fail "Vulnerable NuGet packages found:"
    echo "$NUGET_AUDIT" | grep ">"
else
    check_warn "Unable to determine NuGet vulnerability status"
fi

cd ../../../..

# Test 6.2: Frontend npm packages
echo "Test 6.2: npm package vulnerabilities"
cd apps/web

NPM_AUDIT=$(pnpm audit --prod 2>&1)
HIGH_VULNS=$(echo "$NPM_AUDIT" | grep -o "[0-9]* high" | grep -o "[0-9]*" || echo "0")

if [ "$HIGH_VULNS" == "0" ]; then
    check_pass "No high/critical npm vulnerabilities"
else
    check_fail "$HIGH_VULNS high-severity npm vulnerabilities found"
    echo "$NPM_AUDIT"
fi

cd ../..

# ============================================================================
# 7. Permission System Specific Tests
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "7. Permission System Specific Tests"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

if [ -n "$FREE_TOKEN" ] && [ -n "$ADMIN_TOKEN" ]; then
    # Test 7.1: Privilege escalation prevention
    echo "Test 7.1: Free user cannot access admin endpoint"
    RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
        -X POST \
        -H "Authorization: Bearer $FREE_TOKEN" \
        "$API_URL/api/v1/admin/users/delete")

    if [ "$RESPONSE" == "403" ] || [ "$RESPONSE" == "404" ]; then
        check_pass "Free user denied admin endpoint ($RESPONSE)"
    else
        check_fail "Free user accessed admin endpoint (privilege escalation!)"
    fi

    # Test 7.2: Tier-based feature lockout
    echo "Test 7.2: Free tier locked out of Pro features"
    RESPONSE=$(curl -s -H "Authorization: Bearer $FREE_TOKEN" \
        "$API_URL/api/v1/permissions/me")

    FEATURES=$(echo "$RESPONSE" | jq -r '.accessibleFeatures[]')

    if echo "$FEATURES" | grep -q "bulk-select"; then
        check_fail "Free user has bulk-select in accessible features (tier bypass!)"
    else
        check_pass "Free user correctly denied Pro features"
    fi

    # Test 7.3: Admin role grants admin features
    echo "Test 7.3: Admin role access verification"
    RESPONSE=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" \
        "$API_URL/api/v1/permissions/me")

    FEATURES=$(echo "$RESPONSE" | jq -r '.accessibleFeatures[]')

    if echo "$FEATURES" | grep -q "quick-action.delete"; then
        check_pass "Admin has delete feature"
    else
        check_fail "Admin denied delete feature (role not working!)"
    fi

    # Test 7.4: Suspended user denied access
    if [ -n "$SUSPENDED_USER_TOKEN" ]; then
        echo "Test 7.4: Suspended user denied access"
        RESPONSE=$(curl -s -H "Authorization: Bearer $SUSPENDED_USER_TOKEN" \
            "$API_URL/api/v1/permissions/me")

        FEATURES=$(echo "$RESPONSE" | jq -r '.accessibleFeatures | length')

        if [ "$FEATURES" == "0" ]; then
            check_pass "Suspended user has no accessible features"
        else
            check_fail "Suspended user has $FEATURES features (should be 0!)"
        fi
    else
        check_warn "Suspended user test skipped (no SUSPENDED_USER_TOKEN)"
    fi
else
    check_warn "Permission logic tests skipped (provide test tokens)"
fi

# ============================================================================
# 8. Frontend Security
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "8. Frontend Security"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Test 8.1: Content Security Policy
echo "Test 8.1: CSP header present"
CSP=$(curl -s -I "$WEB_URL" | grep -i "content-security-policy" || echo "")

if [ -n "$CSP" ]; then
    check_pass "CSP header present"

    # Verify unsafe-inline not used
    if echo "$CSP" | grep -q "unsafe-inline"; then
        check_warn "CSP allows unsafe-inline (XSS risk)"
    else
        check_pass "CSP does not allow unsafe-inline"
    fi
else
    check_warn "CSP header missing (recommended)"
fi

# Test 8.2: X-Frame-Options
echo "Test 8.2: X-Frame-Options header"
XFO=$(curl -s -I "$WEB_URL" | grep -i "x-frame-options" || echo "")

if echo "$XFO" | grep -iq "DENY\|SAMEORIGIN"; then
    check_pass "X-Frame-Options set correctly"
else
    check_warn "X-Frame-Options missing (clickjacking risk)"
fi

# Test 8.3: No sensitive data in localStorage
echo "Test 8.3: No JWT tokens in localStorage (if using httpOnly cookies)"
check_warn "Manual verification required: Check browser localStorage for 'token' or 'accessToken'"

# ============================================================================
# 9. Database Security
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "9. Database Security"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Test 9.1: Check constraints enforced
echo "Test 9.1: Database check constraints"
docker exec meepleai-postgres psql -U postgres -d meepleai -t -c \
    "SELECT conname FROM pg_constraint WHERE conname LIKE 'CK_Users_%';" > /tmp/constraints.txt

if grep -q "CK_Users_Tier" /tmp/constraints.txt; then
    check_pass "Tier check constraint exists"
else
    check_fail "Tier check constraint missing (data integrity risk!)"
fi

if grep -q "CK_Users_Role" /tmp/constraints.txt; then
    check_pass "Role check constraint exists"
else
    check_fail "Role check constraint missing"
fi

if grep -q "CK_Users_Status" /tmp/constraints.txt; then
    check_pass "Status check constraint exists"
else
    check_fail "Status check constraint missing"
fi

# Test 9.2: Indexes exist
echo "Test 9.2: Performance indexes"
docker exec meepleai-postgres psql -U postgres -d meepleai -t -c \
    "SELECT indexname FROM pg_indexes WHERE tablename='Users';" > /tmp/indexes.txt

if grep -q "IX_Users_Tier_Role_Status" /tmp/indexes.txt; then
    check_pass "Composite permission index exists"
else
    check_warn "Composite index missing (performance impact)"
fi

# ============================================================================
# 10. Logging & Monitoring
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "10. Logging & Monitoring"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"

# Test 10.1: Permission denials logged
echo "Test 10.1: Permission denials logged"
check_warn "Manual verification: Check logs for permission denial entries"
check_warn "Expected log: 'Permission denied: feature=bulk-select, user=free/user'"

# Test 10.2: Metrics endpoint available
echo "Test 10.2: Prometheus metrics endpoint"
METRICS=$(curl -s "$API_URL/metrics" | head -n 5)

if echo "$METRICS" | grep -q "# HELP"; then
    check_pass "Metrics endpoint available"

    # Check for permission-specific metrics
    if curl -s "$API_URL/metrics" | grep -q "permission_check_total"; then
        check_pass "Permission check metrics exported"
    else
        check_warn "Permission check metrics not found"
    fi
else
    check_warn "Metrics endpoint not accessible"
fi

# ============================================================================
# Summary
# ============================================================================

echo ""
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "SECURITY AUDIT SUMMARY"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""
echo -e "${GREEN}Passed:  $PASSED_CHECKS${NC}"
echo -e "${YELLOW}Warnings: $WARNING_CHECKS${NC}"
echo -e "${RED}Failed:  $FAILED_CHECKS${NC}"
echo ""

if [ "$FAILED_CHECKS" -gt 0 ]; then
    echo -e "${RED}❌ SECURITY AUDIT FAILED${NC}"
    echo "Fix critical issues before deploying to production."
    exit 1
elif [ "$WARNING_CHECKS" -gt 5 ]; then
    echo -e "${YELLOW}⚠️  SECURITY AUDIT PASSED WITH WARNINGS${NC}"
    echo "Review warnings and address before production deployment."
    exit 0
else
    echo -e "${GREEN}✅ SECURITY AUDIT PASSED${NC}"
    echo "Permission system ready for deployment."
    exit 0
fi
