#!/bin/bash
# Master Backup Orchestrator for MeepleAI
# Coordinates backup of all services with parallel execution support

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
MASTER_LOG_FILE="${LOG_DIR}/backup_all_${TIMESTAMP}.log"

# Backup scripts to execute
BACKUP_SCRIPTS=(
    "${SCRIPT_DIR}/backup-postgres.sh"
    "${SCRIPT_DIR}/backup-qdrant.sh"
    "${SCRIPT_DIR}/backup-redis.sh"
    "${SCRIPT_DIR}/backup-volumes.sh"
)

# Execution mode: sequential or parallel
EXECUTION_MODE="${EXECUTION_MODE:-sequential}"

# ============================================================================
# FUNCTIONS
# ============================================================================

# Execute backup script and capture result
execute_backup() {
    local script="$1"
    local script_name
    script_name=$(basename "$script" .sh)

    log_info "Executing: $script_name"

    local script_log="${LOG_DIR}/${script_name}_${TIMESTAMP}.log"
    local start_time
    start_time=$(date +%s)

    if bash "$script" > "$script_log" 2>&1; then
        local end_time
        end_time=$(date +%s)
        local duration=$((end_time - start_time))

        log_success "$script_name completed successfully in ${duration}s"
        return 0
    else
        local end_time
        end_time=$(date +%s)
        local duration=$((end_time - start_time))

        log_error "$script_name failed after ${duration}s"
        log_error "Check log: $script_log"
        return 1
    fi
}

# Execute backups sequentially
run_sequential() {
    log_info "Running backups sequentially..."

    local success_count=0
    local fail_count=0

    for script in "${BACKUP_SCRIPTS[@]}"; do
        if execute_backup "$script"; then
            ((success_count++))
        else
            ((fail_count++))
        fi
    done

    return $fail_count
}

# Execute backups in parallel
run_parallel() {
    log_info "Running backups in parallel..."

    local pids=()
    local results=()

    # Start all backup scripts in background
    for script in "${BACKUP_SCRIPTS[@]}"; do
        execute_backup "$script" &
        pids+=($!)
    done

    # Wait for all background jobs and collect exit codes
    local success_count=0
    local fail_count=0

    for pid in "${pids[@]}"; do
        if wait "$pid"; then
            ((success_count++))
        else
            ((fail_count++))
        fi
    done

    log_info "Parallel execution summary: $success_count successful, $fail_count failed"
    return $fail_count
}

# Generate backup report
generate_report() {
    local total_backups
    local total_size
    local oldest_backup
    local newest_backup

    log_info "=========================================="
    log_info "Backup Report - $(date '+%Y-%m-%d %H:%M:%S')"
    log_info "=========================================="

    # PostgreSQL stats
    if [[ -d "$BACKUP_DIR_POSTGRES" ]]; then
        local pg_count
        pg_count=$(find "$BACKUP_DIR_POSTGRES" -name "postgres_*" -type f | wc -l)
        local pg_size
        pg_size=$(du -sh "$BACKUP_DIR_POSTGRES" 2>/dev/null | cut -f1)
        log_info "PostgreSQL: $pg_count backups, $pg_size total"
    fi

    # Qdrant stats
    if [[ -d "$BACKUP_DIR_QDRANT" ]]; then
        local qdrant_count
        qdrant_count=$(find "$BACKUP_DIR_QDRANT" -name "*.snapshot.*" -type f | wc -l)
        local qdrant_size
        qdrant_size=$(du -sh "$BACKUP_DIR_QDRANT" 2>/dev/null | cut -f1)
        log_info "Qdrant: $qdrant_count backups, $qdrant_size total"
    fi

    # Redis stats
    if [[ -d "$BACKUP_DIR_REDIS" ]]; then
        local redis_count
        redis_count=$(find "$BACKUP_DIR_REDIS" -name "redis_*" -type f | wc -l)
        local redis_size
        redis_size=$(du -sh "$BACKUP_DIR_REDIS" 2>/dev/null | cut -f1)
        log_info "Redis: $redis_count backups, $redis_size total"
    fi

    # Volumes stats
    if [[ -d "$BACKUP_DIR_VOLUMES" ]]; then
        local volumes_count
        volumes_count=$(find "$BACKUP_DIR_VOLUMES" -name "*.tar.*" -type f | wc -l)
        local volumes_size
        volumes_size=$(du -sh "$BACKUP_DIR_VOLUMES" 2>/dev/null | cut -f1)
        log_info "Volumes: $volumes_count backups, $volumes_size total"
    fi

    # Overall stats
    total_size=$(du -sh "$BACKUP_ROOT_DIR" 2>/dev/null | cut -f1)
    log_info "Total backup size: $total_size"
    log_info "=========================================="
}

