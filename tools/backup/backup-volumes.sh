#!/bin/bash
# Docker Volumes Backup Script for MeepleAI
# Backs up Docker volumes for n8n, Grafana, and Prometheus

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
LOG_FILE="${LOG_DIR}/volumes_backup_${TIMESTAMP}.log"

# Volumes to backup (exclude postgres, redis, qdrant - they have dedicated scripts)
VOLUMES_TO_BACKUP=(
    "$GRAFANA_VOLUME"
    "$PROMETHEUS_VOLUME"
    "$N8N_VOLUME"
)

# ============================================================================
# FUNCTIONS
# ============================================================================

# Backup a single Docker volume
backup_volume() {
    local volume="$1"
    local volume_name
    volume_name=$(basename "$volume")
    local backup_file="${BACKUP_DIR_VOLUMES}/${volume_name}_${TIMESTAMP}.tar.gz"

    log_info "Backing up volume: $volume"

    # Check if volume exists
    if ! docker volume inspect "$volume" > /dev/null 2>&1; then
        log_error "Volume does not exist: $volume"
        return 1
    fi

    # Create backup using temporary container
    # Mount volume as /data and backup directory as /backup
    if docker run --rm \
        -v "$volume:/data:ro" \
        -v "$(cd "$BACKUP_DIR_VOLUMES" && pwd):/backup" \
        alpine:latest \
        tar czf "/backup/$(basename "$backup_file")" -C /data . 2>/dev/null; then

        local file_size
        file_size=$(du -h "$backup_file" | cut -f1)
        log_success "Volume backup completed: $backup_file ($file_size)"

        # Verify backup file is not empty
        if [[ ! -s "$backup_file" ]]; then
            log_error "Backup file is empty: $backup_file"
            rm -f "$backup_file"
            return 1
        fi

        return 0
    else
        log_error "Failed to backup volume: $volume"
        rm -f "$backup_file"  # Remove incomplete backup
        return 1
    fi
}

# Backup all configured volumes
backup_all_volumes() {
    log_info "Starting Docker volumes backup..."

    local success_count=0
    local fail_count=0

    for volume in "${VOLUMES_TO_BACKUP[@]}"; do
        if backup_volume "$volume"; then
            ((success_count++))
        else
            ((fail_count++))
        fi
    done

    log_info "Backup summary: $success_count successful, $fail_count failed"

    if [[ $fail_count -gt 0 ]]; then
        return 1
    fi

    return 0
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
    done < <(find "$BACKUP_DIR_VOLUMES" -name "*.tar.gz" -mtime +"$COMPRESSION_THRESHOLD_DAYS" -print0 2>/dev/null)

    if [[ $compressed_count -gt 0 ]]; then
        log_success "Compressed $compressed_count old backup(s)"
    fi
}

# Clean old backups
cleanup_old_backups() {
    log_info "Cleaning up backups older than $RETENTION_DAYS days..."

    local deleted_count=0

    # Delete old .tar.xz files
    while IFS= read -r -d '' file; do
        rm -f "$file"
        ((deleted_count++))
        log_info "Deleted old backup: $file"
    done < <(find "$BACKUP_DIR_VOLUMES" -name "*.tar.xz" -mtime +"$RETENTION_DAYS" -print0 2>/dev/null)

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

    total_backups=$(find "$BACKUP_DIR_VOLUMES" -name "*.tar.*" -type f | wc -l)
    total_size=$(du -sh "$BACKUP_DIR_VOLUMES" 2>/dev/null | cut -f1)

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
    log_info "Docker Volumes Backup Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Backup all volumes
    if ! backup_all_volumes; then
        log_error "Docker volumes backup completed with errors"
        send_notification "Volumes Backup Partial Failure" "Some volumes failed to backup" "error"
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

    log_success "Docker volumes backup completed successfully in ${duration}s"
    log_info "=========================================="

    # Send success notification
    send_notification "Volumes Backup Successful" \
        "Backup completed in ${duration}s" \
        "success"

    exit 0
}

# Run main function
main "$@"
