#!/usr/bin/env bash
# =============================================================================
# docker-e2e.sh - Helper Script for Docker E2E Testing Infrastructure
# =============================================================================
# Issue #2009 - Phase 3: Production-Grade Infrastructure
#
# This script simplifies running E2E tests in Docker containers with full
# observability (Prometheus + Grafana).
#
# Usage:
#   ./scripts/docker-e2e.sh build          # Build Docker images
#   ./scripts/docker-e2e.sh run            # Run all shards + monitoring
#   ./scripts/docker-e2e.sh run-shard N    # Run specific shard only
#   ./scripts/docker-e2e.sh stop           # Stop all containers
#   ./scripts/docker-e2e.sh clean          # Stop and remove volumes
#   ./scripts/docker-e2e.sh logs [service] # View logs
#   ./scripts/docker-e2e.sh status         # Check container status
#   ./scripts/docker-e2e.sh metrics        # View Prometheus metrics
#   ./scripts/docker-e2e.sh dashboard      # Open Grafana dashboard
# =============================================================================

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
COMPOSE_FILE="docker-compose.e2e.yml"
PROJECT_NAME="meepleai-e2e"
PROMETHEUS_URL="http://localhost:9090"
GRAFANA_URL="http://localhost:3001"
# Script location: apps/web/scripts/docker-e2e.sh
# Compose file location: repository_root/docker-compose.e2e.yml
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

# Helper functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

check_docker() {
    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed. Please install Docker first."
        exit 1
    fi

    if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
        log_error "Docker Compose is not installed. Please install Docker Compose first."
        exit 1
    fi
}

check_file() {
    # Files are checked relative to REPO_ROOT
    local filepath="$1"
    if [[ ! -f "$REPO_ROOT/$filepath" ]]; then
        log_error "Required file not found: $filepath"
        log_error "Looking in: $REPO_ROOT/$filepath"
        exit 1
    fi
}

