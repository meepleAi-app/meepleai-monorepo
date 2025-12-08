#!/bin/bash
# Qdrant Backup Script for MeepleAI
# Creates snapshots via Qdrant API and downloads them

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
LOG_FILE="${LOG_DIR}/qdrant_backup_${TIMESTAMP}.log"

# ============================================================================
# FUNCTIONS
# ============================================================================

# Check if Qdrant is accessible
check_qdrant_health() {
    log_info "Checking Qdrant health..."

    if curl -sf "${QDRANT_URL}/healthz" > /dev/null; then
        log_success "Qdrant is healthy"
        return 0
    else
        log_error "Qdrant is not accessible at $QDRANT_URL"
        return 1
    fi
}

# Create snapshot for a collection
create_collection_snapshot() {
    local collection="$1"

    log_info "Creating snapshot for collection: $collection"

    # Create snapshot via API
    local response
    response=$(curl -sf -X POST "${QDRANT_URL}/collections/${collection}/snapshots" \
        -H "Content-Type: application/json" 2>&1) || {
        log_error "Failed to create snapshot for collection: $collection"
        log_error "Response: $response"
        return 1
    }

    # Extract snapshot name from response
    local snapshot_name
    snapshot_name=$(echo "$response" | grep -o '"name":"[^"]*"' | cut -d'"' -f4)

    if [[ -z "$snapshot_name" ]]; then
        log_error "Could not extract snapshot name from API response"
        log_error "Response: $response"
        return 1
    fi

    log_success "Snapshot created: $snapshot_name"
    echo "$snapshot_name"
}

# Download snapshot
download_snapshot() {
    local collection="$1"
    local snapshot_name="$2"
    local output_file="${BACKUP_DIR_QDRANT}/${collection}_${TIMESTAMP}.snapshot"

    log_info "Downloading snapshot: $snapshot_name"

    if curl -sf "${QDRANT_URL}/collections/${collection}/snapshots/${snapshot_name}" \
        -o "$output_file"; then

        local file_size
        file_size=$(du -h "$output_file" | cut -f1)
        log_success "Snapshot downloaded: $output_file ($file_size)"
        echo "$output_file"
        return 0
    else
        log_error "Failed to download snapshot: $snapshot_name"
        rm -f "$output_file"  # Remove incomplete file
        return 1
    fi
}

# Delete snapshot from Qdrant (cleanup)
delete_snapshot() {
    local collection="$1"
    local snapshot_name="$2"

    log_info "Deleting snapshot from Qdrant: $snapshot_name"

    if curl -sf -X DELETE "${QDRANT_URL}/collections/${collection}/snapshots/${snapshot_name}" \
        > /dev/null; then
        log_success "Snapshot deleted from Qdrant: $snapshot_name"
        return 0
    else
        log_error "Failed to delete snapshot: $snapshot_name (will remain on server)"
        return 1
    fi
}

# Get list of collections
get_collections() {
    log_info "Fetching list of collections..."

    local response
    response=$(curl -sf "${QDRANT_URL}/collections" 2>&1) || {
        log_error "Failed to fetch collections list"
        log_error "Response: $response"
        return 1
    }

    # Extract collection names from JSON response
    # This is a simple grep-based extraction; for production, consider using jq
    local collections
    collections=$(echo "$response" | grep -o '"name":"[^"]*"' | cut -d'"' -f4)

    if [[ -z "$collections" ]]; then
        log_error "No collections found or failed to parse response"
        return 1
    fi

    echo "$collections"
}

# Backup all collections
backup_all_collections() {
    log_info "Starting Qdrant backup for all collections..."

    local collections
    collections=$(get_collections) || return 1

    local success_count=0
    local fail_count=0

    while IFS= read -r collection; do
        log_info "Processing collection: $collection"

        # Create snapshot
        local snapshot_name
        snapshot_name=$(create_collection_snapshot "$collection") || {
            log_error "Failed to create snapshot for: $collection"
            ((fail_count++))
            continue
        }

        # Download snapshot
        local downloaded_file
        downloaded_file=$(download_snapshot "$collection" "$snapshot_name") || {
            log_error "Failed to download snapshot for: $collection"
            ((fail_count++))
            continue
        }

        # Delete snapshot from Qdrant to save space
        delete_snapshot "$collection" "$snapshot_name" || {
            log_error "Warning: Snapshot not deleted from Qdrant: $snapshot_name"
        }

        # Compress snapshot
        log_info "Compressing snapshot..."
        if gzip "$downloaded_file"; then
            log_success "Snapshot compressed: ${downloaded_file}.gz"
            ((success_count++))
        else
            log_error "Failed to compress snapshot: $downloaded_file"
            ((fail_count++))
        fi

    done <<< "$collections"

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
    done < <(find "$BACKUP_DIR_QDRANT" -name "*.snapshot.gz" -mtime +"$COMPRESSION_THRESHOLD_DAYS" -print0 2>/dev/null)

    if [[ $compressed_count -gt 0 ]]; then
        log_success "Compressed $compressed_count old backup(s)"
    fi
}

# Clean old backups
cleanup_old_backups() {
    log_info "Cleaning up backups older than $RETENTION_DAYS days..."

    local deleted_count=0

    # Delete old .snapshot.xz files
    while IFS= read -r -d '' file; do
        rm -f "$file"
        ((deleted_count++))
        log_info "Deleted old backup: $file"
    done < <(find "$BACKUP_DIR_QDRANT" -name "*.snapshot.xz" -mtime +"$RETENTION_DAYS" -print0 2>/dev/null)

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

    total_backups=$(find "$BACKUP_DIR_QDRANT" -name "*.snapshot.*" -type f | wc -l)
    total_size=$(du -sh "$BACKUP_DIR_QDRANT" 2>/dev/null | cut -f1)

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
    log_info "Qdrant Backup Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Check Qdrant health
    if ! check_qdrant_health; then
        log_error "Qdrant health check failed"
        send_notification "Qdrant Backup Failed" "Qdrant is not accessible" "error"
        exit 1
    fi

    # Backup all collections
    if ! backup_all_collections; then
        log_error "Qdrant backup completed with errors"
        send_notification "Qdrant Backup Partial Failure" "Some collections failed to backup" "error"
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

    log_success "Qdrant backup completed successfully in ${duration}s"
    log_info "=========================================="

    # Send success notification
    send_notification "Qdrant Backup Successful" \
        "Backup completed in ${duration}s" \
        "success"

    exit 0
}

# Run main function
main "$@"
