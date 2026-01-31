#!/bin/bash
# Backup Configuration for MeepleAI
# This file contains centralized configuration for all backup scripts

# Exit on error, undefined variables, and pipe failures
set -euo pipefail

# ============================================================================
# BACKUP CONFIGURATION
# ============================================================================

# Backup root directory (override with environment variable)
export BACKUP_ROOT_DIR="${BACKUP_ROOT_DIR:-./backups}"

# Individual service backup directories
export BACKUP_DIR_POSTGRES="${BACKUP_ROOT_DIR}/postgres"
export BACKUP_DIR_QDRANT="${BACKUP_ROOT_DIR}/qdrant"
export BACKUP_DIR_REDIS="${BACKUP_ROOT_DIR}/redis"
export BACKUP_DIR_VOLUMES="${BACKUP_ROOT_DIR}/volumes"

# ============================================================================
# RETENTION POLICIES
# ============================================================================

# Days to keep backups before deletion (default: 30 days)
export RETENTION_DAYS="${RETENTION_DAYS:-30}"

# Days before compressing with xz (default: 7 days)
# Recent backups use gzip for fast restore, old backups use xz for better compression
export COMPRESSION_THRESHOLD_DAYS="${COMPRESSION_THRESHOLD_DAYS:-7}"

# ============================================================================
# DOCKER CONFIGURATION
# ============================================================================

# Docker Compose project directory
export DOCKER_COMPOSE_DIR="${DOCKER_COMPOSE_DIR:-./infra}"

# Docker container names (override if different)
export POSTGRES_CONTAINER="${POSTGRES_CONTAINER:-postgres}"
export REDIS_CONTAINER="${REDIS_CONTAINER:-redis}"
export QDRANT_CONTAINER="${QDRANT_CONTAINER:-qdrant}"

# Docker volume names
export POSTGRES_VOLUME="${POSTGRES_VOLUME:-infra_pgdata}"
export REDIS_VOLUME="${REDIS_VOLUME:-infra_redisdata}"
export QDRANT_VOLUME="${QDRANT_VOLUME:-infra_qdrantdata}"
export GRAFANA_VOLUME="${GRAFANA_VOLUME:-infra_grafanadata}"
export PROMETHEUS_VOLUME="${PROMETHEUS_VOLUME:-infra_prometheusdata}"
export N8N_VOLUME="${N8N_VOLUME:-infra_n8ndata}"

# ============================================================================
# DATABASE CONFIGURATION
# ============================================================================

# PostgreSQL credentials (from environment or defaults)
export POSTGRES_USER="${POSTGRES_USER:-meeple}"
export POSTGRES_DB="${POSTGRES_DB:-meepleai}"
export POSTGRES_PASSWORD="${POSTGRES_PASSWORD:-}"

# ============================================================================
# QDRANT CONFIGURATION
# ============================================================================

# Qdrant API URL
export QDRANT_URL="${QDRANT_URL:-http://localhost:6333}"

# Qdrant collection name (primary collection for embeddings)
export QDRANT_COLLECTION="${QDRANT_COLLECTION:-rules}"

# ============================================================================
# NOTIFICATION CONFIGURATION (Optional)
# ============================================================================

# Email notifications (optional)
export NOTIFICATION_EMAIL="${NOTIFICATION_EMAIL:-}"

# Slack webhook URL (optional)
export SLACK_WEBHOOK_URL="${SLACK_WEBHOOK_URL:-}"

# PagerDuty integration key (optional)
export PAGERDUTY_KEY="${PAGERDUTY_KEY:-}"

# ============================================================================
# S3 CONFIGURATION (Optional - Phase 2)
# ============================================================================

# AWS S3 bucket for remote backups
export S3_BUCKET="${S3_BUCKET:-}"
export S3_REGION="${S3_REGION:-us-east-1}"

# Enable S3 upload (default: false)
export S3_UPLOAD_ENABLED="${S3_UPLOAD_ENABLED:-false}"

# Enable encryption for S3 uploads (default: true if S3 enabled)
export S3_ENCRYPTION_ENABLED="${S3_ENCRYPTION_ENABLED:-true}"

# ============================================================================
# LOGGING CONFIGURATION
# ============================================================================

# Log directory
export LOG_DIR="${LOG_DIR:-./logs/backup}"

# Enable verbose logging (default: false)
export VERBOSE="${VERBOSE:-false}"

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

# Log message with timestamp
log() {
    local level="$1"
    shift
    local message="$*"
    local timestamp
    timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[$timestamp] [$level] $message"
}

# Log info message
log_info() {
    log "INFO" "$@"
}

# Log error message
log_error() {
    log "ERROR" "$@" >&2
}

# Log success message
log_success() {
    log "SUCCESS" "$@"
}

# Create directory if it doesn't exist
ensure_directory() {
    local dir="$1"
    if [[ ! -d "$dir" ]]; then
        mkdir -p "$dir"
        log_info "Created directory: $dir"
    fi
}

# Check if Docker container is running
is_container_running() {
    local container="$1"
    docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" ps -q "$container" > /dev/null 2>&1
}

# Send notification (if configured)
send_notification() {
    local subject="$1"
    local message="$2"
    local status="${3:-info}"  # info, success, error

    # Email notification
    if [[ -n "$NOTIFICATION_EMAIL" ]]; then
        echo "$message" | mail -s "[$status] $subject" "$NOTIFICATION_EMAIL" 2>/dev/null || true
    fi

    # Slack notification
    if [[ -n "$SLACK_WEBHOOK_URL" ]]; then
        local color="good"
        [[ "$status" == "error" ]] && color="danger"
        [[ "$status" == "info" ]] && color="warning"

        local payload="{\"text\":\"$subject\",\"attachments\":[{\"color\":\"$color\",\"text\":\"$message\"}]}"
        curl -X POST -H 'Content-type: application/json' \
             --data "$payload" \
             "$SLACK_WEBHOOK_URL" 2>/dev/null || true
    fi
}

# ============================================================================
# VALIDATION
# ============================================================================

# Validate configuration on source
validate_config() {
    local errors=0

    # Check Docker Compose directory exists
    if [[ ! -d "$DOCKER_COMPOSE_DIR" ]]; then
        log_error "Docker Compose directory not found: $DOCKER_COMPOSE_DIR"
        ((errors++))
    fi

    # Check if docker is available
    if ! command -v docker &> /dev/null; then
        log_error "Docker command not found. Please install Docker."
        ((errors++))
    fi

    # Check if docker compose is available
    if ! docker compose version &> /dev/null; then
        log_error "Docker Compose not available. Please install Docker Compose."
        ((errors++))
    fi

    return "$errors"
}

# ============================================================================
# INITIALIZATION
# ============================================================================

# Create backup directories
ensure_directory "$BACKUP_ROOT_DIR"
ensure_directory "$BACKUP_DIR_POSTGRES"
ensure_directory "$BACKUP_DIR_QDRANT"
ensure_directory "$BACKUP_DIR_REDIS"
ensure_directory "$BACKUP_DIR_VOLUMES"
ensure_directory "$LOG_DIR"

# Log configuration if verbose
if [[ "$VERBOSE" == "true" ]]; then
    log_info "Backup configuration loaded"
    log_info "Backup root: $BACKUP_ROOT_DIR"
    log_info "Retention: $RETENTION_DAYS days"
    log_info "Docker Compose dir: $DOCKER_COMPOSE_DIR"
fi
