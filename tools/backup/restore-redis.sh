#!/bin/bash
# Redis Restore Script for MeepleAI
# Restores Redis database from RDB snapshot

set -euo pipefail

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source configuration
# shellcheck source=./backup-config.sh
source "${SCRIPT_DIR}/backup-config.sh"

# ============================================================================
# CONFIGURATION
# ============================================================================

TIMESTAMP=$(date +%Y%m%d_%H%M%S)
LOG_FILE="${LOG_DIR}/redis_restore_${TIMESTAMP}.log"

# Redis RDB file location inside container
REDIS_RDB_FILE="/data/dump.rdb"

# ============================================================================
# FUNCTIONS
# ============================================================================

# Show usage information
usage() {
    echo "Usage: $0 <backup_file>"
    echo ""
    echo "Restore Redis database from RDB backup file"
    echo ""
    echo "Arguments:"
    echo "  backup_file    Path to backup file (.rdb.gz or .rdb.xz)"
    echo ""
    echo "Examples:"
    echo "  $0 backups/redis/redis_20250108_020000.rdb.gz"
    echo "  $0 backups/redis/redis_20250101_020000.rdb.xz"
    echo ""
    echo "WARNING: This will OVERWRITE the current Redis data!"
    echo "         Redis container will be stopped during restore."
    exit 1
}

# Confirm restore operation
confirm_restore() {
    local backup_file="$1"

    log_info "=========================================="
    log_info "⚠️  WARNING: REDIS RESTORE OPERATION"
    log_info "=========================================="
    log_info "This will OVERWRITE the current Redis data with:"
    log_info "  Backup file: $backup_file"
    log_info ""
    log_info "Current Redis data will be PERMANENTLY LOST!"
    log_info "Redis will be temporarily stopped during restore."
    log_info "=========================================="

    # In interactive mode, ask for confirmation
    if [[ -t 0 ]]; then
        read -r -p "Type 'YES' to confirm restore: " response
        if [[ "$response" != "YES" ]]; then
            log_info "Restore cancelled by user"
            exit 0
        fi
    else
        # In non-interactive mode, require FORCE_RESTORE=1
        if [[ "${FORCE_RESTORE:-0}" != "1" ]]; then
            log_error "Non-interactive restore requires FORCE_RESTORE=1 environment variable"
            exit 1
        fi
        log_info "FORCE_RESTORE=1 detected, proceeding with restore"
    fi
}

# Decompress backup file based on extension
decompress_backup() {
    local backup_file="$1"

    if [[ "$backup_file" == *.gz ]]; then
        gunzip -c "$backup_file"
    elif [[ "$backup_file" == *.xz ]]; then
        xzcat "$backup_file"
    elif [[ "$backup_file" == *.rdb ]]; then
        cat "$backup_file"
    else
        log_error "Unsupported backup file format: $backup_file"
        log_error "Supported formats: .rdb, .rdb.gz, .rdb.xz"
        return 1
    fi
}

# Create pre-restore backup
create_pre_restore_backup() {
    log_info "Creating pre-restore backup as safety net..."

    # Trigger Redis save
    docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
        redis-cli BGSAVE > /dev/null 2>&1 || true

    # Wait briefly for save to complete
    sleep 2

    local safety_backup="${BACKUP_DIR_REDIS}/pre_restore_${TIMESTAMP}.rdb.gz"

    if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
        cat "$REDIS_RDB_FILE" | gzip > "$safety_backup"; then

        local file_size
        file_size=$(du -h "$safety_backup" | cut -f1)
        log_success "Safety backup created: $safety_backup ($file_size)"
        return 0
    else
        log_error "Failed to create pre-restore backup"
        return 1
    fi
}

# Stop Redis container
stop_redis() {
    log_info "Stopping Redis container..."

    if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" stop "$REDIS_CONTAINER"; then
        log_success "Redis container stopped"
        return 0
    else
        log_error "Failed to stop Redis container"
        return 1
    fi
}

# Start Redis container
start_redis() {
    log_info "Starting Redis container..."

    if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" start "$REDIS_CONTAINER"; then
        log_success "Redis container started"

        # Wait for Redis to be ready
        log_info "Waiting for Redis to be ready..."
        local max_wait=30
        local waited=0

        while [[ $waited -lt $max_wait ]]; do
            if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
                redis-cli PING 2>/dev/null | grep -q "PONG"; then
                log_success "Redis is ready"
                return 0
            fi
            sleep 1
            ((waited++))
        done

        log_error "Redis did not become ready within ${max_wait}s"
        return 1
    else
        log_error "Failed to start Redis container"
        return 1
    fi
}

# Restore Redis RDB file
restore_redis() {
    local backup_file="$1"

    log_info "Starting Redis restore from: $backup_file"

    # Check if backup file exists
    if [[ ! -f "$backup_file" ]]; then
        log_error "Backup file not found: $backup_file"
        return 1
    fi

    # Create pre-restore backup
    if is_container_running "$REDIS_CONTAINER"; then
        if ! create_pre_restore_backup; then
            log_error "Pre-restore backup failed. Aborting restore."
            return 1
        fi
    fi

    # Stop Redis container
    if ! stop_redis; then
        log_error "Failed to stop Redis container"
        return 1
    fi

    # Replace RDB file in volume
    log_info "Replacing RDB file in Redis volume..."
    if decompress_backup "$backup_file" | \
       docker run --rm -i \
       -v "$REDIS_VOLUME:/data" \
       alpine:latest sh -c "cat > /data/dump.rdb"; then

        log_success "RDB file replaced successfully"
    else
        log_error "Failed to replace RDB file"
        start_redis  # Attempt to start Redis even if restore failed
        return 1
    fi

    # Start Redis container
    if ! start_redis; then
        log_error "Failed to start Redis container"
        return 1
    fi

    log_success "Redis restore completed"
    return 0
}

# Verify restored Redis
verify_restore() {
    log_info "Verifying restored Redis..."

    # Get database size
    local dbsize
    dbsize=$(docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
        redis-cli DBSIZE | tr -d '\r')

    log_info "Keys found: $dbsize"

    # Get Redis info
    local redis_version
    redis_version=$(docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
        redis-cli INFO server | grep "redis_version" | cut -d':' -f2 | tr -d '\r')

    log_info "Redis version: $redis_version"
    log_success "Redis verification passed"
    return 0
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    # Check arguments
    if [[ $# -ne 1 ]]; then
        usage
    fi

    local backup_file="$1"
    local start_time
    start_time=$(date +%s)

    log_info "=========================================="
    log_info "Redis Restore Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Confirm restore operation
    confirm_restore "$backup_file"

    # Perform restore
    if ! restore_redis "$backup_file"; then
        log_error "Redis restore failed"
        send_notification "Redis Restore Failed" \
            "Failed to restore from: $(basename "$backup_file")" \
            "error"
        exit 1
    fi

    # Verify restore
    if ! verify_restore; then
        log_error "Restore verification failed"
        exit 1
    fi

    # Calculate duration
    local end_time
    end_time=$(date +%s)
    local duration=$((end_time - start_time))

    log_success "Redis restore completed successfully in ${duration}s"
    log_info "=========================================="

    # Send success notification
    send_notification "Redis Restore Successful" \
        "Restored from: $(basename "$backup_file")\nDuration: ${duration}s" \
        "success"

    exit 0
}

# Run main function
main "$@"
