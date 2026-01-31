#!/bin/bash
# Backup Verification Script for MeepleAI
# Verifies integrity and completeness of backup files

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
LOG_FILE="${LOG_DIR}/verify_backups_${TIMESTAMP}.log"

# Minimum expected backup file sizes (in bytes)
MIN_POSTGRES_SIZE=1024         # 1 KB minimum
MIN_QDRANT_SIZE=512           # 512 bytes minimum
MIN_REDIS_SIZE=256            # 256 bytes minimum
MIN_VOLUME_SIZE=512           # 512 bytes minimum

# Maximum age for recent backup (in hours)
MAX_BACKUP_AGE_HOURS=48       # Alert if no backup in last 48 hours

# ============================================================================
# FUNCTIONS
# ============================================================================

# Verify single backup file
verify_file() {
    local file="$1"
    local min_size="$2"
    local file_type="$3"

    # Check if file exists
    if [[ ! -f "$file" ]]; then
        log_error "[$file_type] File not found: $file"
        return 1
    fi

    # Check file size
    local file_size
    file_size=$(stat -c%s "$file" 2>/dev/null || stat -f%z "$file" 2>/dev/null)

    if [[ $file_size -lt $min_size ]]; then
        log_error "[$file_type] File too small: $file (${file_size} bytes, minimum: ${min_size} bytes)"
        return 1
    fi

    # Verify compression integrity based on extension
    if [[ "$file" == *.gz ]]; then
        if gunzip -t "$file" 2>/dev/null; then
            log_success "[$file_type] Valid gzip file: $(basename "$file") (${file_size} bytes)"
        else
            log_error "[$file_type] Corrupted gzip file: $file"
            return 1
        fi
    elif [[ "$file" == *.xz ]]; then
        if xz -t "$file" 2>/dev/null; then
            log_success "[$file_type] Valid xz file: $(basename "$file") (${file_size} bytes)"
        else
            log_error "[$file_type] Corrupted xz file: $file"
            return 1
        fi
    else
        log_success "[$file_type] Valid file: $(basename "$file") (${file_size} bytes)"
    fi

    return 0
}

# Verify PostgreSQL backups
verify_postgres_backups() {
    log_info "Verifying PostgreSQL backups..."

    local total=0
    local valid=0
    local invalid=0

    while IFS= read -r -d '' file; do
        ((total++))
        if verify_file "$file" "$MIN_POSTGRES_SIZE" "PostgreSQL"; then
            ((valid++))
        else
            ((invalid++))
        fi
    done < <(find "$BACKUP_DIR_POSTGRES" -name "postgres_*.sql.*" -type f -print0 2>/dev/null)

    log_info "PostgreSQL verification: $valid valid, $invalid invalid (total: $total)"
    return $invalid
}

# Verify Qdrant backups
verify_qdrant_backups() {
    log_info "Verifying Qdrant backups..."

    local total=0
    local valid=0
    local invalid=0

    while IFS= read -r -d '' file; do
        ((total++))
        if verify_file "$file" "$MIN_QDRANT_SIZE" "Qdrant"; then
            ((valid++))
        else
            ((invalid++))
        fi
    done < <(find "$BACKUP_DIR_QDRANT" -name "*.snapshot.*" -type f -print0 2>/dev/null)

    log_info "Qdrant verification: $valid valid, $invalid invalid (total: $total)"
    return $invalid
}

# Verify Redis backups
verify_redis_backups() {
    log_info "Verifying Redis backups..."

    local total=0
    local valid=0
    local invalid=0

    while IFS= read -r -d '' file; do
        ((total++))
        if verify_file "$file" "$MIN_REDIS_SIZE" "Redis"; then
            ((valid++))
        else
            ((invalid++))
        fi
    done < <(find "$BACKUP_DIR_REDIS" -name "redis_*.rdb.*" -type f -print0 2>/dev/null)

    log_info "Redis verification: $valid valid, $invalid invalid (total: $total)"
    return $invalid
}

# Verify volume backups
verify_volume_backups() {
    log_info "Verifying volume backups..."

    local total=0
    local valid=0
    local invalid=0

    while IFS= read -r -d '' file; do
        ((total++))
        if verify_file "$file" "$MIN_VOLUME_SIZE" "Volume"; then
            ((valid++))
        else
            ((invalid++))
        fi
    done < <(find "$BACKUP_DIR_VOLUMES" -name "*.tar.*" -type f -print0 2>/dev/null)

    log_info "Volume verification: $valid valid, $invalid invalid (total: $total)"
    return $invalid
}

