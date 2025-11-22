#!/bin/bash
# MeepleAI Test Environment Setup Script
# Automated clean setup with database creation and testing

set -e
set -o pipefail

# Configuration
DRY_RUN=false
SKIP_CLEANUP=false
SKIP_FRONTEND=false
SKIP_TESTS=false
RUN_FULL_TESTS=false
VERBOSE=false

# Colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

# Directories
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
API_DIR="$ROOT_DIR/apps/api/src/Api"
WEB_DIR="$ROOT_DIR/apps/web"
INFRA_DIR="$ROOT_DIR/infra"

# PIDs for background processes
API_PID=""
WEB_PID=""

# Test failure tracking
TEST_FAILURES=0

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --skip-cleanup)
            SKIP_CLEANUP=true
            shift
            ;;
        --skip-frontend)
            SKIP_FRONTEND=true
            shift
            ;;
        --skip-tests)
            SKIP_TESTS=true
            shift
            ;;
        --full)
            RUN_FULL_TESTS=true
            shift
            ;;
        --verbose|-v)
            VERBOSE=true
            shift
            ;;
        --help|-h)
            echo "MeepleAI Test Environment Setup Script"
            echo ""
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --dry-run           Show what would be executed without running"
            echo "  --skip-cleanup      Skip initial Docker cleanup and build artifacts removal"
            echo "  --skip-frontend     Don't start the frontend server"
            echo "  --skip-tests        Don't run tests after setup"
            echo "  --full              Run full test suite including E2E tests"
            echo "  --verbose, -v       Show detailed output"
            echo "  --help, -h          Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0                  # Full setup with tests"
            echo "  $0 --dry-run        # Preview what would be executed"
            echo "  $0 --skip-frontend  # Setup backend only"
            echo "  $0 --full           # Setup + run all tests including E2E"
            echo ""
            echo "What this script does:"
            echo "  1. Stop existing Docker containers and clean volumes"
            echo "  2. Clean build artifacts (optional)"
            echo "  3. Start Docker services (postgres, qdrant, redis, seq)"
            echo "  4. Wait for PostgreSQL to be ready"
            echo "  5. Build and start the API (auto-applies migrations)"
            echo "  6. Start the frontend dev server (optional)"
            echo "  7. Run tests (optional)"
            echo ""
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Cleanup function for graceful shutdown
cleanup() {
    echo ""
    echo -e "${YELLOW}🛑 Shutting down services...${NC}"

    if [ -n "$WEB_PID" ]; then
        echo -e "${CYAN}Stopping frontend (PID: $WEB_PID)...${NC}"
        kill $WEB_PID 2>/dev/null || true
    fi

    if [ -n "$API_PID" ]; then
        echo -e "${CYAN}Stopping API (PID: $API_PID)...${NC}"
        kill $API_PID 2>/dev/null || true
    fi

    echo -e "${GREEN}✓ Cleanup complete${NC}"
}

# Register cleanup on script exit
trap cleanup EXIT INT TERM

# Function to execute or preview commands
execute() {
    local description=$1
    shift
    local cmd="$@"

    if [ "$DRY_RUN" = true ]; then
        echo -e "${YELLOW}[DRY RUN] $description${NC}"
        echo -e "${CYAN}  Command: $cmd${NC}"
        return 0
    else
        echo -e "${CYAN}▶ $description${NC}"
        if [ "$VERBOSE" = true ]; then
            echo -e "${CYAN}  Running: $cmd${NC}"
        fi
        eval "$cmd"
    fi
}

# Function to wait for service
wait_for_service() {
    local service=$1
    local url=$2
    local max_attempts=${3:-30}
    local attempt=0

    echo -e "${CYAN}⏳ Waiting for $service to be ready...${NC}"

    while [ $attempt -lt $max_attempts ]; do
        if curl -sf "$url" > /dev/null 2>&1; then
            echo -e "${GREEN}✓ $service is ready!${NC}"
            return 0
        fi

        attempt=$((attempt + 1))
        echo -n "."
        sleep 2
    done

    echo ""
    echo -e "${RED}✗ $service failed to start after $max_attempts attempts${NC}"
    return 1
}

# Function to wait for PostgreSQL specifically
wait_for_postgres() {
    local max_attempts=30
    local attempt=0

    echo -e "${CYAN}⏳ Waiting for PostgreSQL to be ready...${NC}"

    if [ "$DRY_RUN" = true ]; then
        echo -e "${YELLOW}[DRY RUN] Would wait for PostgreSQL${NC}"
        return 0
    fi

    while [ $attempt -lt $max_attempts ]; do
        if docker compose -f "$INFRA_DIR/docker-compose.yml" exec -T meepleai-postgres pg_isready -U postgres > /dev/null 2>&1; then
            echo -e "${GREEN}✓ PostgreSQL is ready!${NC}"
            return 0
        fi

        attempt=$((attempt + 1))
        echo -n "."
        sleep 2
    done

    echo ""
    echo -e "${RED}✗ PostgreSQL failed to start after $max_attempts attempts${NC}"
    return 1
}

