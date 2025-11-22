#!/bin/bash

# Frontend Code Coverage Runner
# Runs Jest tests with code coverage and generates HTML reports
# Usage: ./tools/run-frontend-coverage.sh [options]
#
# Options:
#   --html          Generate HTML coverage report (default)
#   --threshold N   Set coverage threshold (default: 90)
#   --open          Open HTML report in browser
#   --watch         Run coverage in watch mode
#   --help          Show this help message

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default options
GENERATE_HTML=true
OPEN_REPORT=false
WATCH_MODE=false
THRESHOLD=90

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --html)
      GENERATE_HTML=true
      shift
      ;;
    --threshold)
      THRESHOLD="$2"
      shift 2
      ;;
    --open)
      GENERATE_HTML=true
      OPEN_REPORT=true
      shift
      ;;
    --watch)
      WATCH_MODE=true
      shift
      ;;
    --help)
      echo "Frontend Code Coverage Runner"
      echo ""
      echo "Usage: ./tools/run-frontend-coverage.sh [options]"
      echo ""
      echo "Options:"
      echo "  --html          Generate HTML coverage report (default)"
      echo "  --threshold N   Set coverage threshold (default: 90)"
      echo "  --open          Open HTML report in browser"
      echo "  --watch         Run coverage in watch mode"
      echo "  --help          Show this help message"
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
echo -e "${BLUE}  MeepleAI Frontend Code Coverage Runner${NC}"
echo -e "${BLUE}==================================================${NC}"
echo ""

# Check if pnpm is installed
if ! command -v pnpm &> /dev/null; then
    echo -e "${RED}Error: pnpm not found${NC}"
    echo "Please install pnpm: npm install -g pnpm"
    exit 1
fi

# Navigate to web directory
cd "$(dirname "$0")/../apps/web" || exit 1

echo -e "${YELLOW}Step 1/4: Cleaning previous coverage data...${NC}"
rm -rf coverage/
echo -e "${GREEN}✓ Cleaned${NC}"
echo ""

echo -e "${YELLOW}Step 2/4: Installing dependencies (if needed)...${NC}"
if [ ! -d "node_modules" ]; then
    echo "Installing dependencies..."
    pnpm install
    echo -e "${GREEN}✓ Dependencies installed${NC}"
else
    echo -e "${GREEN}✓ Dependencies already installed${NC}"
fi
echo ""

echo -e "${YELLOW}Step 3/4: Running tests with coverage collection...${NC}"
echo -e "${BLUE}Threshold: ${THRESHOLD}% (branches, functions, lines, statements)${NC}"
echo ""

# Set coverage threshold environment variables
export COVERAGE_THRESHOLD_BRANCHES=${THRESHOLD}
export COVERAGE_THRESHOLD_FUNCTIONS=${THRESHOLD}
export COVERAGE_THRESHOLD_LINES=${THRESHOLD}
export COVERAGE_THRESHOLD_STATEMENTS=${THRESHOLD}

# Run tests with coverage
if [ "$WATCH_MODE" = true ]; then
    echo -e "${BLUE}Running in watch mode (press Ctrl+C to exit)...${NC}"
    pnpm test:coverage -- --watch
    exit 0
else
    # Run tests with coverage
    set +e  # Don't exit on test failure, we want to show coverage anyway
    pnpm test:coverage
    COVERAGE_EXIT_CODE=$?
    set -e
fi

if [ $COVERAGE_EXIT_CODE -eq 0 ]; then
    echo ""
    echo -e "${GREEN}✓ Tests passed with coverage threshold of ${THRESHOLD}%${NC}"
else
    echo ""
    echo -e "${RED}✗ Tests failed or coverage below ${THRESHOLD}%${NC}"
fi

echo ""
echo -e "${YELLOW}Step 4/4: Processing coverage results...${NC}"

