#!/bin/bash

# Backend Code Coverage Runner (Docker-based)
# Runs .NET tests with code coverage using Docker
# Usage: ./tools/run-backend-coverage-docker.sh [options]
#
# This script is useful when .NET SDK is not installed locally
# It uses Docker to run tests in an isolated environment

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default options
THRESHOLD=90

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --threshold)
      THRESHOLD="$2"
      shift 2
      ;;
    --help)
      echo "Backend Code Coverage Runner (Docker-based)"
      echo ""
      echo "Usage: ./tools/run-backend-coverage-docker.sh [options]"
      echo ""
      echo "Options:"
      echo "  --threshold N   Set coverage threshold (default: 90)"
      echo "  --help          Show this help message"
      echo ""
      echo "This script uses Docker to run tests with coverage collection."
      echo "Results are written to: apps/api/tests/Api.Tests/coverage/"
      exit 0
      ;;
    *)
      echo -e "${RED}Unknown option: $1${NC}"
      echo "Use --help for usage information"
      exit 1
      ;;
  esac
done

echo -e "${BLUE}==================================================${NC}"
echo -e "${BLUE}  MeepleAI Backend Code Coverage (Docker)${NC}"
echo -e "${BLUE}==================================================${NC}"
echo ""

# Check if docker is installed
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Error: docker not found${NC}"
    echo "Please install Docker: https://docs.docker.com/get-docker/"
    exit 1
fi

# Get the repository root
REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"

echo -e "${YELLOW}Step 1/4: Starting required services...${NC}"
cd "$REPO_ROOT/infra"

# Start only the services needed for tests
docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis

echo -e "${GREEN}✓ Services started${NC}"
echo ""

echo -e "${YELLOW}Step 2/4: Waiting for services to be ready...${NC}"

# Wait for PostgreSQL
echo -n "Waiting for PostgreSQL..."
for i in {1..30}; do
    if docker compose exec -T meepleai-postgres pg_isready -U meeple > /dev/null 2>&1; then
        echo -e " ${GREEN}ready${NC}"
        break
    fi
    echo -n "."
    sleep 1
done

# Wait for Qdrant
echo -n "Waiting for Qdrant..."
for i in {1..20}; do
    if curl -sf http://localhost:6333/healthz > /dev/null 2>&1; then
        echo -e " ${GREEN}ready${NC}"
        break
    fi
    echo -n "."
    sleep 1
done

# Wait for Redis
echo -n "Waiting for Redis..."
for i in {1..20}; do
    if docker compose exec -T meepleai-redis redis-cli ping > /dev/null 2>&1; then
        echo -e " ${GREEN}ready${NC}"
        break
    fi
    echo -n "."
    sleep 1
done

echo ""

echo -e "${YELLOW}Step 3/4: Running tests with coverage in Docker container...${NC}"
echo -e "${BLUE}Threshold: ${THRESHOLD}%${NC}"
echo ""

# Clean previous coverage data
rm -rf "$REPO_ROOT/apps/api/tests/Api.Tests/coverage/"

# Run tests in Docker container
docker run --rm \
    --network infra_meepleai \
    -v "$REPO_ROOT/apps/api:/workspace" \
    -w /workspace \
    -e CI=true \
    -e OPENROUTER_API_KEY=test-key-for-ci \
    -e QDRANT_URL=http://meepleai-qdrant:6333 \
    -e REDIS_URL=meepleai-redis:6379 \
    -e "ConnectionStrings__Postgres=Host=meepleai-postgres;Port=5432;Database=meepleai_test;Username=meeple;Password=meeplepass;Maximum Pool Size=100" \
    -e OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318 \
    mcr.microsoft.com/dotnet/sdk:9.0 \
    bash -c "
        echo 'Installing dependencies...'
        apt-get update -qq && apt-get install -y -qq libgdiplus bc > /dev/null 2>&1

        echo 'Restoring packages...'
        dotnet restore

        echo 'Building...'
        dotnet build --no-restore

        echo 'Running tests with coverage...'
        dotnet test \
            --logger 'console;verbosity=normal' \
            --no-build \
            --blame-hang-timeout 5m \
            -p:CollectCoverage=true \
            -p:CoverletOutputFormat=cobertura \
            -p:CoverletOutput=./tests/Api.Tests/coverage/ \
            -p:Threshold=${THRESHOLD} \
            -p:ThresholdType=line \
            -p:Exclude='[*]Api.Migrations.*' \
            -- xUnit.Parallelization.MaxParallelThreads=2

        # Generate summary
        if [ -f tests/Api.Tests/coverage/coverage.cobertura.xml ]; then
            COVERAGE_PCT=\$(grep -oP 'line-rate=\"\K[0-9.]+' tests/Api.Tests/coverage/coverage.cobertura.xml | head -1)
            COVERAGE_PCT=\$(echo \"\$COVERAGE_PCT * 100\" | bc | cut -d'.' -f1)
            echo \"\"
            echo \"Coverage: \${COVERAGE_PCT}%\"
        fi
    "

COVERAGE_EXIT_CODE=$?

echo ""

if [ $COVERAGE_EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓ Tests passed with coverage threshold of ${THRESHOLD}%${NC}"
else
    echo -e "${RED}✗ Tests failed or coverage below ${THRESHOLD}%${NC}"
fi

echo ""
echo -e "${YELLOW}Step 4/4: Processing results...${NC}"

# Check if coverage file exists
if [ -f "$REPO_ROOT/apps/api/tests/Api.Tests/coverage/coverage.cobertura.xml" ]; then
    echo -e "${GREEN}✓ Coverage data generated${NC}"

    # Extract coverage percentage
    COVERAGE_PCT=$(grep -oP 'line-rate="\K[0-9.]+' "$REPO_ROOT/apps/api/tests/Api.Tests/coverage/coverage.cobertura.xml" | head -1)
    COVERAGE_PCT=$(echo "$COVERAGE_PCT * 100" | bc | cut -d'.' -f1)

    echo ""
    echo -e "${BLUE}==================================================${NC}"
    echo -e "${BLUE}  Coverage Summary${NC}"
    echo -e "${BLUE}==================================================${NC}"
    echo -e "Line Coverage: ${GREEN}${COVERAGE_PCT}%${NC}"
    echo -e "Threshold:     ${THRESHOLD}%"
    echo -e "${BLUE}==================================================${NC}"
    echo ""
    echo -e "${BLUE}Coverage file:${NC}"
    echo -e "  • Cobertura XML: apps/api/tests/Api.Tests/coverage/coverage.cobertura.xml"
    echo ""
    echo -e "${YELLOW}Note: To generate an HTML report, install reportgenerator:${NC}"
    echo -e "  dotnet tool install -g dotnet-reportgenerator-globaltool"
    echo -e "  reportgenerator \\"
    echo -e "    -reports:apps/api/tests/Api.Tests/coverage/coverage.cobertura.xml \\"
    echo -e "    -targetdir:apps/api/tests/Api.Tests/coverage-report \\"
    echo -e "    -reporttypes:Html"
else
    echo -e "${RED}✗ Coverage data not found${NC}"
    COVERAGE_EXIT_CODE=1
fi

echo ""
echo -e "${BLUE}==================================================${NC}"
echo -e "${BLUE}  Coverage Analysis Complete${NC}"
echo -e "${BLUE}==================================================${NC}"
echo ""

# Exit with the same code as the test run
exit $COVERAGE_EXIT_CODE