# Main script header
echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   MeepleAI Test Environment Setup         ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo -e "${YELLOW}⚠️  DRY RUN MODE - Commands will be previewed, not executed${NC}"
    echo ""
fi

# Step 1: Docker cleanup
echo -e "${MAGENTA}═══ Step 1: Docker Cleanup ═══${NC}"
echo ""

if [ "$SKIP_CLEANUP" = false ]; then
    execute "Stop all Docker containers and remove volumes" \
        "cd '$INFRA_DIR' && docker compose down -v"
else
    echo -e "${YELLOW}⊘ Skipping Docker cleanup (--skip-cleanup)${NC}"
fi
echo ""

# Step 2: Build artifacts cleanup
echo -e "${MAGENTA}═══ Step 2: Build Artifacts Cleanup ═══${NC}"
echo ""

if [ "$SKIP_CLEANUP" = false ]; then
    if [ -d "$API_DIR" ]; then
        execute "Clean .NET build artifacts" \
            "cd '$API_DIR' && dotnet clean > /dev/null 2>&1 || true"
    fi

    if [ -d "$WEB_DIR/.next" ]; then
        execute "Remove Next.js build cache" \
            "rm -rf '$WEB_DIR/.next'"
    fi
else
    echo -e "${YELLOW}⊘ Skipping build cleanup (--skip-cleanup)${NC}"
fi
echo ""

# Step 3: Start Docker services
echo -e "${MAGENTA}═══ Step 3: Start Docker Services ═══${NC}"
echo ""

execute "Start core Docker services (postgres, qdrant, redis, seq)" \
    "cd '$INFRA_DIR' && docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-seq"

if [ "$DRY_RUN" = false ]; then
    sleep 3  # Give services time to initialize
    wait_for_postgres
fi
echo ""

# Step 4: Build and start API
echo -e "${MAGENTA}═══ Step 4: Build & Start API ═══${NC}"
echo ""

if [ "$DRY_RUN" = false ]; then
    echo -e "${CYAN}▶ Building API...${NC}"
    cd "$API_DIR"
    dotnet build --configuration Release > /dev/null 2>&1
    echo -e "${GREEN}✓ Build complete${NC}"

    echo -e "${CYAN}▶ Starting API server...${NC}"
    nohup dotnet run --configuration Release > "$ROOT_DIR/api.log" 2>&1 &
    API_PID=$!
    echo -e "${GREEN}✓ API started (PID: $API_PID)${NC}"

    # Wait for API health check
    wait_for_service "API" "http://localhost:5080/health" 60
else
    echo -e "${YELLOW}[DRY RUN] Would build and start API at localhost:5080${NC}"
fi
echo ""

# Step 5: Start frontend (optional)
echo -e "${MAGENTA}═══ Step 5: Start Frontend ═══${NC}"
echo ""

if [ "$SKIP_FRONTEND" = false ]; then
    if [ "$DRY_RUN" = false ]; then
        echo -e "${CYAN}▶ Installing frontend dependencies...${NC}"
        cd "$WEB_DIR"

        if [ ! -d "node_modules" ]; then
            pnpm install > /dev/null 2>&1
            echo -e "${GREEN}✓ Dependencies installed${NC}"
        else
            echo -e "${GREEN}✓ Dependencies already installed${NC}"
        fi

        echo -e "${CYAN}▶ Starting frontend dev server...${NC}"
        nohup pnpm dev > "$ROOT_DIR/web.log" 2>&1 &
        WEB_PID=$!
        echo -e "${GREEN}✓ Frontend started (PID: $WEB_PID)${NC}"

        # Give Next.js time to start
        sleep 5
        wait_for_service "Frontend" "http://localhost:3000" 60
    else
        echo -e "${YELLOW}[DRY RUN] Would install dependencies and start frontend at localhost:3000${NC}"
    fi
else
    echo -e "${YELLOW}⊘ Skipping frontend (--skip-frontend)${NC}"
fi
echo ""

