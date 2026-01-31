#!/bin/bash
# Rollback to Previous Deployment
# Reverts to last known good deployment

set -e

ENVIRONMENT="${1:-staging}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

DOCKER_REGISTRY="${DOCKER_REGISTRY:-ghcr.io/degrassiaaron}"

echo -e "${RED}⏮️  Rollback - ${ENVIRONMENT}${NC}"
echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

# Get previous deployment
LAST_DEPLOY_FILE="/tmp/meepleai-last-${ENVIRONMENT}-deployment.txt"

if [[ ! -f "$LAST_DEPLOY_FILE" ]]; then
    echo -e "${RED}❌ No previous deployment found${NC}"
    echo "Cannot rollback without deployment history"
    exit 1
fi

PREVIOUS_TAG=$(cat "$LAST_DEPLOY_FILE")
echo "Previous deployment: ${PREVIOUS_TAG}"
echo ""

# Confirmation
echo -e "${YELLOW}⚠️  This will rollback ${ENVIRONMENT} to: ${PREVIOUS_TAG}${NC}"
read -p "Continue? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    exit 1
fi

# Rollback API
echo "Rolling back API..."
docker pull "${DOCKER_REGISTRY}/meepleai-api:${PREVIOUS_TAG}"
docker tag "${DOCKER_REGISTRY}/meepleai-api:${PREVIOUS_TAG}" \
    "${DOCKER_REGISTRY}/meepleai-api:${ENVIRONMENT}-latest"
docker push "${DOCKER_REGISTRY}/meepleai-api:${ENVIRONMENT}-latest"
echo -e "${GREEN}✓${NC} API rolled back"

# Rollback Web
echo "Rolling back Web..."
docker pull "${DOCKER_REGISTRY}/meepleai-web:${PREVIOUS_TAG}"
docker tag "${DOCKER_REGISTRY}/meepleai-web:${PREVIOUS_TAG}" \
    "${DOCKER_REGISTRY}/meepleai-web:${ENVIRONMENT}-latest"
docker push "${DOCKER_REGISTRY}/meepleai-web:${ENVIRONMENT}-latest"
echo -e "${GREEN}✓${NC} Web rolled back"

echo ""
echo -e "${YELLOW}Rollback images tagged. Deploy them:${NC}"
echo "1. SSH to ${ENVIRONMENT} server"
echo "2. Pull: docker-compose pull"
echo "3. Deploy: docker-compose up -d"
echo ""
echo "4. Verify with: bash tools/deployment/health-check.sh ${ENVIRONMENT}"

# Run health check after manual deployment
read -p "Press ENTER after deploying rollback..."

bash tools/deployment/health-check.sh "${ENVIRONMENT}" || {
    echo -e "${RED}❌ Health check failed after rollback${NC}"
    exit 1
}

echo ""
echo -e "${GREEN}✅ Rollback completed successfully${NC}"
