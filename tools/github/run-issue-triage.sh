#!/usr/bin/env bash
# run-issue-triage.sh
# Master script to execute all issue triage and cleanup operations

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "╔══════════════════════════════════════════════════════════════╗"
echo "║  GitHub Issues Strategic Triage & Cleanup                    ║"
echo "║  MeepleAI Monorepo Backend                                   ║"
echo "╚══════════════════════════════════════════════════════════════╝"
echo ""
echo "Date: $(date '+%Y-%m-%d %H:%M:%S')"
echo ""

# Check if gh CLI is available
if ! command -v gh &> /dev/null; then
    echo "❌ Error: GitHub CLI (gh) not found"
    echo "   Install: https://cli.github.com"
    exit 1
fi

# Verify authentication
if ! gh auth status &> /dev/null; then
    echo "❌ Error: Not authenticated with GitHub CLI"
    echo "   Run: gh auth login"
    exit 1
fi

echo "✅ GitHub CLI authenticated"
echo ""

# Show current state
echo "📊 Current Repository State"
echo "=============================="
echo ""

TOTAL_OPEN=$(gh issue list --state open --limit 1000 --json number | jq '. | length')
NO_MILESTONE=$(gh issue list --search "is:open no:milestone" --limit 100 --json number | jq '. | length')
DEFERRED=$(gh issue list --search "label:deferred is:open" --limit 100 --json number | jq '. | length')

echo "Total open issues: $TOTAL_OPEN"
echo "Issues without milestone: $NO_MILESTONE"
echo "Deferred issues: $DEFERRED"
echo ""

# Show what will be done
echo "📋 Operations to Execute"
echo "========================="
echo ""
echo "1. Close Duplicate Issues"
echo "   - Close #709 as duplicate of #706"
echo "   - Closes: 1 issue"
echo ""
echo "2. Assign Infrastructure Milestones"
echo "   - Month 3: 3 issues (12h total)"
echo "   - Month 4: 3 issues (18h total)"
echo "   - Month 6: 1 issue (40h total)"
echo "   - Phase 2: 4 issues (22h total)"
echo "   - Assigns: 11 issues"
echo ""
echo "3. Ensure Phase 2 Label Consistency"
echo "   - Add 'deferred' and 'priority-low' labels"
echo "   - Processes: 11 issues"
echo ""
echo "Expected Results:"
echo "   - Issues without milestone: 0"
echo "   - Duplicate issues: 0"
echo "   - Milestone assignments: 11 new"
echo "   - Issues closed: 1"
echo ""

read -p "⚠️  Proceed with all operations? [y/N] " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "⏭️  Aborted - no changes made"
    exit 0
fi

echo ""
echo "════════════════════════════════════════════════════════════════"
echo ""

# Step 1: Close duplicates
echo "STEP 1: Close Duplicate Issues"
echo "--------------------------------"
echo ""

if [ -f "$REPO_ROOT/tools/cleanup-duplicate-issues.sh" ]; then
    bash "$REPO_ROOT/tools/cleanup-duplicate-issues.sh" <<< "y"
    STEP1_STATUS=$?
else
    echo "❌ Script not found: cleanup-duplicate-issues.sh"
    STEP1_STATUS=1
fi

echo ""
echo "════════════════════════════════════════════════════════════════"
echo ""

# Step 2: Assign milestones (check if milestones exist first)
echo "STEP 2: Create Milestones (if needed)"
echo "--------------------------------------"
echo ""

# Check if milestones exist, create if needed
MILESTONES=("Month 3" "Month 4" "Month 6" "Phase 2")
MILESTONES_CREATED=0

