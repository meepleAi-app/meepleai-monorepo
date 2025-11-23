#!/bin/bash
#
# TRIAGE PHASE 1: Issue Priority Normalization Script
#
# Purpose: Convert all issues with priority labels to standardized [Px] title format
# Author: Engineering Team
# Date: 2025-11-23
#
# Usage:
#   ./triage-phase1-normalize.sh --dry-run    # Preview changes without applying
#   ./triage-phase1-normalize.sh --execute    # Apply changes to GitHub issues
#
# Requirements:
#   - GitHub CLI (gh) installed and authenticated
#   - Write permissions on repository
#   - jq installed for JSON processing
#
# Mapping:
#   priority-critical / priority: critical → [P0]
#   priority-high / priority: high         → [P1]
#   priority-medium / priority: medium     → [P2]
#   priority-low / priority: low           → [P3]

set -euo pipefail

# Configuration
REPO="DegrassiAaron/meepleai-monorepo"
DRY_RUN=true
LOG_FILE="triage-normalize-$(date +%Y%m%d-%H%M%S).log"
REPORT_FILE="triage-normalize-report-$(date +%Y%m%d-%H%M%S).md"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Parse arguments
for arg in "$@"; do
    case $arg in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --execute)
            DRY_RUN=false
            shift
            ;;
        --help)
            echo "Usage: $0 [--dry-run|--execute]"
            echo "  --dry-run   : Preview changes without applying (default)"
            echo "  --execute   : Apply changes to GitHub issues"
            exit 0
            ;;
        *)
            echo "Unknown argument: $arg"
            exit 1
            ;;
    esac
done

# Logging function
log() {
    local level=$1
    shift
    local message="$@"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo -e "${timestamp} [${level}] ${message}" | tee -a "$LOG_FILE"
}

log "INFO" "${BLUE}=== TRIAGE PHASE 1: Issue Normalization ===${NC}"
log "INFO" "Repository: $REPO"
log "INFO" "Mode: $([ "$DRY_RUN" = true ] && echo 'DRY RUN (preview only)' || echo 'EXECUTE (will modify issues)')"
log "INFO" "Log file: $LOG_FILE"
log "INFO" "Report file: $REPORT_FILE"

# Check prerequisites
if ! command -v gh &> /dev/null; then
    log "ERROR" "${RED}GitHub CLI (gh) not found. Please install: https://cli.github.com/${NC}"
    exit 1
fi

if ! command -v jq &> /dev/null; then
    log "ERROR" "${RED}jq not found. Please install: https://stedolan.github.io/jq/${NC}"
    exit 1
fi

# Verify GitHub CLI authentication
if ! gh auth status &> /dev/null; then
    log "ERROR" "${RED}GitHub CLI not authenticated. Run: gh auth login${NC}"
    exit 1
fi

log "INFO" "${GREEN}✓ Prerequisites verified${NC}"

# Initialize report
cat > "$REPORT_FILE" <<'EOF'
# Issue Normalization Report

**Date**: $(date '+%Y-%m-%d %H:%M:%S')
**Repository**: DegrassiAaron/meepleai-monorepo
**Mode**: DRY_RUN_PLACEHOLDER

---

## Summary

| Metric | Count |
|--------|-------|
| Total issues processed | TOTAL_PLACEHOLDER |
| Issues renamed | RENAMED_PLACEHOLDER |
| Issues skipped (already normalized) | SKIPPED_PLACEHOLDER |
| Errors | ERROR_PLACEHOLDER |

---

## Priority Mapping Applied

| Original Label | New Title Prefix | Count |
|----------------|------------------|-------|
| priority-critical / priority: critical | [P0] | P0_COUNT |
| priority-high / priority: high | [P1] | P1_COUNT |
| priority-medium / priority: medium | [P2] | P2_COUNT |
| priority-low / priority: low | [P3] | P3_COUNT |

---

## Changes

EOF

# Counters
total_processed=0
total_renamed=0
total_skipped=0
total_errors=0
p0_count=0
p1_count=0
p2_count=0
p3_count=0

# Function to normalize issue title
normalize_issue() {
    local issue_number=$1
    local current_title=$2
    local priority_tag=$3

    # Skip if already has [P0], [P1], [P2], or [P3] prefix
    if [[ "$current_title" =~ ^\[P[0-3]\] ]]; then
        log "INFO" "  Issue #${issue_number}: Already normalized, skipping"
        echo "- **#${issue_number}**: ✅ Already normalized: \`${current_title}\`" >> "$REPORT_FILE"
        ((total_skipped++))
        return 0
    fi

    # Construct new title
    local new_title="${priority_tag} ${current_title}"

    log "INFO" "  Issue #${issue_number}:"
    log "INFO" "    Old: ${current_title}"
    log "INFO" "    New: ${new_title}"

    echo "" >> "$REPORT_FILE"
    echo "### Issue #${issue_number}" >> "$REPORT_FILE"
    echo "- **Old title**: \`${current_title}\`" >> "$REPORT_FILE"
    echo "- **New title**: \`${new_title}\`" >> "$REPORT_FILE"
    echo "- **Priority**: ${priority_tag}" >> "$REPORT_FILE"

    if [ "$DRY_RUN" = false ]; then
        # Execute rename
        if gh issue edit "$issue_number" --repo "$REPO" --title "$new_title" 2>> "$LOG_FILE"; then
            log "INFO" "    ${GREEN}✓ Renamed successfully${NC}"
            echo "- **Status**: ✅ Renamed successfully" >> "$REPORT_FILE"
            ((total_renamed++))
        else
            log "ERROR" "    ${RED}✗ Failed to rename${NC}"
            echo "- **Status**: ❌ Failed to rename (see log)" >> "$REPORT_FILE"
            ((total_errors++))
        fi
    else
        log "INFO" "    ${YELLOW}⚠ Would rename (dry run)${NC}"
        echo "- **Status**: ⚠️ Would rename (dry run)" >> "$REPORT_FILE"
        ((total_renamed++))
    fi

    ((total_processed++))
}