docker_compose() {
    # Execute docker-compose from repository root
    # docker-compose.e2e.yml is at repository root
    (cd "$REPO_ROOT" && docker compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" "$@" 2>/dev/null || docker-compose -f "$COMPOSE_FILE" -p "$PROJECT_NAME" "$@")
}

# Command functions
cmd_build() {
    log_info "Building Docker images for E2E testing..."
    check_file "apps/web/Dockerfile.e2e"

    docker_compose build --no-cache

    log_success "Docker images built successfully!"
    log_info "Image: meepleai-e2e:latest"
}

cmd_run() {
    log_info "Starting E2E testing infrastructure..."
    log_info "Services: 4 test shards + Prometheus + Grafana + cAdvisor"

    check_file "apps/web/Dockerfile.e2e"
    check_file "$COMPOSE_FILE"

    # Start infrastructure (Prometheus + Grafana + cAdvisor)
    log_info "Starting monitoring stack (Prometheus + Grafana + cAdvisor)..."
    docker_compose up -d prometheus grafana cadvisor

    # Wait for Prometheus to be ready
    log_info "Waiting for Prometheus to be ready..."
    for i in {1..30}; do
        if curl -s "${PROMETHEUS_URL}/-/ready" > /dev/null 2>&1; then
            log_success "Prometheus is ready!"
            break
        fi
        sleep 2
        if [[ $i -eq 30 ]]; then
            log_error "Prometheus failed to start within 60 seconds"
            cmd_logs prometheus
            exit 1
        fi
    done

    # Wait for Grafana to be ready
    log_info "Waiting for Grafana to be ready..."
    for i in {1..30}; do
        if curl -s "${GRAFANA_URL}/api/health" > /dev/null 2>&1; then
            log_success "Grafana is ready!"
            break
        fi
        sleep 2
        if [[ $i -eq 30 ]]; then
            log_error "Grafana failed to start within 60 seconds"
            cmd_logs grafana
            exit 1
        fi
    done

    # Start all test shards
    log_info "Starting E2E test shards (1/4, 2/4, 3/4, 4/4)..."
    docker_compose up --build e2e-shard-1 e2e-shard-2 e2e-shard-3 e2e-shard-4

    # Check exit codes
    SHARD_1_EXIT=$(docker_compose ps -q e2e-shard-1 | xargs docker inspect -f '{{.State.ExitCode}}' 2>/dev/null || echo "1")
    SHARD_2_EXIT=$(docker_compose ps -q e2e-shard-2 | xargs docker inspect -f '{{.State.ExitCode}}' 2>/dev/null || echo "1")
    SHARD_3_EXIT=$(docker_compose ps -q e2e-shard-3 | xargs docker inspect -f '{{.State.ExitCode}}' 2>/dev/null || echo "1")
    SHARD_4_EXIT=$(docker_compose ps -q e2e-shard-4 | xargs docker inspect -f '{{.State.ExitCode}}' 2>/dev/null || echo "1")

    log_info "Test Results:"
    log_info "  Shard 1/4: Exit code $SHARD_1_EXIT"
    log_info "  Shard 2/4: Exit code $SHARD_2_EXIT"
    log_info "  Shard 3/4: Exit code $SHARD_3_EXIT"
    log_info "  Shard 4/4: Exit code $SHARD_4_EXIT"

    TOTAL_EXIT=$((SHARD_1_EXIT + SHARD_2_EXIT + SHARD_3_EXIT + SHARD_4_EXIT))

    if [[ $TOTAL_EXIT -eq 0 ]]; then
        log_success "All E2E tests passed! ✅"
    else
        log_error "Some E2E tests failed. Check logs for details."
        log_info "View test results: ./test-results/"
        log_info "View Playwright reports: ./playwright-report/"
        exit 1
    fi

    log_info ""
    log_info "📊 Monitoring URLs:"
    log_info "  Prometheus: ${PROMETHEUS_URL}"
    log_info "  Grafana:    ${GRAFANA_URL} (admin/\${GRAFANA_E2E_PASSWORD:-admin})"
}

cmd_run_shard() {
    local SHARD_NUM=$1

    if [[ ! "$SHARD_NUM" =~ ^[1-4]$ ]]; then
        log_error "Invalid shard number: $SHARD_NUM. Must be 1, 2, 3, or 4."
        exit 1
    fi

    log_info "Starting E2E test shard $SHARD_NUM/4..."

    # Start monitoring stack if not running
    if ! docker_compose ps | grep -q "prometheus.*Up"; then
        log_info "Starting monitoring stack..."
        docker_compose up -d prometheus grafana
        sleep 5
    fi

    # Run specific shard
    docker_compose up --build "e2e-shard-$SHARD_NUM"

    # Check exit code
    EXIT_CODE=$(docker_compose ps -q "e2e-shard-$SHARD_NUM" | xargs docker inspect -f '{{.State.ExitCode}}' 2>/dev/null || echo "1")

    if [[ $EXIT_CODE -eq 0 ]]; then
        log_success "Shard $SHARD_NUM/4 tests passed! ✅"
    else
        log_error "Shard $SHARD_NUM/4 tests failed with exit code $EXIT_CODE"
        exit 1
    fi
}

cmd_stop() {
    log_info "Stopping E2E testing infrastructure..."
    docker_compose stop
    log_success "All containers stopped."
}

cmd_clean() {
    log_warning "This will stop containers and remove volumes (data will be lost)."
    read -p "Are you sure? [y/N] " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        log_info "Stopping and removing containers and volumes..."
        docker_compose down -v
        log_success "Cleanup complete."
    else
        log_info "Cleanup cancelled."
    fi
}

cmd_logs() {
    local SERVICE=${1:-""}

    if [[ -z "$SERVICE" ]]; then
        log_info "Showing logs for all services (Ctrl+C to exit)..."
        docker_compose logs -f
    else
        log_info "Showing logs for service: $SERVICE (Ctrl+C to exit)..."
        docker_compose logs -f "$SERVICE"
    fi
}

cmd_status() {
    log_info "Container Status:"
    docker_compose ps

    echo ""
    log_info "Resource Usage:"
    docker stats --no-stream $(docker_compose ps -q) 2>/dev/null || log_warning "No containers running"
}

cmd_metrics() {
    log_info "Opening Prometheus metrics explorer..."

    if command -v xdg-open &> /dev/null; then
        xdg-open "${PROMETHEUS_URL}/graph"
    elif command -v open &> /dev/null; then
        open "${PROMETHEUS_URL}/graph"
    elif command -v start &> /dev/null; then
        start "${PROMETHEUS_URL}/graph"
    else
        log_info "Please open manually: ${PROMETHEUS_URL}/graph"
    fi
}

cmd_dashboard() {
    log_info "Opening Grafana dashboard..."
    log_info "Credentials: admin / \${GRAFANA_E2E_PASSWORD:-admin}"

    if command -v xdg-open &> /dev/null; then
        xdg-open "${GRAFANA_URL}/d/e2e-testing/e2e-test-monitoring"
    elif command -v open &> /dev/null; then
        open "${GRAFANA_URL}/d/e2e-testing/e2e-test-monitoring"
    elif command -v start &> /dev/null; then
        start "${GRAFANA_URL}/d/e2e-testing/e2e-test-monitoring"
    else
        log_info "Please open manually: ${GRAFANA_URL}/d/e2e-testing/e2e-test-monitoring"
    fi
}

cmd_help() {
    cat << EOF
Docker E2E Testing Helper Script

Usage: ./scripts/docker-e2e.sh <command> [options]

Commands:
  build              Build Docker images
  run                Run all shards + monitoring stack
  run-shard <N>      Run specific shard (1-4)
  stop               Stop all containers
  clean              Stop and remove containers + volumes
  logs [service]     View container logs (all or specific service)
  status             Show container status and resource usage
  metrics            Open Prometheus metrics explorer
  dashboard          Open Grafana dashboard
  help               Show this help message

Examples:
  # Build and run full E2E test suite with monitoring
  ./scripts/docker-e2e.sh build
  ./scripts/docker-e2e.sh run

  # Run only shard 2
  ./scripts/docker-e2e.sh run-shard 2

  # View logs for specific shard
  ./scripts/docker-e2e.sh logs e2e-shard-1

  # Check container status and resource usage
  ./scripts/docker-e2e.sh status

  # Open Grafana dashboard
  ./scripts/docker-e2e.sh dashboard

  # Cleanup everything
  ./scripts/docker-e2e.sh clean

Services:
  - e2e-shard-1      Test shard 1/4
  - e2e-shard-2      Test shard 2/4
  - e2e-shard-3      Test shard 3/4
  - e2e-shard-4      Test shard 4/4
  - prometheus       Metrics collection
  - grafana          Visualization and alerting
  - aggregator       Result aggregation

Monitoring URLs:
  - Prometheus: ${PROMETHEUS_URL}
  - Grafana:    ${GRAFANA_URL} (admin/\${GRAFANA_E2E_PASSWORD:-admin})

Documentation:
  - Issue #2009: https://github.com/meepleai/meepleai-monorepo/issues/2009
  - E2E Testing Guide: docs/02-development/testing/e2e-testing-guide.md
  - Runbook: docs/05-operations/runbooks/e2e-docker-runbook.md
EOF
}

# Main script logic
main() {
    check_docker

    local COMMAND=${1:-"help"}

    case "$COMMAND" in
        build)
            cmd_build
            ;;
        run)
            cmd_run
            ;;
        run-shard)
            if [[ -z "${2:-}" ]]; then
                log_error "Shard number required. Usage: $0 run-shard <1-4>"
                exit 1
            fi
            cmd_run_shard "$2"
            ;;
        stop)
            cmd_stop
            ;;
        clean)
            cmd_clean
            ;;
        logs)
            cmd_logs "${2:-}"
            ;;
        status)
            cmd_status
            ;;
        metrics)
            cmd_metrics
            ;;
        dashboard)
            cmd_dashboard
            ;;
        help|--help|-h)
            cmd_help
            ;;
        *)
            log_error "Unknown command: $COMMAND"
            echo ""
            cmd_help
            exit 1
            ;;
    esac
}

main "$@"