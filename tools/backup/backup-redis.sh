#!/bin/bash
# Redis Backup Script for MeepleAI
# Copies RDB snapshot from Redis volume

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
BACKUP_FILE="${BACKUP_DIR_REDIS}/redis_${TIMESTAMP}.rdb.gz"
LOG_FILE="${LOG_DIR}/redis_backup_${TIMESTAMP}.log"

# Redis RDB file location inside container
REDIS_RDB_FILE="/data/dump.rdb"

# ============================================================================
# FUNCTIONS
# ============================================================================

# Trigger Redis BGSAVE to create fresh snapshot
trigger_redis_save() {
    log_info "Triggering Redis BGSAVE..."

    if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
        redis-cli BGSAVE > /dev/null; then
        log_success "Redis BGSAVE triggered"

        # Wait for BGSAVE to complete
        log_info "Waiting for BGSAVE to complete..."
        local max_wait=30  # seconds
        local waited=0

        while [[ $waited -lt $max_wait ]]; do
            local last_save
            last_save=$(docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
                redis-cli LASTSAVE)

            # Check if BGSAVE is done (compare with current time)
            if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
                redis-cli INFO persistence | grep -q "rdb_bgsave_in_progress:0"; then
                log_success "BGSAVE completed"
                return 0
            fi

            sleep 1
            ((waited++))
        done

        log_error "BGSAVE timed out after ${max_wait}s"
        return 1
    else
        log_error "Failed to trigger Redis BGSAVE"
        return 1
    fi
}

# Backup Redis RDB file
backup_redis() {
    log_info "Starting Redis backup..."

    # Check if Redis container is running
    if ! is_container_running "$REDIS_CONTAINER"; then
        log_error "Redis container '$REDIS_CONTAINER' is not running"
        return 1
    fi

    # Trigger BGSAVE for fresh snapshot
    if ! trigger_redis_save; then
        log_error "Failed to trigger Redis save"
        return 1
    fi

    # Copy RDB file from container and compress
    log_info "Copying RDB file from Redis container..."
    if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$REDIS_CONTAINER" \
        cat "$REDIS_RDB_FILE" | gzip > "$BACKUP_FILE"; then

        local file_size
        file_size=$(du -h "$BACKUP_FILE" | cut -f1)
        log_success "Redis backup completed: $BACKUP_FILE ($file_size)"

        # Verify backup file is not empty
        if [[ ! -s "$BACKUP_FILE" ]]; then
            log_error "Backup file is empty!"
            return 1
        fi

        return 0
    else
        log_error "Failed to copy RDB file"
        rm -f "$BACKUP_FILE"  # Remove incomplete backup
        return 1
    fi
}

# Compress old backups with xz
compress_old_backups() {
    log_info "Compressing old backups (older than $COMPRESSION_THRESHOLD_DAYS days)..."

    local compressed_count=0
    while IFS= read -r -d '' file; do
        local xz_file="${file%.gz}.xz"

        # Skip if already compressed with xz
        if [[ -f "$xz_file" ]]; then
            continue
        fi

        log_info "Compressing: $file"
        if gunzip -c "$file" | xz > "$xz_file"; then
            rm -f "$file"
            ((compressed_count++))
            log_info "Compressed to: $xz_file"
        else
            log_error "Failed to compress: $file"
            rm -f "$xz_file"
        fi
    done < <(find "$BACKUP_DIR_REDIS" -name "redis_*.rdb.gz" -mtime +"$COMPRESSION_THRESHOLD_DAYS" -print0 2>/dev/null)

    if [[ $compressed_count -gt 0 ]]; then
        log_success "Compressed $compressed_count old backup(s)"
    fi
}

# Clean old backups
cleanup_old_backups() {
    log_info "Cleaning up backups older than $RETENTION_DAYS days..."

    local deleted_count=0

    # Delete old .rdb.xz files
    while IFS= read -r -d '' file; do
        rm -f "$file"
        ((deleted_count++))
        log_info "Deleted old backup: $file"
    done < <(find "$BACKUP_DIR_REDIS" -name "redis_*.rdb.xz" -mtime +"$RETENTION_DAYS" -print0 2>/dev/null)

    if [[ $deleted_count -gt 0 ]]; then
        log_success "Deleted $deleted_count old backup(s)"
    else
        log_info "No old backups to delete"
    fi
}

# Show backup statistics
show_backup_stats() {
    local total_backups
    local total_size

    total_backups=$(find "$BACKUP_DIR_REDIS" -name "redis_*" -type f | wc -l)
    total_size=$(du -sh "$BACKUP_DIR_REDIS" 2>/dev/null | cut -f1)

    if [[ $total_backups -gt 0 ]]; then
        log_info "Backup Statistics:"
        log_info "  Total backups: $total_backups"
        log_info "  Total size: $total_size"
    fi
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    local start_time
    start_time=$(date +%s)

    log_info "=========================================="
    log_info "Redis Backup Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Perform backup
    if ! backup_redis; then
        log_error "Redis backup failed"
        send_notification "Redis Backup Failed" "Backup failed at $(date)" "error"
        exit 1
    fi

    # Compress old backups
    compress_old_backups

    # Cleanup old backups
    cleanup_old_backups

    # Show statistics
    show_backup_stats

    # Calculate duration
    local end_time
    end_time=$(date +%s)
    local duration=$((end_time - start_time))

    log_success "Redis backup completed successfully in ${duration}s"
    log_info "=========================================="

    # Send success notification
    send_notification "Redis Backup Successful" \
        "Backup completed in ${duration}s\nFile: $(basename "$BACKUP_FILE")" \
        "success"

    exit 0
}

# Run main function
main "$@"
