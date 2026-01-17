#!/bin/bash
# Setup environment variables for integration tests with external infrastructure
# Issue #2541: Enable parallel test execution with Docker Compose

set -e

# Determine repository root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
SECRET_PATH="$REPO_ROOT/infra/secrets/admin.secret"

# Read admin password from secret file
if [ -f "$SECRET_PATH" ]; then
    ADMIN_PASSWORD=$(cat "$SECRET_PATH" | tr -d '\n\r')
    echo "✓ Loaded admin password from $SECRET_PATH"
else
    echo "❌ Admin password not found at $SECRET_PATH"
    echo "Run: cd infra/secrets && ./setup-secrets.sh"
    exit 1
fi

# Configure environment variables
export TEST_POSTGRES_CONNSTRING="Host=localhost;Port=5432;Database=test_shared;Username=admin;Password=$ADMIN_PASSWORD;Ssl Mode=Disable;Trust Server Certificate=true;Pooling=false;Timeout=10"
export TEST_REDIS_CONNSTRING="localhost:6379"

# Verify Docker Compose infrastructure is running
echo ""
echo "🔍 Checking Docker Compose infrastructure..."

cd "$REPO_ROOT/infra"

if docker compose ps postgres --format json 2>/dev/null | grep -q '"State":"running"'; then
    echo "✓ PostgreSQL running"
else
    echo "⚠️  PostgreSQL container not running"
    echo "Starting PostgreSQL..."
    docker compose up -d postgres
fi

if docker compose ps redis --format json 2>/dev/null | grep -q '"State":"running"'; then
    echo "✓ Redis running"
else
    echo "⚠️  Redis container not running"
    echo "Starting Redis..."
    docker compose up -d redis
fi

# Display configuration
echo ""
echo "✅ Environment configured for parallel integration tests"
echo ""
echo "TEST_POSTGRES_CONNSTRING: $TEST_POSTGRES_CONNSTRING"
echo "TEST_REDIS_CONNSTRING: $TEST_REDIS_CONNSTRING"

echo ""
echo "Run tests with:"
echo "  cd apps/api/tests/Api.Tests"
echo "  dotnet test --parallel --max-cpu-count 4"

echo ""
echo "For more details, see:"
echo "  docs/05-testing/INTEGRATION_TEST_OPTIMIZATION.md"