for milestone in "${MILESTONES[@]}"; do
    if ! gh api repos/:owner/:repo/milestones --jq ".[].title" 2>/dev/null | grep -q "^${milestone}$"; then
        echo "📝 Creating milestone: $milestone"

        # Set due dates based on milestone
        DUE_DATE=""
        case "$milestone" in
            "Month 3")
                DUE_DATE="2025-12-31T00:00:00Z"
                ;;
            "Month 4")
                DUE_DATE="2026-01-31T00:00:00Z"
                ;;
            "Month 6")
                DUE_DATE="2026-03-31T00:00:00Z"
                ;;
            "Phase 2")
                DUE_DATE="2026-06-30T00:00:00Z"
                ;;
        esac

        gh api repos/:owner/:repo/milestones \
            -f title="$milestone" \
            -f state="open" \
            -f due_on="$DUE_DATE" \
            &> /dev/null

        if [ $? -eq 0 ]; then
            echo "   ✅ Created: $milestone"
            ((MILESTONES_CREATED++))
        else
            echo "   ⚠️  Failed to create: $milestone"
        fi
    else
        echo "✅ Milestone exists: $milestone"
    fi
done

echo ""
echo "Milestones created: $MILESTONES_CREATED"
echo ""

echo "════════════════════════════════════════════════════════════════"
echo ""

echo "STEP 3: Assign Infrastructure Milestones"
echo "-----------------------------------------"
echo ""

if [ -f "$REPO_ROOT/tools/assign-infrastructure-milestones.sh" ]; then
    bash "$REPO_ROOT/tools/assign-infrastructure-milestones.sh" <<< "y"
    STEP2_STATUS=$?
else
    echo "❌ Script not found: assign-infrastructure-milestones.sh"
    STEP2_STATUS=1
fi

echo ""
echo "════════════════════════════════════════════════════════════════"
echo ""

# Step 3: Ensure labels
echo "STEP 4: Ensure Phase 2 Label Consistency"
echo "-----------------------------------------"
echo ""

if [ -f "$REPO_ROOT/tools/phase-2-issue-labels.sh" ]; then
    bash "$REPO_ROOT/tools/phase-2-issue-labels.sh" <<< "y"
    STEP3_STATUS=$?
else
    echo "❌ Script not found: phase-2-issue-labels.sh"
    STEP3_STATUS=1
fi

echo ""
echo "════════════════════════════════════════════════════════════════"
echo ""

# Final verification
echo "✅ ALL OPERATIONS COMPLETE"
echo "============================"
echo ""
echo "📊 Final Verification"
echo "---------------------"
echo ""

FINAL_TOTAL_OPEN=$(gh issue list --state open --limit 1000 --json number | jq '. | length')
FINAL_NO_MILESTONE=$(gh issue list --search "is:open no:milestone" --limit 100 --json number | jq '. | length')
FINAL_DEFERRED=$(gh issue list --search "label:deferred is:open" --limit 100 --json number | jq '. | length')

echo "Before → After"
echo "---------------"
echo "Total open issues: $TOTAL_OPEN → $FINAL_TOTAL_OPEN"
echo "Issues without milestone: $NO_MILESTONE → $FINAL_NO_MILESTONE"
echo "Deferred issues: $DEFERRED → $FINAL_DEFERRED"
echo ""

if [ $FINAL_NO_MILESTONE -eq 0 ]; then
    echo "✅ SUCCESS: All issues have milestones assigned!"
else
    echo "⚠️  WARNING: $FINAL_NO_MILESTONE issues still without milestone"
    echo ""
    echo "Remaining issues:"
    gh issue list --search "is:open no:milestone" --json number,title
fi

echo ""
echo "📖 Documentation"
echo "----------------"
echo "Full analysis: tools/issue-triage-analysis.md"
echo ""
echo "📋 Quick Verification Commands"
echo "-------------------------------"
echo "1. Check all open issues:"
echo "   gh issue list --state open --limit 50"
echo ""
echo "2. Check deferred issues:"
echo "   gh issue list --search 'label:deferred is:open' --limit 50"
echo ""
echo "3. Check Phase 2 work:"
echo "   gh issue list --search 'milestone:\"Phase 2\" is:open'"
echo ""
echo "4. Check Month 3-6 work:"
echo "   gh issue list --search 'milestone:\"Month 3\" OR milestone:\"Month 4\" OR milestone:\"Month 6\" is:open'"
echo ""
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "✅ Issue triage complete! Repository is now organized."
