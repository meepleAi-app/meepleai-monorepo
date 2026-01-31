#!/bin/bash
# Health Check Script
# Verifies all services are healthy in staging/production

set -e

ENVIRONMENT="${1:-staging}"
MAX_RETRIES=30
RETRY_DELAY=2

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Environment URLs
case "$ENVIRONMENT" in
    staging)
        API_URL="https://api.staging.meepleai.dev"
        WEB_URL="https://staging.meepleai.dev"
        ;;
    production)
        API_URL="https://api.meepleai.dev"
        WEB_URL="https://meepleai.dev"
        ;;
    local)
        API_URL="http://localhost:8080"
        WEB_URL="http://localhost:3000"
        ;;
    *)
        echo -e "${RED}Unknown environment: ${ENVIRONMENT}${NC}"
        exit 1
        ;;
esac

echo -e "${YELLOW}🏥 Health Check - ${ENVIRONMENT}${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Check API health endpoint
echo "Checking API health..."
RETRIES=0
while [ $RETRIES -lt $MAX_RETRIES ]; do
    if curl -sf "${API_URL}/health" > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} API is healthy"

        # Get detailed health info
        HEALTH_RESPONSE=$(curl -s "${API_URL}/health")
        echo "  Status: $(echo $HEALTH_RESPONSE | jq -r '.status' 2>/dev/null || echo 'Healthy')"
        break
    else
        RETRIES=$((RETRIES + 1))
        if [ $RETRIES -lt $MAX_RETRIES ]; then
            echo "  Retry $RETRIES/$MAX_RETRIES..."
            sleep $RETRY_DELAY
        else
            echo -e "${RED}✗${NC} API health check failed after $MAX_RETRIES attempts"
            exit 1
        fi
    fi
done

# Check API database connection
echo "Checking API database..."
if curl -sf "${API_URL}/health/ready" > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} Database connection healthy"
else
    echo -e "${RED}✗${NC} Database connection failed"
    exit 1
fi

# Check Web application
echo "Checking Web application..."
RETRIES=0
while [ $RETRIES -lt $MAX_RETRIES ]; do
    if curl -sf "${WEB_URL}" > /dev/null 2>&1; then
        echo -e "${GREEN}✓${NC} Web application is healthy"
        break
    else
        RETRIES=$((RETRIES + 1))
        if [ $RETRIES -lt $MAX_RETRIES ]; then
            echo "  Retry $RETRIES/$MAX_RETRIES..."
            sleep $RETRY_DELAY
        else
            echo -e "${RED}✗${NC} Web health check failed after $MAX_RETRIES attempts"
            exit 1
        fi
    fi
done

# Check critical dependencies (if endpoints exist)
echo "Checking dependencies..."

# PostgreSQL
if curl -sf "${API_URL}/health/postgres" > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} PostgreSQL connected"
else
    echo -e "${YELLOW}⚠${NC}  PostgreSQL health endpoint not available"
fi

# Redis
if curl -sf "${API_URL}/health/redis" > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} Redis connected"
else
    echo -e "${YELLOW}⚠${NC}  Redis health endpoint not available"
fi

# Qdrant
if curl -sf "${API_URL}/health/qdrant" > /dev/null 2>&1; then
    echo -e "${GREEN}✓${NC} Qdrant connected"
else
    echo -e "${YELLOW}⚠${NC}  Qdrant health endpoint not available"
fi

echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}✅ All health checks passed${NC}"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
