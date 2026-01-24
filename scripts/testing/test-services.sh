#!/bin/bash

# MeepleAI Services Health Check Script
# Tests all Docker services and reports their status

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "==========================================="
echo "   MeepleAI Services Health Check"
echo "==========================================="
echo ""

# Function to check HTTP service
check_http() {
    local name=$1
    local url=$2
    local status_code=$(curl -s -o /dev/null -w '%{http_code}' "$url" 2>/dev/null || echo "000")

    if [ "$status_code" = "200" ] || [ "$status_code" = "302" ]; then
        echo -e "${name}: ${GREEN}✅ OK${NC} (HTTP $status_code)"
    elif [ "$status_code" = "000" ]; then
        echo -e "${name}: ${RED}❌ DOWN${NC} (Connection refused)"
    else
        echo -e "${name}: ${YELLOW}⚠️  WARN${NC} (HTTP $status_code)"
    fi
}

# Web Services
echo "=== Web Services ==="
check_http "Frontend          " "http://localhost:3000"
check_http "API Health        " "http://localhost:8080/health"
check_http "API Swagger       " "http://localhost:8080/scalar/v1"
echo ""

# Monitoring Services
echo "=== Monitoring & Observability ==="
check_http "Grafana           " "http://localhost:3001"
check_http "Prometheus        " "http://localhost:9090/-/healthy"
check_http "Alertmanager      " "http://localhost:9093"
check_http "cAdvisor          " "http://localhost:8082"
check_http "Node Exporter     " "http://localhost:9100/metrics"
echo ""

# Development Tools
echo "=== Development Tools ==="
check_http "Mailpit UI        " "http://localhost:8025"
check_http "n8n Workflow      " "http://localhost:5678"
echo ""

# AI Services
echo "=== AI Services ==="
check_http "Embedding Service " "http://localhost:8000/health"
check_http "Unstructured      " "http://localhost:8001/health"
check_http "SmolDocling       " "http://localhost:8002/health"
check_http "Reranker Service  " "http://localhost:8003/health"
check_http "Ollama            " "http://localhost:11434/api/tags"
echo ""

# Vector DB
echo "=== Storage Services ==="
check_http "Qdrant HTTP       " "http://localhost:6333/collections"

# PostgreSQL
echo -n "PostgreSQL        : "
POSTGRES_STATUS=$(docker exec meepleai-postgres pg_isready -U meepleai 2>&1 || echo "error")
if [[ $POSTGRES_STATUS == *"accepting connections"* ]]; then
    echo -e "${GREEN}✅ OK${NC} (Accepting connections)"
else
    echo -e "${RED}❌ DOWN${NC} ($POSTGRES_STATUS)"
fi

# Redis
echo -n "Redis             : "
REDIS_PASSWORD=$(cat infra/secrets/redis-password.txt 2>/dev/null | tr -d '\n\r')
REDIS_STATUS=$(docker exec meepleai-redis redis-cli -a "$REDIS_PASSWORD" PING 2>/dev/null || echo "error")
if [[ $REDIS_STATUS == "PONG" ]]; then
    echo -e "${GREEN}✅ OK${NC} (PONG)"
else
    echo -e "${RED}❌ DOWN${NC} (No response)"
fi

echo ""
echo "==========================================="
echo "   Summary"
echo "==========================================="
echo ""
echo "Legend:"
echo "  ✅ OK   = Service is healthy"
echo "  ⚠️  WARN = Service responded but with non-200 status"
echo "  ❌ DOWN = Service is not responding"
echo ""
echo "Quick Links:"
echo "  Frontend:    http://localhost:3000"
echo "  API Docs:    http://localhost:8080/scalar/v1"
echo "  Grafana:     http://localhost:3001 (admin/admin)"
echo "  Prometheus:  http://localhost:9090"
echo "  Mailpit:     http://localhost:8025"
echo ""