# Function to process issues with a specific label
process_label() {
    local label=$1
    local priority_tag=$2

    log "INFO" "${BLUE}Processing issues with label: ${label}${NC}"

    # Fetch issues with this label (excluding deferred and already tagged)
    local issues=$(gh issue list \
        --repo "$REPO" \
        --label "$label" \
        --state open \
        --limit 1000 \
        --json number,title,labels \
        --jq '.[] | select(.labels | map(select(.name == "deferred")) | length == 0) | "\(.number)|\(.title)"')

    if [ -z "$issues" ]; then
        log "INFO" "  No issues found with label: ${label}"
        return 0
    fi

    # Process each issue
    while IFS='|' read -r issue_number title; do
        normalize_issue "$issue_number" "$title" "$priority_tag"

        # Increment counter for this priority
        case $priority_tag in
            "[P0]") ((p0_count++)) ;;
            "[P1]") ((p1_count++)) ;;
            "[P2]") ((p2_count++)) ;;
            "[P3]") ((p3_count++)) ;;
        esac

        # Rate limiting: wait 0.5s between API calls
        sleep 0.5
    done <<< "$issues"
}

# Main processing
log "INFO" "${BLUE}Starting normalization process...${NC}"

# Process each priority level
process_label "priority-critical" "[P0]"
process_label "priority: critical" "[P0]"
process_label "critical" "[P0]"

process_label "priority-high" "[P1]"
process_label "priority: high" "[P1]"

process_label "priority-medium" "[P2]"
process_label "priority: medium" "[P2]"

process_label "priority-low" "[P3]"
process_label "priority: low" "[P3]"

# Update report with final counts
sed -i "s/DRY_RUN_PLACEHOLDER/$([ "$DRY_RUN" = true ] && echo 'DRY RUN (preview)' || echo 'EXECUTE (applied)')/" "$REPORT_FILE"
sed -i "s/TOTAL_PLACEHOLDER/${total_processed}/" "$REPORT_FILE"
sed -i "s/RENAMED_PLACEHOLDER/${total_renamed}/" "$REPORT_FILE"
sed -i "s/SKIPPED_PLACEHOLDER/${total_skipped}/" "$REPORT_FILE"
sed -i "s/ERROR_PLACEHOLDER/${total_errors}/" "$REPORT_FILE"
sed -i "s/P0_COUNT/${p0_count}/" "$REPORT_FILE"
sed -i "s/P1_COUNT/${p1_count}/" "$REPORT_FILE"
sed -i "s/P2_COUNT/${p2_count}/" "$REPORT_FILE"
sed -i "s/P3_COUNT/${p3_count}/" "$REPORT_FILE"

# Final summary
log "INFO" "${BLUE}=== Normalization Complete ===${NC}"
log "INFO" "Total issues processed: ${total_processed}"
log "INFO" "Issues renamed: ${total_renamed}"
log "INFO" "Issues skipped: ${total_skipped}"
log "INFO" "Errors: ${total_errors}"
log "INFO" ""
log "INFO" "Priority breakdown:"
log "INFO" "  [P0] Critical: ${p0_count}"
log "INFO" "  [P1] High: ${p1_count}"
log "INFO" "  [P2] Medium: ${p2_count}"
log "INFO" "  [P3] Low: ${p3_count}"
log "INFO" ""
log "INFO" "Report saved to: ${GREEN}${REPORT_FILE}${NC}"
log "INFO" "Log saved to: ${GREEN}${LOG_FILE}${NC}"

if [ "$DRY_RUN" = true ]; then
    log "INFO" "${YELLOW}This was a DRY RUN. No changes were made.${NC}"
    log "INFO" "${YELLOW}To apply changes, run: $0 --execute${NC}"
else
    log "INFO" "${GREEN}Changes have been applied to GitHub issues.${NC}"
fi

# Exit with error code if there were errors
if [ $total_errors -gt 0 ]; then
    log "ERROR" "${RED}Normalization completed with ${total_errors} errors. Check log for details.${NC}"
    exit 1
fi

log "INFO" "${GREEN}✓ Normalization successful!${NC}"
exit 0