# Check for recent backups
check_recent_backups() {
    log_info "Checking for recent backups (last ${MAX_BACKUP_AGE_HOURS} hours)..."

    local warnings=0

    # Check each backup type
    local backup_types=(
        "PostgreSQL:$BACKUP_DIR_POSTGRES:postgres_*.sql.*"
        "Qdrant:$BACKUP_DIR_QDRANT:*.snapshot.*"
        "Redis:$BACKUP_DIR_REDIS:redis_*.rdb.*"
        "Volumes:$BACKUP_DIR_VOLUMES:*.tar.*"
    )

    for backup_type_info in "${backup_types[@]}"; do
        IFS=':' read -r type_name type_dir type_pattern <<< "$backup_type_info"

        local newest_file
        newest_file=$(find "$type_dir" -name "$type_pattern" -type f -printf '%T@ %p\n' 2>/dev/null | sort -rn | head -1 | cut -d' ' -f2-)

        if [[ -z "$newest_file" ]]; then
            log_error "[$type_name] No backups found!"
            ((warnings++))
            continue
        fi

        local file_age_hours
        local current_time
        local file_time
        current_time=$(date +%s)
        file_time=$(stat -c%Y "$newest_file" 2>/dev/null || stat -f%m "$newest_file" 2>/dev/null)
        file_age_hours=$(( (current_time - file_time) / 3600 ))

        if [[ $file_age_hours -gt $MAX_BACKUP_AGE_HOURS ]]; then
            log_error "[$type_name] Newest backup is ${file_age_hours} hours old (max: ${MAX_BACKUP_AGE_HOURS})"
            log_error "[$type_name] File: $(basename "$newest_file")"
            ((warnings++))
        else
            log_success "[$type_name] Recent backup found (${file_age_hours} hours old)"
        fi
    done

    return $warnings
}

# Generate verification report
generate_report() {
    log_info "=========================================="
    log_info "Backup Verification Report"
    log_info "=========================================="
    log_info "Generated: $(date '+%Y-%m-%d %H:%M:%S')"
    log_info ""

    # Count all backups
    local total_postgres
    local total_qdrant
    local total_redis
    local total_volumes

    total_postgres=$(find "$BACKUP_DIR_POSTGRES" -name "postgres_*" -type f 2>/dev/null | wc -l)
    total_qdrant=$(find "$BACKUP_DIR_QDRANT" -name "*.snapshot.*" -type f 2>/dev/null | wc -l)
    total_redis=$(find "$BACKUP_DIR_REDIS" -name "redis_*" -type f 2>/dev/null | wc -l)
    total_volumes=$(find "$BACKUP_DIR_VOLUMES" -name "*.tar.*" -type f 2>/dev/null | wc -l)

    log_info "Backup counts:"
    log_info "  PostgreSQL: $total_postgres"
    log_info "  Qdrant: $total_qdrant"
    log_info "  Redis: $total_redis"
    log_info "  Volumes: $total_volumes"
    log_info ""

    # Disk usage
    local total_size
    total_size=$(du -sh "$BACKUP_ROOT_DIR" 2>/dev/null | cut -f1)
    log_info "Total backup size: $total_size"

    # Available disk space
    local available_space
    available_space=$(df -h "$BACKUP_ROOT_DIR" | awk 'NR==2 {print $4}')
    log_info "Available disk space: $available_space"

    log_info "=========================================="
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    local start_time
    start_time=$(date +%s)

    log_info "=========================================="
    log_info "Backup Verification Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Verify all backup types
    local total_errors=0

    verify_postgres_backups || ((total_errors += $?))
    verify_qdrant_backups || ((total_errors += $?))
    verify_redis_backups || ((total_errors += $?))
    verify_volume_backups || ((total_errors += $?))

    # Check for recent backups
    check_recent_backups || ((total_errors += $?))

    # Generate report
    generate_report

    # Calculate duration
    local end_time
    end_time=$(date +%s)
    local duration=$((end_time - start_time))

    # Final status
    if [[ $total_errors -eq 0 ]]; then
        log_success "All backups verified successfully in ${duration}s"
        send_notification "Backup Verification Successful" \
            "All backups are valid and recent" \
            "success"
        exit 0
    else
        log_error "Backup verification found $total_errors issue(s)"
        send_notification "Backup Verification Failed" \
            "$total_errors issue(s) found during verification" \
            "error"
        exit 1
    fi
}

# Run main function
main "$@"
