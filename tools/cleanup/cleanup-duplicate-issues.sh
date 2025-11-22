#!/usr/bin/env bash
# cleanup-duplicate-issues.sh
# Close duplicate issues with proper reasoning and cross-references

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "🔍 GitHub Issues Duplicate Cleanup"
echo "===================================="
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

# Close duplicate issue #709
echo "📋 Processing Duplicate: #709"
echo "   Title: Create operational runbooks documentation"
echo "   Reason: Duplicate of #706 (created 2 minutes later)"
echo ""

read -p "⚠️  Close #709 as duplicate of #706? [y/N] " -n 1 -r
echo ""

if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "🔄 Closing #709 as duplicate..."

    gh issue close 709 \
        --reason "not_planned" \
        --comment "Closing as duplicate of #706.

**Analysis**:
- #706 created: 2025-11-04T14:30:45Z
- #709 created: 2025-11-04T14:32:55Z (2 minutes 10 seconds later)
- Both issues have identical scope: 8 operational runbooks
- #706 is the canonical issue

**Action**: All work will be tracked in #706. If #709 has unique details not in #706, they will be merged into #706.

See strategic triage analysis: \`tools/issue-triage-analysis.md\`"

    if [ $? -eq 0 ]; then
        echo "✅ Issue #709 closed as duplicate"
    else
        echo "❌ Failed to close #709"
        exit 1
    fi
else
    echo "⏭️  Skipped closing #709"
fi

echo ""
echo "✅ Duplicate cleanup complete!"
echo ""
echo "📊 Summary:"
echo "   - Duplicates closed: 1 (#709)"
echo "   - Canonical issue: #706"
echo ""
echo "Next steps:"
echo "   1. Run: ./tools/assign-infrastructure-milestones.sh"
echo "   2. Verify: gh issue list --search 'is:open no:milestone'"
