#!/bin/bash
# PostgreSQL Restore Script for MeepleAI
# Restores PostgreSQL database from backup file

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
LOG_FILE="${LOG_DIR}/postgres_restore_${TIMESTAMP}.log"

# ============================================================================
# FUNCTIONS
# ============================================================================

# Show usage information
usage() {
    echo "Usage: $0 <backup_file>"
    echo ""
    echo "Restore PostgreSQL database from backup file"
    echo ""
    echo "Arguments:"
    echo "  backup_file    Path to backup file (.sql.gz or .sql.xz)"
    echo ""
    echo "Examples:"
    echo "  $0 backups/postgres/postgres_20250108_020000.sql.gz"
    echo "  $0 backups/postgres/postgres_20250101_020000.sql.xz"
    echo ""
    echo "WARNING: This will OVERWRITE the current database!"
    echo "         Make sure you have a recent backup before proceeding."
    exit 1
}

# Confirm restore operation
confirm_restore() {
    local backup_file="$1"

    log_info "=========================================="
    log_info "⚠️  WARNING: DATABASE RESTORE OPERATION"
    log_info "=========================================="
    log_info "This will OVERWRITE the current database with:"
    log_info "  Backup file: $backup_file"
    log_info "  Database: $POSTGRES_DB"
    log_info ""
    log_info "Current database will be PERMANENTLY LOST!"
    log_info "=========================================="

    # In interactive mode, ask for confirmation
    if [[ -t 0 ]]; then
        read -r -p "Type 'YES' to confirm restore: " response
        if [[ "$response" != "YES" ]]; then
            log_info "Restore cancelled by user"
            exit 0
        fi
    else
        # In non-interactive mode (automation), require FORCE_RESTORE=1
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
    elif [[ "$backup_file" == *.sql ]]; then
        cat "$backup_file"
    else
        log_error "Unsupported backup file format: $backup_file"
        log_error "Supported formats: .sql, .sql.gz, .sql.xz"
        return 1
    fi
}

# Create pre-restore backup (safety net)
create_pre_restore_backup() {
    log_info "Creating pre-restore backup as safety net..."

    local safety_backup="${BACKUP_DIR_POSTGRES}/pre_restore_${TIMESTAMP}.sql.gz"

    if docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$POSTGRES_CONTAINER" \
        pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" | gzip > "$safety_backup"; then

        local file_size
        file_size=$(du -h "$safety_backup" | cut -f1)
        log_success "Safety backup created: $safety_backup ($file_size)"
        return 0
    else
        log_error "Failed to create pre-restore backup"
        return 1
    fi
}

# Restore PostgreSQL database
restore_postgres() {
    local backup_file="$1"

    log_info "Starting PostgreSQL restore from: $backup_file"

    # Check if PostgreSQL container is running
    if ! is_container_running "$POSTGRES_CONTAINER"; then
        log_error "PostgreSQL container '$POSTGRES_CONTAINER' is not running"
        return 1
    fi

    # Check if backup file exists
    if [[ ! -f "$backup_file" ]]; then
        log_error "Backup file not found: $backup_file"
        return 1
    fi

    # Create pre-restore backup
    if ! create_pre_restore_backup; then
        log_error "Pre-restore backup failed. Aborting restore."
        return 1
    fi

    # Drop existing connections to database
    log_info "Terminating existing connections to database..."
    docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$POSTGRES_CONTAINER" \
        psql -U "$POSTGRES_USER" -d postgres -c \
        "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$POSTGRES_DB' AND pid <> pg_backend_pid();" \
        > /dev/null 2>&1 || true

    # Drop and recreate database
    log_info "Dropping and recreating database..."
    docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$POSTGRES_CONTAINER" \
        psql -U "$POSTGRES_USER" -d postgres -c "DROP DATABASE IF EXISTS $POSTGRES_DB;" || {
        log_error "Failed to drop database"
        return 1
    }

    docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$POSTGRES_CONTAINER" \
        psql -U "$POSTGRES_USER" -d postgres -c "CREATE DATABASE $POSTGRES_DB;" || {
        log_error "Failed to create database"
        return 1
    }

    # Restore database from backup
    log_info "Restoring database from backup..."
    if decompress_backup "$backup_file" | \
       docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$POSTGRES_CONTAINER" \
       psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" > /dev/null 2>&1; then

        log_success "Database restored successfully"
        return 0
    else
        log_error "Database restore failed"
        log_error "You can restore from safety backup: ${BACKUP_DIR_POSTGRES}/pre_restore_${TIMESTAMP}.sql.gz"
        return 1
    fi
}

# Verify restored database
verify_restore() {
    log_info "Verifying restored database..."

    # Count tables
    local table_count
    table_count=$(docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" exec -T "$POSTGRES_CONTAINER" \
        psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -t -c \
        "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" | tr -d ' ')

    log_info "Tables found: $table_count"

    if [[ $table_count -gt 0 ]]; then
        log_success "Database verification passed"
        return 0
    else
        log_error "Database verification failed: no tables found"
        return 1
    fi
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
    log_info "PostgreSQL Restore Script"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Confirm restore operation
    confirm_restore "$backup_file"

    # Perform restore
    if ! restore_postgres "$backup_file"; then
        log_error "PostgreSQL restore failed"
        send_notification "PostgreSQL Restore Failed" \
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

    log_success "PostgreSQL restore completed successfully in ${duration}s"
    log_info "=========================================="

    # Send success notification
    send_notification "PostgreSQL Restore Successful" \
        "Restored from: $(basename "$backup_file")\nDuration: ${duration}s" \
        "success"

    exit 0
}

# Run main function
main "$@"
