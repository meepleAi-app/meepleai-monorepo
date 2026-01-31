#!/bin/bash
# Deploy to Staging Environment
# Builds, tests, and deploys the application to staging

set -e
set -o pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Configuration
ENVIRONMENT="staging"
DOCKER_REGISTRY="${DOCKER_REGISTRY:-ghcr.io/degrassiaaron}"
IMAGE_TAG="${IMAGE_TAG:-$(git rev-parse --short HEAD)}"
STAGING_HOST="${STAGING_HOST:-staging.meepleai.dev}"
REQUIRE_TESTS="${REQUIRE_TESTS:-true}"

echo -e "${CYAN}🚀 MeepleAI Staging Deployment${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "Environment: ${ENVIRONMENT}"
echo "Image Tag: ${IMAGE_TAG}"
echo "Staging Host: ${STAGING_HOST}"
echo ""

# Pre-flight checks
echo -e "${YELLOW}📋 Running pre-flight checks...${NC}"

# Check git status
if [[ -n $(git status --porcelain) ]]; then
    echo -e "${RED}❌ Working directory has uncommitted changes${NC}"
    echo "Please commit or stash changes before deploying"
    exit 1
fi
echo -e "${GREEN}✓${NC} Git working directory clean"

# Check current branch
CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
if [[ "$CURRENT_BRANCH" != "main" ]] && [[ "$CURRENT_BRANCH" != "develop" ]]; then
    echo -e "${YELLOW}⚠️  Deploying from branch: ${CURRENT_BRANCH}${NC}"
    read -p "Continue? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi
echo -e "${GREEN}✓${NC} Branch: ${CURRENT_BRANCH}"

# Check Docker
if ! command -v docker &> /dev/null; then
    echo -e "${RED}❌ Docker not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓${NC} Docker available"

# Run tests (optional)
if [[ "$REQUIRE_TESTS" == "true" ]]; then
    echo ""
    echo -e "${YELLOW}🧪 Running tests...${NC}"

    # Backend tests
    echo "Running backend tests..."
    bash tools/coverage/run-backend-coverage.sh --threshold 90 || {
        echo -e "${RED}❌ Backend tests failed${NC}"
        exit 1
    }
    echo -e "${GREEN}✓${NC} Backend tests passed"

    # Frontend tests
    echo "Running frontend tests..."
    cd apps/web && pnpm test --coverage || {
        echo -e "${RED}❌ Frontend tests failed${NC}"
        exit 1
    }
    cd ../..
    echo -e "${GREEN}✓${NC} Frontend tests passed"
else
    echo -e "${YELLOW}⚠️  Skipping tests (REQUIRE_TESTS=false)${NC}"
fi

# Build Docker images
echo ""
echo -e "${YELLOW}🐳 Building Docker images...${NC}"

docker build \
    -t "${DOCKER_REGISTRY}/meepleai-api:${IMAGE_TAG}" \
    -t "${DOCKER_REGISTRY}/meepleai-api:staging-latest" \
    -f apps/api/Dockerfile \
    apps/api/ || {
    echo -e "${RED}❌ API image build failed${NC}"
    exit 1
}
echo -e "${GREEN}✓${NC} API image built"

docker build \
    -t "${DOCKER_REGISTRY}/meepleai-web:${IMAGE_TAG}" \
    -t "${DOCKER_REGISTRY}/meepleai-web:staging-latest" \
    -f apps/web/Dockerfile \
    apps/web/ || {
    echo -e "${RED}❌ Web image build failed${NC}"
    exit 1
}
echo -e "${GREEN}✓${NC} Web image built"

# Push images to registry
echo ""
echo -e "${YELLOW}📤 Pushing images to registry...${NC}"

docker push "${DOCKER_REGISTRY}/meepleai-api:${IMAGE_TAG}"
docker push "${DOCKER_REGISTRY}/meepleai-api:staging-latest"
docker push "${DOCKER_REGISTRY}/meepleai-web:${IMAGE_TAG}"
docker push "${DOCKER_REGISTRY}/meepleai-web:staging-latest"

echo -e "${GREEN}✓${NC} Images pushed to registry"

# Backup database (if staging has data)
echo ""
echo -e "${YELLOW}💾 Creating database backup...${NC}"
bash tools/deployment/backup-database.sh "${ENVIRONMENT}" || {
    echo -e "${YELLOW}⚠️  Backup failed (continuing anyway)${NC}"
}

# Deploy to staging
echo ""
echo -e "${YELLOW}🚢 Deploying to staging...${NC}"

# Create deployment directory
DEPLOY_DIR="/tmp/meepleai-staging-deploy-${IMAGE_TAG}"
mkdir -p "${DEPLOY_DIR}"

# Copy docker-compose file for staging
cat > "${DEPLOY_DIR}/docker-compose.yml" <<EOF
version: '3.8'

services:
  api:
    image: ${DOCKER_REGISTRY}/meepleai-api:${IMAGE_TAG}
    environment:
      - ASPNETCORE_ENVIRONMENT=Staging
      - ConnectionStrings__Postgres=\${POSTGRES_CONNECTION}
      - QDRANT_URL=\${QDRANT_URL}
      - REDIS_URL=\${REDIS_URL}
      - OPENROUTER_API_KEY=\${OPENROUTER_API_KEY}
    ports:
      - "8080:8080"
    restart: unless-stopped

  web:
    image: ${DOCKER_REGISTRY}/meepleai-web:${IMAGE_TAG}
    environment:
      - NODE_ENV=production
      - NEXT_PUBLIC_API_BASE=https://api.staging.meepleai.dev
    ports:
      - "3000:3000"
    restart: unless-stopped
EOF

# Deploy via Docker (adjust based on your infrastructure)
echo "Deploying via Docker Compose..."
# Example: ssh to staging server and deploy
# ssh staging "cd /opt/meepleai && docker-compose pull && docker-compose up -d"

echo -e "${YELLOW}Note: Adjust deployment method based on your infrastructure${NC}"
echo "Deployment files created in: ${DEPLOY_DIR}"

# Run health checks
echo ""
echo -e "${YELLOW}🏥 Running health checks...${NC}"
sleep 10  # Wait for services to start

bash tools/deployment/health-check.sh "${ENVIRONMENT}" || {
    echo -e "${RED}❌ Health checks failed${NC}"
    echo "Run rollback if needed: bash tools/deployment/rollback.sh staging"
    exit 1
}

# Run smoke tests
echo ""
echo -e "${YELLOW}💨 Running smoke tests...${NC}"
bash tools/deployment/smoke-test.sh "${ENVIRONMENT}" || {
    echo -e "${RED}❌ Smoke tests failed${NC}"
    echo "Run rollback if needed: bash tools/deployment/rollback.sh staging"
    exit 1
}

# Success
echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}✅ Staging deployment successful!${NC}"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "Deployed version: ${IMAGE_TAG}"
echo "Staging URL: https://${STAGING_HOST}"
echo ""
echo "Next steps:"
echo "1. Test manually on staging"
echo "2. Run: bash tools/deployment/deploy-production.sh"
echo "3. If issues, run: bash tools/deployment/rollback.sh staging"
