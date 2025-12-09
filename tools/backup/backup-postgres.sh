#!/bin/bash
# PostgreSQL Backup Script for MeepleAI
# Performs logical backup using pg_dump with compression

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
BACKUP_FILE="${BACKUP_DIR_POSTGRES}/postgres_${TIMESTAMP}.sql.gz"
LOG_FILE="${LOG_DIR}/postgres_backup_${TIMESTAMP}.log"

# ============================================================================
# FUNCTIONS
# ============================================================================

# Perform PostgreSQL backup
backup_postgres() {
    log_info "Starting PostgreSQL backup..."

    # Check if PostgreSQL container is running
    if ! is_container_running "$POSTGRES_CONTAINER"; then
        log_error "PostgreSQL container '$POSTGRES_CONTAINER' is not running"
        return 1
    fi

    # Create backup using pg_dump
    log_info "Running pg_dump..."
    if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$POSTGRES_CONTAINER" \
        pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" | gzip > "$BACKUP_FILE"; then

        local file_size
        file_size=$(du -h "$BACKUP_FILE" | cut -f1)
        log_success "PostgreSQL backup completed: $BACKUP_FILE ($file_size)"

        # Verify backup file is not empty
        if [[ ! -s "$BACKUP_FILE" ]]; then
            log_error "Backup file is empty!"
            return 1
        fi

        return 0
    else
        log_error "pg_dump failed"
        rm -f "$BACKUP_FILE"  # Remove incomplete backup
        return 1
    fi
}

# Compress old backups with xz (better compression for long-term storage)
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
            rm -f "$xz_file"  # Remove incomplete file
        fi
    done < <(find "$BACKUP_DIR_POSTGRES" -name "postgres_*.sql.gz" -mtime +"$COMPRESSION_THRESHOLD_DAYS" -print0 2>/dev/null)

    if [[ $compressed_count -gt 0 ]]; then
        log_success "Compressed $compressed_count old backup(s)"
    fi
}

# Clean old backups based on retention policy
cleanup_old_backups() {
    log_info "Cleaning up backups older than $RETENTION_DAYS days..."

    local deleted_count=0

    # Delete old .sql.xz files (compressed backups)
    while IFS= read -r -d '' file; do
        rm -f "$file"
        ((deleted_count++))
        log_info "Deleted old backup: $file"
    done < <(find "$BACKUP_DIR_POSTGRES" -name "postgres_*.sql.xz" -mtime +"$RETENTION_DAYS" -print0 2>/dev/null)

    if [[ $deleted_count -gt 0 ]]; then
        log_success "Deleted $deleted_count old backup(s)"
    else
        log_info "No old backups to delete"
    fi
}

# Calculate backup statistics
show_backup_stats() {
    local total_backups
    local total_size
    local oldest_backup
    local newest_backup

    total_backups=$(find "$BACKUP_DIR_POSTGRES" -name "postgres_*" -type f | wc -l)
    total_size=$(du -sh "$BACKUP_DIR_POSTGRES" 2>/dev/null | cut -f1)

    if [[ $total_backups -gt 0 ]]; then
        oldest_backup=$(find "$BACKUP_DIR_POSTGRES" -name "postgres_*" -type f -printf '%T+ %p\n' | sort | head -1 | cut -d' ' -f2- | xargs basename)
        newest_backup=$(find "$BACKUP_DIR_POSTGRES" -name "postgres_*" -type f -printf '%T+ %p\n' | sort | tail -1 | cut -d' ' -f2- | xargs basename)

        log_info "Backup Statistics:"
        log_info "  Total backups: $total_backups"
        log_info "  Total size: $total_size"
        log_info "  Oldest: $oldest_backup"
        log_info "  Newest: $newest_backup"
    fi
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    local start_time
    start_time=$(date +%s)

    log_info "=========================================="
    log_info "PostgreSQL Backup Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Perform backup
    if ! backup_postgres; then
        log_error "PostgreSQL backup failed"
        send_notification "PostgreSQL Backup Failed" "Backup failed at $(date)" "error"
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

    log_success "PostgreSQL backup completed successfully in ${duration}s"
    log_info "=========================================="

    # Send success notification
    send_notification "PostgreSQL Backup Successful" \
        "Backup completed in ${duration}s\nFile: $(basename "$BACKUP_FILE")" \
        "success"

    exit 0
}

# Run main function
main "$@"
