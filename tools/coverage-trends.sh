#!/bin/bash
# Coverage Trend Tracking Script
# Tracks test coverage over time for MeepleAI monorepo

set -e

# Configuration
TIMESTAMP=$(date +%Y-%m-%d-%H%M%S)
COVERAGE_DIR="coverage-history"
LOG_FILE="$COVERAGE_DIR/trends.log"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Create coverage history directory
mkdir -p "$COVERAGE_DIR"

echo -e "${BLUE}=== MeepleAI Coverage Trend Tracker ===${NC}"
echo -e "${BLUE}Timestamp: $TIMESTAMP${NC}\n"

# Function to extract coverage metrics from JSON
extract_metrics() {
    local json_file=$1
    local project=$2

    if [ -f "$json_file" ]; then
        echo -e "${GREEN}📊 $project Coverage Snapshot${NC}"

        # Extract key metrics using jq if available, otherwise use grep
        if command -v jq &> /dev/null; then
            local statements=$(jq -r '.total.statements.pct' "$json_file" 2>/dev/null || echo "N/A")
            local branches=$(jq -r '.total.branches.pct' "$json_file" 2>/dev/null || echo "N/A")
            local functions=$(jq -r '.total.functions.pct' "$json_file" 2>/dev/null || echo "N/A")
            local lines=$(jq -r '.total.lines.pct' "$json_file" 2>/dev/null || echo "N/A")

            echo "  Statements: $statements%"
            echo "  Branches:   $branches%"
            echo "  Functions:  $functions%"
            echo "  Lines:      $lines%"

            # Log to trends file
            echo "[$TIMESTAMP] $project - Statements: $statements% | Branches: $branches% | Functions: $functions% | Lines: $lines%" >> "$LOG_FILE"
        else
            echo "  (jq not installed - install for detailed metrics)"
            cat "$json_file" >> "$LOG_FILE"
        fi

        # Copy full report to history
        cp "$json_file" "$COVERAGE_DIR/coverage-${project}-${TIMESTAMP}.json"
        echo -e "${GREEN}✓ Snapshot saved: $COVERAGE_DIR/coverage-${project}-${TIMESTAMP}.json${NC}\n"
    else
        echo -e "${YELLOW}⚠ Coverage file not found: $json_file${NC}\n"
    fi
}

# Frontend Coverage
echo -e "${BLUE}--- Frontend (Jest) ---${NC}"
cd apps/web

if [ "$1" != "--no-run" ]; then
    echo "Running frontend tests with coverage..."
    pnpm test:coverage --silent --json --outputFile="../../$COVERAGE_DIR/coverage-web-${TIMESTAMP}-raw.json" 2>&1 | tail -20
fi

# Check for Jest coverage summary
if [ -f "coverage/coverage-summary.json" ]; then
    extract_metrics "coverage/coverage-summary.json" "Frontend"
else
    echo -e "${YELLOW}⚠ Frontend coverage summary not found${NC}\n"
fi

cd ../..

# Backend Coverage
echo -e "${BLUE}--- Backend (.NET) ---${NC}"
cd apps/api

if [ "$1" != "--no-run" ]; then
    echo "Running backend tests with coverage..."
    dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput="../../$COVERAGE_DIR/coverage-api-${TIMESTAMP}.json" --verbosity quiet 2>&1 | tail -20
fi

# Extract backend coverage if available
if [ -f "../../$COVERAGE_DIR/coverage-api-${TIMESTAMP}.json" ]; then
    extract_metrics "../../$COVERAGE_DIR/coverage-api-${TIMESTAMP}.json" "Backend"
else
    echo -e "${YELLOW}⚠ Backend coverage file not generated${NC}\n"
fi

cd ../..

# Summary
echo -e "${BLUE}=== Summary ===${NC}"
echo "Coverage snapshots saved to: $COVERAGE_DIR/"
echo "Trend log: $LOG_FILE"
echo ""
echo -e "${GREEN}✓ Coverage trend tracking complete!${NC}"

# Show last 5 entries from log
if [ -f "$LOG_FILE" ]; then
    echo -e "\n${BLUE}Recent Coverage History:${NC}"
    tail -5 "$LOG_FILE"
fi

# Usage instructions
if [ "$1" == "--help" ] || [ "$1" == "-h" ]; then
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --no-run    Use existing coverage data (skip test execution)"
    echo "  --help, -h  Show this help message"
    echo ""
    echo "Examples:"
    echo "  $0                  # Run tests and capture coverage"
    echo "  $0 --no-run         # Use existing coverage files"
fi