# Check if coverage was generated
if [ -d "coverage" ]; then
    echo -e "${GREEN}✓ Coverage data generated${NC}"

    # Extract coverage summary from coverage-summary.json
    if [ -f "coverage/coverage-summary.json" ]; then
        # Use jq if available, otherwise use grep/sed
        if command -v jq &> /dev/null; then
            LINES_PCT=$(jq '.total.lines.pct' coverage/coverage-summary.json)
            STATEMENTS_PCT=$(jq '.total.statements.pct' coverage/coverage-summary.json)
            FUNCTIONS_PCT=$(jq '.total.functions.pct' coverage/coverage-summary.json)
            BRANCHES_PCT=$(jq '.total.branches.pct' coverage/coverage-summary.json)
        else
            # Fallback: parse JSON manually (less reliable but works without jq)
            LINES_PCT=$(grep -A 3 '"total"' coverage/coverage-summary.json | grep '"lines"' -A 3 | grep '"pct"' | sed 's/.*: \([0-9.]*\).*/\1/')
            STATEMENTS_PCT=$(grep -A 3 '"total"' coverage/coverage-summary.json | grep '"statements"' -A 3 | grep '"pct"' | sed 's/.*: \([0-9.]*\).*/\1/')
            FUNCTIONS_PCT=$(grep -A 3 '"total"' coverage/coverage-summary.json | grep '"functions"' -A 3 | grep '"pct"' | sed 's/.*: \([0-9.]*\).*/\1/')
            BRANCHES_PCT=$(grep -A 3 '"total"' coverage/coverage-summary.json | grep '"branches"' -A 3 | grep '"pct"' | sed 's/.*: \([0-9.]*\).*/\1/')
        fi

        echo ""
        echo -e "${BLUE}==================================================${NC}"
        echo -e "${BLUE}  Coverage Summary${NC}"
        echo -e "${BLUE}==================================================${NC}"
        echo -e "Lines:       ${GREEN}${LINES_PCT}%${NC}"
        echo -e "Statements:  ${GREEN}${STATEMENTS_PCT}%${NC}"
        echo -e "Functions:   ${GREEN}${FUNCTIONS_PCT}%${NC}"
        echo -e "Branches:    ${GREEN}${BRANCHES_PCT}%${NC}"
        echo -e "Threshold:   ${THRESHOLD}%"
        echo -e "${BLUE}==================================================${NC}"
        echo ""
    fi

    # Display coverage report locations
    echo -e "${BLUE}Coverage files:${NC}"
    if [ -f "coverage/lcov.info" ]; then
        echo -e "  • LCOV:        coverage/lcov.info"
    fi
    if [ -f "coverage/coverage-final.json" ]; then
        echo -e "  • JSON:        coverage/coverage-final.json"
    fi
    if [ -f "coverage/clover.xml" ]; then
        echo -e "  • Clover XML:  coverage/clover.xml"
    fi
    if [ -d "coverage/lcov-report" ]; then
        echo -e "  • HTML Report: coverage/lcov-report/index.html"
    fi
    echo ""

    # Open HTML report if requested
    if [ "$OPEN_REPORT" = true ]; then
        if [ -f "coverage/lcov-report/index.html" ]; then
            echo -e "${YELLOW}Opening HTML report in browser...${NC}"

            # Detect OS and open browser accordingly
            if [[ "$OSTYPE" == "linux-gnu"* ]]; then
                xdg-open coverage/lcov-report/index.html 2>/dev/null || \
                sensible-browser coverage/lcov-report/index.html 2>/dev/null || \
                echo -e "${YELLOW}Could not open browser automatically. Please open coverage/lcov-report/index.html manually.${NC}"
            elif [[ "$OSTYPE" == "darwin"* ]]; then
                open coverage/lcov-report/index.html
            elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
                start coverage/lcov-report/index.html
            else
                echo -e "${YELLOW}Could not detect OS. Please open coverage/lcov-report/index.html manually.${NC}"
            fi
        else
            echo -e "${YELLOW}HTML report not found. Coverage may have failed.${NC}"
        fi
    else
        echo -e "${BLUE}Tip: Use --open to automatically view the HTML report${NC}"
    fi
else
    echo -e "${RED}✗ Coverage directory not found${NC}"
    COVERAGE_EXIT_CODE=1
fi

echo ""
echo -e "${BLUE}==================================================${NC}"
echo -e "${BLUE}  Coverage Analysis Complete${NC}"
echo -e "${BLUE}==================================================${NC}"
echo ""

# Exit with the same code as the test run
exit $COVERAGE_EXIT_CODE
