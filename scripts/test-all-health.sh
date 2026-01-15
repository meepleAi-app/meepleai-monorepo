#!/bin/bash

# Comprehensive Health Check Script
# Tests all services, OAuth configuration, and generates summary report

set -e

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

echo ""
echo "==========================================="
echo "   MeepleAI Comprehensive Health Check"
echo "==========================================="
echo ""

# Test 1: Docker Services
echo -e "${CYAN}[1/3]${NC} Testing Docker Services..."
echo "==========================================="
./scripts/test-services.sh
echo ""

# Test 2: OAuth Secrets Validation
echo -e "${CYAN}[2/3]${NC} Validating OAuth Secrets..."
echo "==========================================="
python scripts/validate-oauth-secrets.py
echo ""

# Test 3: OAuth Health Check API
echo -e "${CYAN}[3/3]${NC} Testing OAuth Health Check API..."
echo "==========================================="
./scripts/test-oauth-health.sh
echo ""

# Final Summary
echo "==========================================="
echo "   Final Summary"
echo "==========================================="
echo ""

# Get health check status
HEALTH_STATUS=$(curl -s http://localhost:8080/health | jq -r '.status' 2>/dev/null || echo "Unknown")
CONFIG_STATUS=$(curl -s http://localhost:8080/health/config | jq -r '.status' 2>/dev/null || echo "Unknown")

echo -e "Main Health:     ${GREEN}$HEALTH_STATUS${NC}"
echo -e "Configuration:   ${YELLOW}$CONFIG_STATUS${NC}"
echo ""

# Count services
TOTAL_CHECKS=$(curl -s http://localhost:8080/health | jq '.checks | length' 2>/dev/null || echo "0")
HEALTHY_COUNT=$(curl -s http://localhost:8080/health | jq '[.checks[] | select(.status == "Healthy")] | length' 2>/dev/null || echo "0")
DEGRADED_COUNT=$(curl -s http://localhost:8080/health | jq '[.checks[] | select(.status == "Degraded")] | length' 2>/dev/null || echo "0")

echo "Health Checks:"
echo -e "  Total:    $TOTAL_CHECKS"
echo -e "  Healthy:  ${GREEN}$HEALTHY_COUNT${NC}"
echo -e "  Degraded: ${YELLOW}$DEGRADED_COUNT${NC}"
echo ""

# OAuth summary
OAUTH_PROVIDERS=$(curl -s http://localhost:8080/health/config | jq -r '.checks[0].data.oauth_configured_providers | length' 2>/dev/null || echo "0")
echo -e "OAuth Providers: ${GREEN}$OAUTH_PROVIDERS/3 configured${NC}"
echo ""

echo "==========================================="
echo -e "${GREEN}✅ Health check completed successfully${NC}"
echo "==========================================="
echo ""
echo "Quick Links:"
echo "  Frontend:    http://localhost:3000"
echo "  API:         http://localhost:8080"
echo "  Health:      http://localhost:8080/health"
echo "  Config:      http://localhost:8080/health/config"
echo "  Swagger:     http://localhost:8080/scalar/v1"
echo "  Grafana:     http://localhost:3001 (admin/admin)"
echo ""
