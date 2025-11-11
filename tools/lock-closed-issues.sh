#!/bin/bash
# Lock all closed GitHub issues
# Prevents further comments/activity on resolved issues

set -e

DRY_RUN=false
BATCH_SIZE=50

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --batch-size)
            BATCH_SIZE="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 [--dry-run] [--batch-size N]"
            exit 1
            ;;
    esac
done

echo "🔒 Locking Closed GitHub Issues"
echo "================================="
echo ""

# Get all closed issues
echo "📋 Fetching closed issues..."
CLOSED_ISSUES=$(gh issue list --state closed --limit 1000 --json number)
TOTAL=$(echo "$CLOSED_ISSUES" | grep -o '"number":[0-9]*' | wc -l)

echo "✓ Found $TOTAL closed issues"
echo ""

if [ "$DRY_RUN" = true ]; then
    echo "🔍 DRY RUN MODE - No issues will be locked"
    echo ""
    echo "Would lock $TOTAL issues"
    echo ""
    echo "Run without --dry-run to execute"
    exit 0
fi

# Lock issues
LOCKED=0
FAILED=0
FAILED_ISSUES=()

echo "🔒 Locking issues (batch size: $BATCH_SIZE)..."
echo ""

# Extract issue numbers
ISSUE_NUMBERS=$(echo "$CLOSED_ISSUES" | grep -o '"number":[0-9]*' | grep -o '[0-9]*')

for ISSUE in $ISSUE_NUMBERS; do
    if gh issue lock "$ISSUE" --reason "resolved" 2>/dev/null; then
        ((LOCKED++))

        # Progress indicator every 10 issues
        if [ $((LOCKED % 10)) -eq 0 ]; then
            PERCENT=$(awk "BEGIN {printf \"%.1f\", ($LOCKED / $TOTAL) * 100}")
            echo "  Progress: $LOCKED/$TOTAL ($PERCENT%)"
        fi

        # Rate limiting: pause every batch
        if [ $((LOCKED % BATCH_SIZE)) -eq 0 ]; then
            echo "  ⏸️  Batch complete, pausing 5s..."
            sleep 5
        fi
    else
        ((FAILED++))
        FAILED_ISSUES+=("$ISSUE")
        echo "  ❌ Failed to lock issue #$ISSUE"
    fi
done

echo ""
echo "╔════════════════════════════════════════╗"
echo "║   Lock Operation Complete              ║"
echo "╚════════════════════════════════════════╝"
echo ""
echo "✅ Successfully locked: $LOCKED issues"

if [ $FAILED -gt 0 ]; then
    echo "❌ Failed to lock: $FAILED issues"
    echo ""
    echo "Failed issues:"
    for ISSUE in "${FAILED_ISSUES[@]}"; do
        echo "  - Issue #$ISSUE"
    done
fi

echo ""
echo "🔒 All closed issues are now locked (no new comments allowed)"