# Check system resources before backup
check_resources() {
    log_info "Checking system resources..."

    # Check available disk space
    local available_space
    available_space=$(df -h "$BACKUP_ROOT_DIR" | awk 'NR==2 {print $4}')
    log_info "Available disk space: $available_space"

    # Check if Docker is running
    if ! docker info > /dev/null 2>&1; then
        log_error "Docker is not running"
        return 1
    fi

    # Check if Docker Compose services are running
    local running_services
    running_services=$(docker compose -f "${DOCKER_COMPOSE_DIR}/docker-compose.yml" ps --services --filter "status=running" | wc -l)
    log_info "Running Docker services: $running_services"

    return 0
}

# Parse command-line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --parallel)
                EXECUTION_MODE="parallel"
                shift
                ;;
            --sequential)
                EXECUTION_MODE="sequential"
                shift
                ;;
            --help|-h)
                echo "Usage: $0 [OPTIONS]"
                echo ""
                echo "Options:"
                echo "  --parallel      Run backups in parallel (faster but more resource-intensive)"
                echo "  --sequential    Run backups sequentially (default, more reliable)"
                echo "  --help, -h      Show this help message"
                echo ""
                echo "Environment Variables:"
                echo "  BACKUP_ROOT_DIR        Root directory for backups (default: ./backups)"
                echo "  RETENTION_DAYS         Days to keep backups (default: 30)"
                echo "  EXECUTION_MODE         Execution mode: parallel|sequential (default: sequential)"
                echo ""
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
    done
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    # Parse command-line arguments
    parse_args "$@"

    local start_time
    start_time=$(date +%s)

    log_info "=========================================="
    log_info "MeepleAI Master Backup Orchestrator"
    log_info "=========================================="
    log_info "Timestamp: $(date '+%Y-%m-%d %H:%M:%S')"
    log_info "Execution mode: $EXECUTION_MODE"
    log_info "Retention policy: $RETENTION_DAYS days"
    log_info "=========================================="

    # Validate configuration
    if ! validate_config; then
        log_error "Configuration validation failed"
        exit 1
    fi

    # Check system resources
    if ! check_resources; then
        log_error "Resource check failed"
        send_notification "Backup Failed" "System resources check failed" "error"
        exit 1
    fi

    # Execute backups based on mode
    local fail_count
    if [[ "$EXECUTION_MODE" == "parallel" ]]; then
        run_parallel
        fail_count=$?
    else
        run_sequential
        fail_count=$?
    fi

    # Generate report
    generate_report

    # Calculate total duration
    local end_time
    end_time=$(date +%s)
    local duration=$((end_time - start_time))

    # Final status
    if [[ $fail_count -eq 0 ]]; then
        log_success "All backups completed successfully in ${duration}s"
        send_notification "Backup Successful" \
            "All backups completed successfully in ${duration}s" \
            "success"
        exit 0
    else
        log_error "$fail_count backup(s) failed. Total time: ${duration}s"
        send_notification "Backup Partial Failure" \
            "$fail_count backup(s) failed after ${duration}s" \
            "error"
        exit 1
    fi
}

# Run main function with all arguments
main "$@"
