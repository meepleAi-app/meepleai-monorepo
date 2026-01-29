#!/bin/bash
# Run E2E Admin Setup Tests with Docker services (Linux/Mac)

set -e

echo -e "\n🐳 E2E Admin Setup Tests - Docker Mode\n"

# Navigate to infra directory
cd ../../infra

# Step 1: Rebuild API container
echo "📦 Step 1: Rebuilding API container..."
docker compose build api
echo -e "✅ API container rebuilt\n"

# Step 2: Restart API to use new build
echo "🔄 Step 2: Restarting API service..."
docker compose restart api

# Wait for API health
echo "⏳ Waiting for API to be healthy..."
max_wait=60
waited=0
while [ $waited -lt $max_wait ]; do
    if curl -sf http://localhost:8080/api/v1/health > /dev/null 2>&1; then
        echo -e "✅ API is healthy\n"
        break
    fi
    sleep 2
    waited=$((waited + 2))
done

if [ $waited -ge $max_wait ]; then
    echo "❌ API health check timeout!"
    exit 1
fi

# Step 3: Verify all services healthy
echo "🔍 Step 3: Verifying all services..."
services=("postgres" "redis" "qdrant" "api" "web")

for service in "${services[@]}"; do
    status=$(docker ps --filter "name=meepleai-$service" --format "{{.Status}}")
    if [[ $status == *"healthy"* ]] || [[ $status == *"Up"* ]]; then
        echo "  ✅ $service - healthy"
    else
        echo "  ❌ $service - not running!"
        exit 1
    fi
done

echo -e "\n✅ All services healthy\n"

# Step 4: Generate test PDFs if needed
cd ../apps/web
if [ ! -f "e2e/test-data/pandemic_rulebook.pdf" ]; then
    echo "📄 Generating mock PDFs..."
    node e2e/scripts/generate-mock-pdfs.js
fi

# Step 5: Run Playwright tests
echo "🧪 Step 4: Running E2E tests..."
echo -e "   Using Docker services (http://localhost:3000 / :8080)\n"

# Set environment to skip webServer
export PLAYWRIGHT_SKIP_WEB_SERVER=1

# Parse arguments
UI_MODE=false
TEST_FILE=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --ui)
            UI_MODE=true
            shift
            ;;
        --test)
            TEST_FILE="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done

# Build test command
if [ -n "$TEST_FILE" ]; then
    TEST_PATH="e2e/admin-first-time-setup/$TEST_FILE.spec.ts"
else
    TEST_PATH="e2e/admin-first-time-setup"
fi

REPORTER="--reporter=list"
if [ "$UI_MODE" = true ]; then
    REPORTER="--ui"
fi

# Execute tests
pnpm exec playwright test "$TEST_PATH" --project=admin-first-time-setup $REPORTER

EXIT_CODE=$?

# Report results
echo ""
if [ $EXIT_CODE -eq 0 ]; then
    echo "✅ Tests completed successfully!"
else
    echo "❌ Tests failed (exit code: $EXIT_CODE)"
    echo "Check test results in: test-results/"
fi

exit $EXIT_CODE
