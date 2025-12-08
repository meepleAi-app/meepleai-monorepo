#!/bin/bash
# Test Script for Backup and Restore Procedures
# Validates backup/restore functionality in test environment

set -euo pipefail

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Source configuration
# shellcheck source=./backup-config.sh
source "${SCRIPT_DIR}/backup-config.sh"

# ============================================================================
# TEST CONFIGURATION
# ============================================================================

TEST_LOG="${LOG_DIR}/test_backup_restore_$(date +%Y%m%d_%H%M%S).log"
TESTS_PASSED=0
TESTS_FAILED=0

# ============================================================================
# TEST FUNCTIONS
# ============================================================================

# Log test result
log_test() {
    local test_name="$1"
    local result="$2"

    if [[ "$result" == "PASS" ]]; then
        log_success "[TEST] $test_name: PASS"
        ((TESTS_PASSED++))
    else
        log_error "[TEST] $test_name: FAIL"
        ((TESTS_FAILED++))
    fi
}

# Test 1: Verify all scripts exist
test_scripts_exist() {
    local test_name="Scripts Existence"
    log_info "Running test: $test_name"

    local required_scripts=(
        "backup-config.sh"
        "backup-all.sh"
        "backup-postgres.sh"
        "backup-qdrant.sh"
        "backup-redis.sh"
        "backup-volumes.sh"
        "restore-postgres.sh"
        "restore-qdrant.sh"
        "restore-redis.sh"
        "restore-volumes.sh"
        "verify-backups.sh"
    )

    local missing=0
    for script in "${required_scripts[@]}"; do
        if [[ ! -f "${SCRIPT_DIR}/${script}" ]]; then
            log_error "Missing script: $script"
            ((missing++))
        fi
    done

    if [[ $missing -eq 0 ]]; then
        log_test "$test_name" "PASS"
        return 0
    else
        log_test "$test_name" "FAIL"
        return 1
    fi
}

# Test 2: Verify scripts are executable
test_scripts_executable() {
    local test_name="Scripts Executable"
    log_info "Running test: $test_name"

    local non_executable=0
    while IFS= read -r script; do
        if [[ ! -x "$script" ]]; then
            log_error "Not executable: $(basename "$script")"
            ((non_executable++))
        fi
    done < <(find "$SCRIPT_DIR" -name "*.sh" -type f)

    if [[ $non_executable -eq 0 ]]; then
        log_test "$test_name" "PASS"
        return 0
    else
        log_test "$test_name" "FAIL"
        return 1
    fi
}

# Test 3: Configuration validation
test_configuration() {
    local test_name="Configuration Validation"
    log_info "Running test: $test_name"

    if validate_config; then
        log_test "$test_name" "PASS"
        return 0
    else
        log_test "$test_name" "FAIL"
        return 1
    fi
}

# Test 4: Backup directories creation
test_backup_directories() {
    local test_name="Backup Directories"
    log_info "Running test: $test_name"

    local required_dirs=(
        "$BACKUP_ROOT_DIR"
        "$BACKUP_DIR_POSTGRES"
        "$BACKUP_DIR_QDRANT"
        "$BACKUP_DIR_REDIS"
        "$BACKUP_DIR_VOLUMES"
        "$LOG_DIR"
    )

    local missing=0
    for dir in "${required_dirs[@]}"; do
        if [[ ! -d "$dir" ]]; then
            log_error "Missing directory: $dir"
            ((missing++))
        fi
    done

    if [[ $missing -eq 0 ]]; then
        log_test "$test_name" "PASS"
        return 0
    else
        log_test "$test_name" "FAIL"
        return 1
    fi
}

