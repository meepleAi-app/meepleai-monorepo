#!/bin/bash
# =============================================================================
# MeepleAI Production Deployment Script
# =============================================================================
#
# Deploys MeepleAI to production at meepleai.io
#
# Usage:
#   ./scripts/deploy-meepleai.sh [command]
#
# Commands:
#   up        Start all services (default)
#   down      Stop all services
#   restart   Restart all services
#   logs      Show logs
#   status    Show service status
#   backup    Backup databases
#   update    Pull latest images and restart
#
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
INFRA_DIR="$PROJECT_ROOT/infra"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Compose files
COMPOSE_FILES="-f docker-compose.yml -f docker-compose.traefik.yml -f compose.prod.yml -f compose.meepleai.yml"

cd "$INFRA_DIR"

# =============================================================================
# Functions
# =============================================================================

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

check_prerequisites() {
    log_info "Checking prerequisites..."

    # Check Docker
    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed"
        exit 1
    fi

    # Check Docker Compose
    if ! docker compose version &> /dev/null; then
        log_error "Docker Compose is not installed"
        exit 1
    fi

    # Check secrets exist
    if [ ! -f "secrets/prod/postgres-password.txt" ]; then
        log_error "Production secrets not found. Run: cd infra/secrets/prod && ./setup-prod-secrets.sh"
        exit 1
    fi

    # Check Traefik config exists
    if [ ! -f "traefik/traefik.prod.yml" ]; then
        log_error "Traefik production config not found: traefik/traefik.prod.yml"
        exit 1
    fi

    log_success "Prerequisites check passed"
}

create_directories() {
    log_info "Creating required directories..."
    mkdir -p traefik/letsencrypt
    mkdir -p traefik/logs
    chmod 600 traefik/letsencrypt 2>/dev/null || true
    log_success "Directories created"
}

start_services() {
    log_info "Starting MeepleAI services..."

    docker compose $COMPOSE_FILES --profile full up -d

    log_success "Services started"
    echo ""
    log_info "Waiting for services to be healthy..."
    sleep 10

    show_status
}

stop_services() {
    log_info "Stopping MeepleAI services..."
    docker compose $COMPOSE_FILES --profile full down
    log_success "Services stopped"
}

restart_services() {
    log_info "Restarting MeepleAI services..."
    docker compose $COMPOSE_FILES --profile full restart
    log_success "Services restarted"
}

show_logs() {
    SERVICE=${1:-""}
    if [ -n "$SERVICE" ]; then
        docker compose $COMPOSE_FILES logs -f "$SERVICE"
    else
        docker compose $COMPOSE_FILES logs -f
    fi
}

show_status() {
    echo ""
    echo "========================================="
    echo "       MeepleAI Service Status          "
    echo "========================================="
    echo ""
    docker compose $COMPOSE_FILES ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}"
    echo ""
    echo "========================================="
    echo "           Endpoints                    "
    echo "========================================="
    echo ""
    echo "  🌐 Website:    https://www.meepleai.io"
    echo "  🔌 API:        https://api.meepleai.io"
    echo "  📊 Grafana:    https://grafana.meepleai.io"
    echo "  🚦 Traefik:    https://traefik.meepleai.io"
    echo ""
}

backup_databases() {
    log_info "Creating database backup..."

    BACKUP_DIR="$PROJECT_ROOT/backups/$(date +%Y%m%d_%H%M%S)"
    mkdir -p "$BACKUP_DIR"

    # PostgreSQL backup
    log_info "Backing up PostgreSQL..."
    docker compose $COMPOSE_FILES exec -T postgres pg_dumpall -U meeple > "$BACKUP_DIR/postgres_backup.sql"

    # Qdrant backup (if snapshots are configured)
    log_info "Backing up Qdrant..."
    docker compose $COMPOSE_FILES exec -T qdrant /qdrant/qdrant --help 2>/dev/null || log_warn "Qdrant CLI not available for backup"

    log_success "Backup completed: $BACKUP_DIR"
}

update_services() {
    log_info "Pulling latest images..."
    docker compose $COMPOSE_FILES pull

    log_info "Recreating containers with new images..."
    docker compose $COMPOSE_FILES --profile full up -d --force-recreate

    log_success "Update completed"
    show_status
}

show_help() {
    echo ""
    echo "MeepleAI Production Deployment"
    echo ""
    echo "Usage: $0 [command]"
    echo ""
    echo "Commands:"
    echo "  up        Start all services (default)"
    echo "  down      Stop all services"
    echo "  restart   Restart all services"
    echo "  logs      Show logs (optional: service name)"
    echo "  status    Show service status"
    echo "  backup    Backup databases"
    echo "  update    Pull latest images and restart"
    echo "  help      Show this help message"
    echo ""
}

# =============================================================================
# Main
# =============================================================================

COMMAND=${1:-up}

case $COMMAND in
    up)
        check_prerequisites
        create_directories
        start_services
        ;;
    down)
        stop_services
        ;;
    restart)
        restart_services
        ;;
    logs)
        show_logs "$2"
        ;;
    status)
        show_status
        ;;
    backup)
        backup_databases
        ;;
    update)
        check_prerequisites
        update_services
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        log_error "Unknown command: $COMMAND"
        show_help
        exit 1
        ;;
esac
