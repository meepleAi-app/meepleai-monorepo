#!/usr/bin/env bash
# phase-2-issue-labels.sh
# Ensure consistent labeling for Phase 2 deferred issues

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "🏷️  Phase 2 Issue Label Consistency"
echo "====================================="
echo ""

# Check if gh CLI is available
if ! command -v gh &> /dev/null; then
    echo "❌ Error: GitHub CLI (gh) not found"
    exit 1
fi

# Verify authentication
if ! gh auth status &> /dev/null; then
    echo "❌ Error: Not authenticated with GitHub CLI"
    exit 1
fi

echo "✅ GitHub CLI authenticated"
echo ""

# Issues that should have 'deferred' and 'priority-low' labels
# (Admin Console issues already have these, this is for infrastructure/epic issues)
declare -a PHASE_2_ISSUES=(
    702 703 818 936  # Infrastructure Phase 2
    926 931 932 933 934 935 844  # Frontend epics
)

echo "📋 Phase 2 Issues to Label:"
echo ""
echo "Infrastructure:"
echo "  - #702: Docker Compose profiles"
echo "  - #703: Traefik reverse proxy"
echo "  - #818: Quarterly security review"
echo "  - #936: Infisical POC"
echo ""
echo "Frontend Epics (already deferred):"
echo "  - #926: Foundation & Quick Wins"
echo "  - #931: React 19 Optimization"
echo "  - #932: Advanced Features"
echo "  - #933: App Router Migration"
echo "  - #934: Design Polish"
echo "  - #935: Performance & Accessibility"
echo "  - #844: UI/UX Testing Roadmap"
echo ""

read -p "⚠️  Ensure 'deferred' and 'priority-low' labels on Phase 2 issues? [y/N] " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "⏭️  Aborted"
    exit 0
fi

# Function to ensure labels
ensure_labels() {
    local issue_num=$1

    echo "🔄 Checking labels on #$issue_num..."

    # Get current labels
    CURRENT_LABELS=$(gh issue view "$issue_num" --json labels --jq '.labels[].name' 2>/dev/null || echo "")

    # Check if labels exist
    HAS_DEFERRED=false
    HAS_PRIORITY_LOW=false

    while IFS= read -r label; do
        if [ "$label" = "deferred" ]; then
            HAS_DEFERRED=true
        elif [ "$label" = "priority-low" ]; then
            HAS_PRIORITY_LOW=true
        fi
    done <<< "$CURRENT_LABELS"

    # Add missing labels
    LABELS_TO_ADD=()

    if [ "$HAS_DEFERRED" = false ]; then
        LABELS_TO_ADD+=("deferred")
    fi

    if [ "$HAS_PRIORITY_LOW" = false ]; then
        LABELS_TO_ADD+=("priority-low")
    fi

    if [ ${#LABELS_TO_ADD[@]} -gt 0 ]; then
        echo "   📝 Adding labels: ${LABELS_TO_ADD[*]}"

        for label in "${LABELS_TO_ADD[@]}"; do
            gh issue edit "$issue_num" --add-label "$label" &> /dev/null
        done

        echo "   ✅ Labels added to #$issue_num"
        return 0
    else
        echo "   ✅ #$issue_num already has correct labels"
        return 0
    fi
}

# Process issues
SUCCESS_COUNT=0
FAILED_COUNT=0

for issue in "${PHASE_2_ISSUES[@]}"; do
    if ensure_labels "$issue"; then
        ((SUCCESS_COUNT++))
    else
        ((FAILED_COUNT++))
    fi

    sleep 0.5  # Rate limit protection
done

echo ""
echo "✅ Label consistency check complete!"
echo ""
echo "📊 Summary:"
echo "   - Issues processed: $SUCCESS_COUNT"
echo "   - Failed: $FAILED_COUNT"
echo ""
echo "Verification:"
echo "   gh issue list --search 'label:deferred label:priority-low is:open'"
echo ""
echo "✅ All cleanup scripts complete!"
echo ""
echo "📊 Final Verification Commands:"
echo "   1. Issues without milestone:"
echo "      gh issue list --search 'is:open no:milestone' --limit 50"
echo ""
echo "   2. Deferred issues:"
echo "      gh issue list --search 'label:deferred is:open' --limit 100"
echo ""
echo "   3. Phase 2 infrastructure:"
echo "      gh issue list --search 'milestone:\"Phase 2\" is:open'"
echo ""
echo "   4. Month 3-6 milestones:"
echo "      gh issue list --search 'milestone:\"Month 3\" OR milestone:\"Month 4\" OR milestone:\"Month 6\" is:open'"