# Step 6: Run tests (optional)
if [ "$SKIP_TESTS" = false ]; then
    echo -e "${MAGENTA}═══ Step 6: Run Tests ═══${NC}"
    echo ""

    if [ "$DRY_RUN" = false ]; then
        # Backend tests
        echo -e "${CYAN}▶ Running backend tests...${NC}"
        cd "$API_DIR"

        if dotnet test --configuration Release --no-build > "$ROOT_DIR/test-backend.log" 2>&1; then
            echo -e "${GREEN}✓ Backend tests passed${NC}"
        else
            echo -e "${RED}✗ Backend tests failed (see test-backend.log)${NC}"
            TEST_FAILURES=$((TEST_FAILURES + 1))
        fi

        # Frontend tests
        echo -e "${CYAN}▶ Running frontend tests...${NC}"
        cd "$WEB_DIR"

        if pnpm test -- --passWithNoTests > "$ROOT_DIR/test-frontend.log" 2>&1; then
            echo -e "${GREEN}✓ Frontend tests passed${NC}"
        else
            echo -e "${RED}✗ Frontend tests failed (see test-frontend.log)${NC}"
            TEST_FAILURES=$((TEST_FAILURES + 1))
        fi

        # Full test suite (E2E)
        if [ "$RUN_FULL_TESTS" = true ]; then
            echo -e "${CYAN}▶ Running E2E tests...${NC}"
            cd "$WEB_DIR"

            if pnpm test:e2e > "$ROOT_DIR/test-e2e.log" 2>&1; then
                echo -e "${GREEN}✓ E2E tests passed${NC}"
            else
                echo -e "${RED}✗ E2E tests failed (see test-e2e.log)${NC}"
                TEST_FAILURES=$((TEST_FAILURES + 1))
            fi
        fi
    else
        echo -e "${YELLOW}[DRY RUN] Would run backend and frontend tests${NC}"
        if [ "$RUN_FULL_TESTS" = true ]; then
            echo -e "${YELLOW}[DRY RUN] Would run E2E tests (--full)${NC}"
        fi
    fi
else
    echo -e "${YELLOW}⊘ Skipping tests (--skip-tests)${NC}"
fi
echo ""

# Summary
if [ "$TEST_FAILURES" -gt 0 ] && [ "$SKIP_TESTS" = false ] && [ "$DRY_RUN" = false ]; then
    # Test failures detected - exit with error
    echo -e "${RED}╔════════════════════════════════════════════╗${NC}"
    echo -e "${RED}║   Setup Failed - Tests Failed              ║${NC}"
    echo -e "${RED}╚════════════════════════════════════════════╝${NC}"
    echo ""
    echo -e "${RED}✗ $TEST_FAILURES test suite(s) failed${NC}"
    echo ""
    echo -e "${CYAN}📋 Test Logs:${NC}"
    if [ -f "$ROOT_DIR/test-backend.log" ]; then
        echo -e "  ${RED}Backend:${NC}  cat $ROOT_DIR/test-backend.log"
    fi
    if [ -f "$ROOT_DIR/test-frontend.log" ]; then
        echo -e "  ${RED}Frontend:${NC} cat $ROOT_DIR/test-frontend.log"
    fi
    if [ -f "$ROOT_DIR/test-e2e.log" ]; then
        echo -e "  ${RED}E2E:${NC}      cat $ROOT_DIR/test-e2e.log"
    fi
    echo ""
    echo -e "${YELLOW}💡 Fix the failing tests and run the script again${NC}"
    echo ""
    exit 1
else
    # Success - all tests passed or tests were skipped
    echo -e "${BLUE}╔════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║   Setup Complete!                          ║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════╝${NC}"
    echo ""

    if [ "$DRY_RUN" = false ]; then
        echo -e "${GREEN}✓ Environment is ready!${NC}"
        echo ""
        echo -e "${CYAN}📍 Service URLs:${NC}"
        echo -e "  ${GREEN}Frontend:${NC}      http://localhost:3000"
        echo -e "  ${GREEN}API:${NC}           http://localhost:5080"
        echo -e "  ${GREEN}Health Check:${NC}  http://localhost:5080/health"
        echo -e "  ${GREEN}Seq (Logs):${NC}    http://localhost:8081"
        echo -e "  ${GREEN}Qdrant:${NC}        http://localhost:6333/dashboard"
        echo ""
        echo -e "${CYAN}👤 Demo Users:${NC}"
        echo -e "  ${GREEN}admin@meepleai.dev${NC}   - Password: ${GREEN}Demo123!${NC}"
        echo -e "  ${GREEN}editor@meepleai.dev${NC}  - Password: ${GREEN}Demo123!${NC}"
        echo -e "  ${GREEN}user@meepleai.dev${NC}    - Password: ${GREEN}Demo123!${NC}"
        echo ""
        echo -e "${CYAN}📋 Logs:${NC}"
        echo -e "  ${GREEN}API:${NC}      tail -f $ROOT_DIR/api.log"
        if [ "$SKIP_FRONTEND" = false ]; then
            echo -e "  ${GREEN}Frontend:${NC} tail -f $ROOT_DIR/web.log"
        fi
        if [ "$SKIP_TESTS" = false ]; then
            echo -e "  ${GREEN}Tests:${NC}    ls -la $ROOT_DIR/test-*.log"
        fi
        echo ""
        echo -e "${YELLOW}💡 Press Ctrl+C to stop all services${NC}"
        echo ""

        # Keep script running
        echo -e "${CYAN}⏳ Services running... (Ctrl+C to stop)${NC}"
        wait
    else
        echo -e "${YELLOW}✓ Dry run complete. Use without --dry-run to execute.${NC}"
    fi
fi

