#!/bin/bash
# Deploy to Production Environment
# Requires staging verification and additional safety checks

set -e
set -o pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Configuration
ENVIRONMENT="production"
DOCKER_REGISTRY="${DOCKER_REGISTRY:-ghcr.io/meepleai-app}"
IMAGE_TAG="${IMAGE_TAG:-$(git rev-parse --short HEAD)}"
PRODUCTION_HOST="${PRODUCTION_HOST:-meepleai.dev}"
REQUIRE_STAGING_APPROVAL="${REQUIRE_STAGING_APPROVAL:-true}"

echo -e "${CYAN}🚀 MeepleAI Production Deployment${NC}"
echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${RED}⚠️  PRODUCTION DEPLOYMENT - PROCEED WITH CAUTION ⚠️${NC}"
echo -e "${RED}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "Environment: ${ENVIRONMENT}"
echo "Image Tag: ${IMAGE_TAG}"
echo "Production Host: ${PRODUCTION_HOST}"
echo ""

# Safety confirmation
echo -e "${YELLOW}This will deploy to PRODUCTION.${NC}"
echo -e "${YELLOW}Have you verified everything works on staging?${NC}"
read -p "Type 'DEPLOY TO PRODUCTION' to continue: " CONFIRMATION

if [[ "$CONFIRMATION" != "DEPLOY TO PRODUCTION" ]]; then
    echo -e "${RED}Deployment cancelled${NC}"
    exit 1
fi

# Pre-flight checks
echo ""
echo -e "${YELLOW}📋 Running pre-flight checks...${NC}"

# Check git status
if [[ -n $(git status --porcelain) ]]; then
    echo -e "${RED}❌ Working directory has uncommitted changes${NC}"
    exit 1
fi
echo -e "${GREEN}✓${NC} Git working directory clean"

# Must be on main branch
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [[ "$CURRENT_BRANCH" != "main" ]]; then
    echo -e "${RED}❌ Production deployments must be from 'main' branch${NC}"
    echo "Current branch: ${CURRENT_BRANCH}"
    exit 1
fi
echo -e "${GREEN}✓${NC} On main branch"

# Check if images exist
echo "Verifying Docker images exist..."
if ! docker manifest inspect "${DOCKER_REGISTRY}/meepleai-api:${IMAGE_TAG}" &> /dev/null; then
    echo -e "${RED}❌ API image ${IMAGE_TAG} not found in registry${NC}"
    exit 1
fi
if ! docker manifest inspect "${DOCKER_REGISTRY}/meepleai-web:${IMAGE_TAG}" &> /dev/null; then
    echo -e "${RED}❌ Web image ${IMAGE_TAG} not found in registry${NC}"
    exit 1
fi
echo -e "${GREEN}✓${NC} Docker images verified in registry"

# Backup production database
echo ""
echo -e "${YELLOW}💾 Creating production database backup...${NC}"
bash tools/deployment/backup-database.sh "${ENVIRONMENT}" || {
    echo -e "${RED}❌ Production backup failed${NC}"
    echo "Cannot proceed without backup"
    exit 1
}
echo -e "${GREEN}✓${NC} Production backup created"

# Create deployment record
DEPLOYMENT_ID="prod-$(date +%Y%m%d-%H%M%S)-${IMAGE_TAG}"
echo "${DEPLOYMENT_ID}" > /tmp/meepleai-last-production-deployment.txt

# Deploy to production
echo ""
echo -e "${YELLOW}🚢 Deploying to production...${NC}"

# Tag images for production
docker pull "${DOCKER_REGISTRY}/meepleai-api:${IMAGE_TAG}"
docker tag "${DOCKER_REGISTRY}/meepleai-api:${IMAGE_TAG}" \
    "${DOCKER_REGISTRY}/meepleai-api:production-latest"
docker push "${DOCKER_REGISTRY}/meepleai-api:production-latest"

docker pull "${DOCKER_REGISTRY}/meepleai-web:${IMAGE_TAG}"
docker tag "${DOCKER_REGISTRY}/meepleai-web:${IMAGE_TAG}" \
    "${DOCKER_REGISTRY}/meepleai-web:production-latest"
docker push "${DOCKER_REGISTRY}/meepleai-web:production-latest"

echo -e "${GREEN}✓${NC} Production images tagged and pushed"

# Deploy via your infrastructure (Kubernetes, Docker Swarm, etc.)
echo ""
echo -e "${YELLOW}📝 Deployment Instructions:${NC}"
echo ""
echo "1. SSH to production server:"
echo "   ssh production.meepleai.dev"
echo ""
echo "2. Pull latest images:"
echo "   docker pull ${DOCKER_REGISTRY}/meepleai-api:production-latest"
echo "   docker pull ${DOCKER_REGISTRY}/meepleai-web:production-latest"
echo ""
echo "3. Run database migrations:"
echo "   docker exec meepleai-api dotnet ef database update"
echo ""
echo "4. Rolling update (zero downtime):"
echo "   docker-compose up -d --no-deps api"
echo "   sleep 30  # Wait for health check"
echo "   docker-compose up -d --no-deps web"
echo ""
echo -e "${YELLOW}⚠️  Or use your orchestration tool (k8s apply, etc.)${NC}"
echo ""

# Wait for manual deployment confirmation
read -p "Press ENTER after deploying to production..."

# Run health checks
echo ""
echo -e "${YELLOW}🏥 Running production health checks...${NC}"
sleep 10

bash tools/deployment/health-check.sh "${ENVIRONMENT}" || {
    echo -e "${RED}❌ Production health checks failed${NC}"
    echo -e "${RED}IMMEDIATE ACTION REQUIRED${NC}"
    echo "Run rollback: bash tools/deployment/rollback.sh production"
    exit 1
}

# Run smoke tests
echo ""
echo -e "${YELLOW}💨 Running production smoke tests...${NC}"
bash tools/deployment/smoke-test.sh "${ENVIRONMENT}" || {
    echo -e "${RED}❌ Production smoke tests failed${NC}"
    echo "Consider rollback: bash tools/deployment/rollback.sh production"
    exit 1
}

# Success
echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}✅ Production deployment successful!${NC}"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "Deployment ID: ${DEPLOYMENT_ID}"
echo "Deployed version: ${IMAGE_TAG}"
echo "Production URL: https://${PRODUCTION_HOST}"
echo ""
echo "Monitor for the next 30 minutes:"
echo "- Logs: bash tools/deployment/view-logs.sh production"
echo "- Metrics: Check Grafana dashboards"
echo "- Alerts: Monitor Slack/email"
echo ""
echo "If issues occur:"
echo "- Rollback: bash tools/deployment/rollback.sh production"