# Test 5: PostgreSQL backup
test_postgres_backup() {
    local test_name="PostgreSQL Backup"
    log_info "Running test: $test_name"

    # Check if PostgreSQL is running
    if ! is_container_running "$POSTGRES_CONTAINER"; then
        log_error "PostgreSQL container not running - skipping test"
        log_test "$test_name" "SKIP"
        return 0
    fi

    # Run backup
    if bash "${SCRIPT_DIR}/backup-postgres.sh" > /dev/null 2>&1; then
        # Verify backup file was created
        local latest_backup
        latest_backup=$(find "$BACKUP_DIR_POSTGRES" -name "postgres_*.sql.gz" -type f -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2-)

        if [[ -n "$latest_backup" && -s "$latest_backup" ]]; then
            log_test "$test_name" "PASS"
            return 0
        else
            log_error "Backup file not found or empty"
            log_test "$test_name" "FAIL"
            return 1
        fi
    else
        log_test "$test_name" "FAIL"
        return 1
    fi
}

# Test 6: Redis backup
test_redis_backup() {
    local test_name="Redis Backup"
    log_info "Running test: $test_name"

    # Check if Redis is running
    if ! is_container_running "$REDIS_CONTAINER"; then
        log_error "Redis container not running - skipping test"
        log_test "$test_name" "SKIP"
        return 0
    fi

    # Run backup
    if bash "${SCRIPT_DIR}/backup-redis.sh" > /dev/null 2>&1; then
        # Verify backup file was created
        local latest_backup
        latest_backup=$(find "$BACKUP_DIR_REDIS" -name "redis_*.rdb.gz" -type f -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2-)

        if [[ -n "$latest_backup" && -s "$latest_backup" ]]; then
            log_test "$test_name" "PASS"
            return 0
        else
            log_error "Backup file not found or empty"
            log_test "$test_name" "FAIL"
            return 1
        fi
    else
        log_test "$test_name" "FAIL"
        return 1
    fi
}

# Test 7: Backup verification
test_backup_verification() {
    local test_name="Backup Verification"
    log_info "Running test: $test_name"

    if bash "${SCRIPT_DIR}/verify-backups.sh" > /dev/null 2>&1; then
        log_test "$test_name" "PASS"
        return 0
    else
        log_test "$test_name" "FAIL"
        return 1
    fi
}

# Test 8: Help messages
test_help_messages() {
    local test_name="Help Messages"
    log_info "Running test: $test_name"

    local scripts_with_args=(
        "restore-postgres.sh"
        "restore-qdrant.sh"
        "restore-redis.sh"
        "restore-volumes.sh"
    )

    local missing_help=0
    for script in "${scripts_with_args[@]}"; do
        if ! bash "${SCRIPT_DIR}/${script}" --help > /dev/null 2>&1; then
            log_error "No help message: $script"
            ((missing_help++))
        fi
    done

    if [[ $missing_help -eq 0 ]]; then
        log_test "$test_name" "PASS"
        return 0
    else
        log_test "$test_name" "FAIL"
        return 1
    fi
}

# ============================================================================
# MAIN EXECUTION
# ============================================================================

main() {
    log_info "=========================================="
    log_info "Backup & Restore Test Suite"
    log_info "=========================================="
    log_info "Timestamp: $(date '+%Y-%m-%d %H:%M:%S')"
    log_info "=========================================="

    # Run all tests
    test_scripts_exist
    test_scripts_executable
    test_configuration
    test_backup_directories
    test_postgres_backup
    test_redis_backup
    test_backup_verification
    test_help_messages

    # Summary
    log_info "=========================================="
    log_info "Test Summary"
    log_info "=========================================="
    log_info "Tests passed: $TESTS_PASSED"
    log_info "Tests failed: $TESTS_FAILED"
    log_info "Total tests: $((TESTS_PASSED + TESTS_FAILED))"
    log_info "=========================================="

    if [[ $TESTS_FAILED -eq 0 ]]; then
        log_success "All tests passed!"
        exit 0
    else
        log_error "$TESTS_FAILED test(s) failed"
        exit 1
    fi
}

# Run main function
main "$@"
