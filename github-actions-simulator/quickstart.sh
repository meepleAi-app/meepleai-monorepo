#!/bin/bash
# GitHub Actions Simulator - Quick Start (Linux/Mac)

set -e

# Colors
CYAN='\033[0;36m'
GREEN='\033[0;32m'
YELLOW='\033[0;33m'
RED='\033[0;31m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

echo -e "${CYAN}🎭 GitHub Actions Simulator - Quick Start${NC}"
echo -e "${CYAN}=========================================${NC}"
echo ""

# Check Docker
echo -e "${YELLOW}🔍 Checking Docker...${NC}"
if command -v docker &> /dev/null; then
    echo -e "${GREEN}   ✅ Docker is installed${NC}"
else
    echo -e "${RED}   ❌ Docker is not installed or not running!${NC}"
    echo -e "${RED}   Please install Docker and try again.${NC}"
    exit 1
fi

# Check docker-compose
echo -e "${YELLOW}🔍 Checking docker-compose...${NC}"
if docker compose version &> /dev/null; then
    echo -e "${GREEN}   ✅ docker-compose is installed${NC}"
else
    echo -e "${RED}   ❌ docker-compose is not installed!${NC}"
    exit 1
fi

# Setup secrets
echo ""
echo -e "${YELLOW}🔐 Setting up secrets...${NC}"
if [ ! -f "config/.secrets" ]; then
    cp "config/.secrets.example" "config/.secrets"
    echo -e "${YELLOW}   ⚠️  Please edit config/.secrets with your actual API keys${NC}"
    echo -e "${YELLOW}   Press Enter to continue after editing secrets...${NC}"
    read
else
    echo -e "${GREEN}   ✅ config/.secrets already exists${NC}"
fi

# Build
echo ""
echo -e "${YELLOW}🏗️  Building Docker environment...${NC}"
echo -e "${GRAY}   (This may take 5-10 minutes on first run)${NC}"
docker compose build
echo -e "${GREEN}   ✅ Build complete!${NC}"

# Start services
echo ""
echo -e "${YELLOW}🚀 Starting services...${NC}"
docker compose up -d
echo -e "${GREEN}   ✅ Services started!${NC}"

# Wait for services
echo ""
echo -e "${YELLOW}⏳ Waiting for services to be ready...${NC}"
sleep 10

# Health check
echo ""
echo -e "${YELLOW}🏥 Running health check...${NC}"
docker compose exec act-runner health-check.sh || echo -e "${YELLOW}   ⚠️  Some services may not be ready yet${NC}"

# Summary
echo ""
echo -e "${CYAN}=========================================${NC}"
echo -e "${GREEN}✅ Setup complete!${NC}"
echo ""
echo -e "${CYAN}📚 Quick Reference:${NC}"
echo -e "   Shell:        docker compose exec act-runner bash"
echo -e "   Test CI:      make test-ci"
echo -e "   Test API:     make test-api"
echo -e "   View Logs:    make view-logs"
echo -e "   Health:       make health"
echo -e "   Stop:         make down"
echo ""
echo -e "${CYAN}🌐 Web Interfaces:${NC}"
echo -e "   Log Viewer:   http://localhost:9999"
echo ""
echo -e "${CYAN}📖 Documentation:${NC}"
echo -e "   README:       github-actions-simulator/README.md"
echo -e "   Help:         make help"
echo ""
echo -e "${CYAN}🎯 Try it now:${NC}"
echo -e "${YELLOW}   docker compose exec act-runner bash${NC}"
echo -e "${YELLOW}   run-workflow.sh .github/workflows/ci.yml${NC}"
echo -e "${CYAN}=========================================${NC}"
