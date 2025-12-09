#!/bin/bash
# Docker Volumes Restore Script for MeepleAI
# Restores Docker volumes from tar.gz backups

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
LOG_FILE="${LOG_DIR}/volumes_restore_${TIMESTAMP}.log"

# ============================================================================
# FUNCTIONS
# ============================================================================

# Show usage information
usage() {
    echo "Usage: $0 <backup_file> <target_volume>"
    echo ""
    echo "Restore Docker volume from backup file"
    echo ""
    echo "Arguments:"
    echo "  backup_file      Path to backup file (.tar.gz or .tar.xz)"
    echo "  target_volume    Target Docker volume name"
    echo ""
    echo "Examples:"
    echo "  $0 backups/volumes/infra_grafanadata_20250108_020000.tar.gz infra_grafanadata"
    echo "  $0 backups/volumes/infra_n8ndata_20250101_020000.tar.xz infra_n8ndata"
    echo ""
    echo "WARNING: This will OVERWRITE the target volume!"
    exit 1
}

# Confirm restore operation
confirm_restore() {
    local backup_file="$1"
    local target_volume="$2"

    log_info "=========================================="
    log_info "⚠️  WARNING: VOLUME RESTORE OPERATION"
    log_info "=========================================="
    log_info "This will OVERWRITE the volume with:"
    log_info "  Backup file: $backup_file"
    log_info "  Target volume: $target_volume"
    log_info ""
    log_info "Current volume data will be PERMANENTLY LOST!"
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
decompress_command() {
    local backup_file="$1"

    if [[ "$backup_file" == *.tar.gz ]]; then
        echo "tar xzf"
    elif [[ "$backup_file" == *.tar.xz ]]; then
        echo "tar xJf"
    elif [[ "$backup_file" == *.tar ]]; then
        echo "tar xf"
    else
        log_error "Unsupported backup file format: $backup_file"
        log_error "Supported formats: .tar, .tar.gz, .tar.xz"
        return 1
    fi
}

# Create pre-restore backup
create_pre_restore_backup() {
    local target_volume="$1"

    log_info "Creating pre-restore backup as safety net..."

    local safety_backup="${BACKUP_DIR_VOLUMES}/pre_restore_$(basename "$target_volume")_${TIMESTAMP}.tar.gz"

    if docker run --rm \
        -v "$target_volume:/data:ro" \
        -v "$(cd "$BACKUP_DIR_VOLUMES" && pwd):/backup" \
        alpine:latest \
        tar czf "/backup/$(basename "$safety_backup")" -C /data . 2>/dev/null; then

        local file_size
        file_size=$(du -h "$safety_backup" | cut -f1)
        log_success "Safety backup created: $safety_backup ($file_size)"
        return 0
    else
        log_error "Failed to create pre-restore backup"
        return 1
    fi
}

# Restore Docker volume
restore_volume() {
    local backup_file="$1"
    local target_volume="$2"

    log_info "Starting volume restore from: $backup_file"

    # Check if backup file exists
    if [[ ! -f "$backup_file" ]]; then
        log_error "Backup file not found: $backup_file"
        return 1
    fi

    # Check if volume exists
    if docker volume inspect "$target_volume" > /dev/null 2>&1; then
        log_info "Target volume exists: $target_volume"

        # Create pre-restore backup
        if ! create_pre_restore_backup "$target_volume"; then
            log_error "Pre-restore backup failed. Aborting restore."
            return 1
        fi
    else
        log_info "Creating new volume: $target_volume"
        if ! docker volume create "$target_volume" > /dev/null; then
            log_error "Failed to create volume: $target_volume"
            return 1
        fi
    fi

    # Get decompress command
    local decompress_cmd
    decompress_cmd=$(decompress_command "$backup_file") || return 1

    # Clear volume contents
    log_info "Clearing volume contents..."
    docker run --rm \
        -v "$target_volume:/data" \
        alpine:latest \
        sh -c "rm -rf /data/* /data/..?* /data/.[!.]* 2>/dev/null || true"

    # Restore backup to volume
    log_info "Restoring backup to volume..."
    if docker run --rm \
        -v "$target_volume:/data" \
        -v "$(cd "$(dirname "$backup_file")" && pwd):/backup:ro" \
        alpine:latest \
        sh -c "cd /data && $decompress_cmd /backup/$(basename "$backup_file")"; then

        log_success "Volume restored successfully"
        return 0
    else
        log_error "Failed to restore volume"
        log_error "You can restore from safety backup: ${BACKUP_DIR_VOLUMES}/pre_restore_$(basename "$target_volume")_${TIMESTAMP}.tar.gz"
        return 1
    fi
}

# Verify restored volume
verify_restore() {
    local target_volume="$1"

    log_info "Verifying restored volume..."

    # Check if volume exists
    if ! docker volume inspect "$target_volume" > /dev/null 2>&1; then
        log_error "Volume not found: $target_volume"
        return 1
    fi

    # Count files in volume
    local file_count
    file_count=$(docker run --rm \
        -v "$target_volume:/data:ro" \
        alpine:latest \
        sh -c "find /data -type f | wc -l")

    # Get volume size
    local volume_size
    volume_size=$(docker run --rm \
        -v "$target_volume:/data:ro" \
        alpine:latest \
        sh -c "du -sh /data" | cut -f1)

    log_info "Volume verification:"
    log_info "  Volume: $target_volume"
    log_info "  Files: $file_count"
    log_info "  Size: $volume_size"

    if [[ $file_count -gt 0 ]]; then
        log_success "Volume verification passed"
        return 0
    else
        log_error "Volume verification failed: no files found"
        return 1
    fi
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    # Check arguments
    if [[ $# -ne 2 ]]; then
        usage
    fi

    local backup_file="$1"
    local target_volume="$2"
    local start_time
    start_time=$(date +%s)

    log_info "=========================================="
    log_info "Docker Volume Restore Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Confirm restore operation
    confirm_restore "$backup_file" "$target_volume"

    # Perform restore
    if ! restore_volume "$backup_file" "$target_volume"; then
        log_error "Volume restore failed"
        send_notification "Volume Restore Failed" \
            "Failed to restore volume: $target_volume" \
            "error"
        exit 1
    fi

    # Verify restore
    if ! verify_restore "$target_volume"; then
        log_error "Restore verification failed"
        exit 1
    fi

    # Calculate duration
    local end_time
    end_time=$(date +%s)
    local duration=$((end_time - start_time))

    log_success "Volume restore completed successfully in ${duration}s"
    log_info "=========================================="

    # Send success notification
    send_notification "Volume Restore Successful" \
        "Volume: $target_volume\nDuration: ${duration}s" \
        "success"

    exit 0
}

# Run main function
main "$@"
