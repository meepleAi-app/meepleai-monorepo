#!/bin/bash

# Backend Code Coverage Runner
# Runs .NET tests with code coverage and generates HTML reports
# Usage: ./tools/run-backend-coverage.sh [options]
#
# Options:
#   --html          Generate HTML coverage report (requires reportgenerator)
#   --threshold N   Set coverage threshold (default: 90)
#   --open          Open HTML report in browser (implies --html)
#   --help          Show this help message

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Default options
GENERATE_HTML=false
OPEN_REPORT=false
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
    --help)
      echo "Backend Code Coverage Runner"
      echo ""
      echo "Usage: ./tools/run-backend-coverage.sh [options]"
      echo ""
      echo "Options:"
      echo "  --html          Generate HTML coverage report (requires reportgenerator)"
      echo "  --threshold N   Set coverage threshold (default: 90)"
      echo "  --open          Open HTML report in browser (implies --html)"
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
echo -e "${BLUE}  MeepleAI Backend Code Coverage Runner${NC}"
echo -e "${BLUE}==================================================${NC}"
echo ""

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: dotnet CLI not found${NC}"
    echo "Please install .NET 9 SDK: https://dotnet.microsoft.com/download"
    exit 1
fi

# Navigate to API directory
cd "$(dirname "$0")/../apps/api" || exit 1

echo -e "${YELLOW}Step 1/4: Cleaning previous coverage data...${NC}"
rm -rf tests/Api.Tests/coverage/
rm -rf tests/Api.Tests/coverage-report/
echo -e "${GREEN}✓ Cleaned${NC}"
echo ""

echo -e "${YELLOW}Step 2/4: Running tests with coverage collection...${NC}"
echo -e "${BLUE}Threshold: ${THRESHOLD}%${NC}"
echo ""

# Run tests with coverage
dotnet test \
  --logger "console;verbosity=normal" \
  --blame-hang-timeout 5m \
  -p:CollectCoverage=true \
  -p:CoverletOutputFormat=cobertura \
  -p:CoverletOutput=./tests/Api.Tests/coverage/ \
  -p:Threshold=${THRESHOLD} \
  -p:ThresholdType=line \
  -p:Exclude="[*]Api.Migrations.*" \
  -- xUnit.Parallelization.MaxParallelThreads=2

COVERAGE_EXIT_CODE=$?

if [ $COVERAGE_EXIT_CODE -eq 0 ]; then
    echo ""
    echo -e "${GREEN}✓ Tests passed with coverage threshold of ${THRESHOLD}%${NC}"
else
    echo ""
    echo -e "${RED}✗ Tests failed or coverage below ${THRESHOLD}%${NC}"
fi

echo ""
echo -e "${YELLOW}Step 3/4: Processing coverage results...${NC}"

# Check if coverage file exists
if [ -f "tests/Api.Tests/coverage/coverage.cobertura.xml" ]; then
    echo -e "${GREEN}✓ Coverage data generated${NC}"

    # Extract coverage percentage from the cobertura XML
    COVERAGE_PCT=$(grep -oP 'line-rate="\K[0-9.]+' tests/Api.Tests/coverage/coverage.cobertura.xml | head -1)
    COVERAGE_PCT=$(echo "$COVERAGE_PCT * 100" | bc | cut -d'.' -f1)

    echo ""
    echo -e "${BLUE}==================================================${NC}"
    echo -e "${BLUE}  Coverage Summary${NC}"
    echo -e "${BLUE}==================================================${NC}"
    echo -e "Line Coverage: ${GREEN}${COVERAGE_PCT}%${NC}"
    echo -e "Threshold:     ${THRESHOLD}%"
    echo -e "${BLUE}==================================================${NC}"
    echo ""
else
    echo -e "${RED}✗ Coverage data not found${NC}"
    exit 1
fi

# Generate HTML report if requested
if [ "$GENERATE_HTML" = true ]; then
    echo -e "${YELLOW}Step 4/4: Generating HTML coverage report...${NC}"

    # Check if reportgenerator is installed
    if ! command -v reportgenerator &> /dev/null; then
        echo -e "${YELLOW}Installing dotnet-reportgenerator-globaltool...${NC}"
        dotnet tool install -g dotnet-reportgenerator-globaltool
    fi

    # Generate HTML report
    reportgenerator \
        -reports:tests/Api.Tests/coverage/coverage.cobertura.xml \
        -targetdir:tests/Api.Tests/coverage-report \
        -reporttypes:Html

    echo -e "${GREEN}✓ HTML report generated${NC}"
    echo -e "${BLUE}Report location: ${PWD}/tests/Api.Tests/coverage-report/index.html${NC}"

    # Open report in browser if requested
    if [ "$OPEN_REPORT" = true ]; then
        echo ""
        echo -e "${YELLOW}Opening report in browser...${NC}"

        # Detect OS and open browser accordingly
        if [[ "$OSTYPE" == "linux-gnu"* ]]; then
            xdg-open tests/Api.Tests/coverage-report/index.html 2>/dev/null || \
            sensible-browser tests/Api.Tests/coverage-report/index.html 2>/dev/null || \
            echo -e "${YELLOW}Could not open browser automatically. Please open the report manually.${NC}"
        elif [[ "$OSTYPE" == "darwin"* ]]; then
            open tests/Api.Tests/coverage-report/index.html
        elif [[ "$OSTYPE" == "msys" || "$OSTYPE" == "cygwin" ]]; then
            start tests/Api.Tests/coverage-report/index.html
        else
            echo -e "${YELLOW}Could not detect OS. Please open the report manually.${NC}"
        fi
    fi
else
    echo -e "${YELLOW}Step 4/4: Skipping HTML report generation${NC}"
    echo -e "${BLUE}Tip: Use --html to generate an HTML report${NC}"
fi

echo ""
echo -e "${BLUE}==================================================${NC}"
echo -e "${BLUE}  Coverage Analysis Complete${NC}"
echo -e "${BLUE}==================================================${NC}"
echo ""
echo -e "${BLUE}Coverage files:${NC}"
echo -e "  • Cobertura XML: tests/Api.Tests/coverage/coverage.cobertura.xml"
if [ "$GENERATE_HTML" = true ]; then
    echo -e "  • HTML Report:   tests/Api.Tests/coverage-report/index.html"
fi
echo ""

# Exit with the same code as the test run
exit $COVERAGE_EXIT_CODE
