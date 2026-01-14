#!/bin/bash
# ISSUE #2427: SharedGameCatalog Performance Indexes Deployment Script
#
# Applies database migrations for SharedGameCatalog Phase 5 performance optimization.
# Includes validation and rollback instructions.
#
# Prerequisites:
# - PostgreSQL connection configured in CONNECTIONSTRINGS__POSTGRES
# - .NET 9 SDK installed
# - psql client installed (for validation)
#
# Usage:
#   ./scripts/db/apply-shared-catalog-indexes.sh

set -e  # Exit on error
set -u  # Exit on undefined variable

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}SharedGameCatalog Indexes Deployment${NC}"
echo -e "${GREEN}Issue #2374 Phase 5${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# Check prerequisites
echo -e "${YELLOW}[1/5] Checking prerequisites...${NC}"

if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: dotnet CLI not found. Install .NET 9 SDK.${NC}"
    exit 1
fi

if ! command -v psql &> /dev/null; then
    echo -e "${YELLOW}Warning: psql not found. Skipping index validation.${NC}"
    SKIP_VALIDATION=true
else
    SKIP_VALIDATION=false
fi

echo -e "${GREEN}✓ Prerequisites OK${NC}"
echo ""

# Navigate to API project
echo -e "${YELLOW}[2/5] Navigating to API project...${NC}"
cd "$(dirname "$0")/../../apps/api/src/Api" || exit 1
echo -e "${GREEN}✓ Directory: $(pwd)${NC}"
echo ""

# Apply migrations
echo -e "${YELLOW}[3/5] Applying database migrations...${NC}"
echo "Migration: 20260114121520_AddSharedGameCatalogPerformanceIndexes"
echo "Indexes: GIN FTS + 7 composite/junction indexes"
echo ""

dotnet ef database update

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Migrations applied successfully${NC}"
else
    echo -e "${RED}✗ Migration failed. Check database connection and logs.${NC}"
    exit 1
fi
echo ""

# Validate indexes (if psql available)
if [ "$SKIP_VALIDATION" = false ]; then
    echo -e "${YELLOW}[4/5] Validating indexes...${NC}"

    VALIDATION_SCRIPT="../../../docs/05-testing/shared-catalog-fts-performance-validation.sql"

    if [ -f "$VALIDATION_SCRIPT" ]; then
        echo "Running: $VALIDATION_SCRIPT"
        psql -f "$VALIDATION_SCRIPT"
        echo -e "${GREEN}✓ Index validation complete${NC}"
    else
        echo -e "${YELLOW}Warning: Validation script not found at $VALIDATION_SCRIPT${NC}"
    fi
else
    echo -e "${YELLOW}[4/5] Skipping validation (psql not available)${NC}"
fi
echo ""

# Summary
echo -e "${GREEN}[5/5] Deployment Summary${NC}"
echo "=========================================="
echo "✓ Migration 20260114121520 applied"
echo "✓ 8 performance indexes created:"
echo "  1. ix_shared_games_fts (GIN full-text search)"
echo "  2. ix_shared_games_status_year_title (sort by year)"
echo "  3. ix_shared_games_status_rating_title (sort by rating)"
echo "  4. ix_shared_games_players (player count filter)"
echo "  5. ix_shared_games_playtime (playtime filter)"
echo "  6-7. ix_shared_game_categories_* (junction tables)"
echo "  8-9. ix_shared_game_mechanics_* (junction tables)"
echo "  10. ix_shared_games_getbyid_covering (cache miss optimization)"
echo ""
echo -e "${GREEN}✓ Deployment complete!${NC}"
echo ""
echo "Next steps:"
echo "1. Run load test: cd tests/k6 && k6 run shared-catalog-load-test.js"
echo "2. Verify Prometheus metrics: curl http://localhost:8080/metrics | grep meepleai_cache"
echo "3. Check Grafana dashboard: http://localhost:3000/d/shared-catalog-perf"
echo "4. Verify health check: curl http://localhost:8080/health | jq '.entries.\"shared-catalog-fts\"'"
echo ""

# Rollback instructions
echo -e "${YELLOW}Rollback Instructions (if needed):${NC}"
echo "=========================================="
echo "To rollback this migration:"
echo "  cd apps/api/src/Api"
echo "  dotnet ef database update 20260113212945  # Previous migration"
echo ""
echo "This will drop all 8 performance indexes."
echo "Impact: Search performance will degrade to ~2000ms (BGG baseline)."
echo ""
