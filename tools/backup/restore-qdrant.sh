#!/bin/bash
# Qdrant Restore Script for MeepleAI
# Restores Qdrant collections from snapshot files

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
LOG_FILE="${LOG_DIR}/qdrant_restore_${TIMESTAMP}.log"

# ============================================================================
# FUNCTIONS
# ============================================================================

# Show usage information
usage() {
    echo "Usage: $0 <snapshot_file> [collection_name]"
    echo ""
    echo "Restore Qdrant collection from snapshot file"
    echo ""
    echo "Arguments:"
    echo "  snapshot_file     Path to snapshot file (.snapshot.gz or .snapshot.xz)"
    echo "  collection_name   Target collection name (optional, extracted from filename if not provided)"
    echo ""
    echo "Examples:"
    echo "  $0 backups/qdrant/rules_20250108_020000.snapshot.gz"
    echo "  $0 backups/qdrant/rules_20250108_020000.snapshot.gz my_collection"
    echo ""
    echo "WARNING: This will OVERWRITE the target collection!"
    exit 1
}

# Confirm restore operation
confirm_restore() {
    local snapshot_file="$1"
    local collection_name="$2"

    log_info "=========================================="
    log_info "⚠️  WARNING: QDRANT RESTORE OPERATION"
    log_info "=========================================="
    log_info "This will OVERWRITE the collection with:"
    log_info "  Snapshot file: $snapshot_file"
    log_info "  Collection: $collection_name"
    log_info ""
    log_info "Current collection data will be PERMANENTLY LOST!"
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

# Check Qdrant health
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

# Extract collection name from snapshot filename
extract_collection_name() {
    local snapshot_file="$1"
    local filename
    filename=$(basename "$snapshot_file")

    # Extract collection name (format: <collection>_<timestamp>.snapshot.[gz|xz])
    local collection_name
    collection_name=$(echo "$filename" | sed 's/_[0-9]\{8\}_[0-9]\{6\}\.snapshot\..*//')

    if [[ -z "$collection_name" ]]; then
        log_error "Could not extract collection name from filename: $filename"
        return 1
    fi

    echo "$collection_name"
}

# Decompress snapshot file
decompress_snapshot() {
    local snapshot_file="$1"
    local decompressed_file="${snapshot_file%.*}"  # Remove .gz or .xz extension

    log_info "Decompressing snapshot..."

    if [[ "$snapshot_file" == *.gz ]]; then
        if gunzip -c "$snapshot_file" > "$decompressed_file"; then
            echo "$decompressed_file"
            return 0
        fi
    elif [[ "$snapshot_file" == *.xz ]]; then
        if xzcat "$snapshot_file" > "$decompressed_file"; then
            echo "$decompressed_file"
            return 0
        fi
    elif [[ "$snapshot_file" == *.snapshot ]]; then
        echo "$snapshot_file"
        return 0
    else
        log_error "Unsupported snapshot file format: $snapshot_file"
        return 1
    fi

    log_error "Failed to decompress snapshot"
    return 1
}

# Upload snapshot to Qdrant
upload_snapshot() {
    local snapshot_file="$1"
    local collection_name="$2"

    log_info "Uploading snapshot to Qdrant..."

    # Upload via multipart form data
    if curl -sf -X POST "${QDRANT_URL}/collections/${collection_name}/snapshots/upload" \
        -H "Content-Type: multipart/form-data" \
        -F "snapshot=@${snapshot_file}" > /dev/null; then

        log_success "Snapshot uploaded successfully"
        return 0
    else
        log_error "Failed to upload snapshot to Qdrant"
        return 1
    fi
}

# Delete existing collection
delete_collection() {
    local collection_name="$1"

    log_info "Deleting existing collection: $collection_name"

    if curl -sf -X DELETE "${QDRANT_URL}/collections/${collection_name}" > /dev/null 2>&1; then
        log_success "Collection deleted: $collection_name"
        return 0
    else
        log_info "Collection does not exist or already deleted: $collection_name"
        return 0
    fi
}

# Recover collection from snapshot
recover_collection() {
    local snapshot_file="$1"
    local collection_name="$2"

    log_info "Recovering collection from snapshot..."

    # Get the snapshot name (basename without path)
    local snapshot_name
    snapshot_name=$(basename "$snapshot_file")

    # Recover collection via API
    local response
    response=$(curl -sf -X PUT "${QDRANT_URL}/collections/${collection_name}/snapshots/recover" \
        -H "Content-Type: application/json" \
        -d "{\"location\": \"file://snapshots/${snapshot_name}\"}" 2>&1) || {
        log_error "Failed to recover collection from snapshot"
        log_error "Response: $response"
        return 1
    }

    log_success "Collection recovered successfully"
    return 0
}

# Verify restored collection
verify_restore() {
    local collection_name="$1"

    log_info "Verifying restored collection..."

    # Check collection info
    local response
    response=$(curl -sf "${QDRANT_URL}/collections/${collection_name}" 2>&1) || {
        log_error "Failed to verify collection: $collection_name"
        return 1
    }

    # Extract point count from response (simple grep, for production consider jq)
    local point_count
    point_count=$(echo "$response" | grep -o '"points_count":[0-9]*' | cut -d':' -f2 || echo "0")

    log_info "Collection verified:"
    log_info "  Name: $collection_name"
    log_info "  Points: $point_count"

    if [[ $point_count -gt 0 ]]; then
        log_success "Collection verification passed"
        return 0
    else
        log_error "Collection verification failed: no points found"
        return 1
    fi
}

# Restore Qdrant collection
restore_qdrant() {
    local snapshot_file="$1"
    local collection_name="$2"

    log_info "Starting Qdrant restore..."

    # Check if snapshot file exists
    if [[ ! -f "$snapshot_file" ]]; then
        log_error "Snapshot file not found: $snapshot_file"
        return 1
    fi

    # Decompress snapshot if needed
    local decompressed_file
    decompressed_file=$(decompress_snapshot "$snapshot_file") || return 1

    # Delete existing collection
    delete_collection "$collection_name"

    # Upload snapshot to Qdrant
    if ! upload_snapshot "$decompressed_file" "$collection_name"; then
        log_error "Failed to upload snapshot"
        # Cleanup decompressed file if it's temporary
        [[ "$decompressed_file" != "$snapshot_file" ]] && rm -f "$decompressed_file"
        return 1
    fi

    # Cleanup decompressed file if it's temporary
    if [[ "$decompressed_file" != "$snapshot_file" ]]; then
        rm -f "$decompressed_file"
        log_info "Cleaned up temporary decompressed file"
    fi

    log_success "Qdrant restore completed"
    return 0
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    # Check arguments
    if [[ $# -lt 1 || $# -gt 2 ]]; then
        usage
    fi

    local snapshot_file="$1"
    local collection_name="${2:-}"

    # Extract collection name from filename if not provided
    if [[ -z "$collection_name" ]]; then
        collection_name=$(extract_collection_name "$snapshot_file") || {
            log_error "Failed to extract collection name"
            exit 1
        }
        log_info "Extracted collection name: $collection_name"
    fi

    local start_time
    start_time=$(date +%s)

    log_info "=========================================="
    log_info "Qdrant Restore Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Check Qdrant health
    if ! check_qdrant_health; then
        log_error "Qdrant health check failed"
        exit 1
    fi

    # Confirm restore operation
    confirm_restore "$snapshot_file" "$collection_name"

    # Perform restore
    if ! restore_qdrant "$snapshot_file" "$collection_name"; then
        log_error "Qdrant restore failed"
        send_notification "Qdrant Restore Failed" \
            "Failed to restore collection: $collection_name" \
            "error"
        exit 1
    fi

    # Verify restore
    if ! verify_restore "$collection_name"; then
        log_error "Restore verification failed"
        exit 1
    fi

    # Calculate duration
    local end_time
    end_time=$(date +%s)
    local duration=$((end_time - start_time))

    log_success "Qdrant restore completed successfully in ${duration}s"
    log_info "=========================================="

    # Send success notification
    send_notification "Qdrant Restore Successful" \
        "Collection: $collection_name\nDuration: ${duration}s" \
        "success"

    exit 0
}

# Run main function
main "$@"
