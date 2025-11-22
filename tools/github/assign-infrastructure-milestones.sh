#!/usr/bin/env bash
# assign-infrastructure-milestones.sh
# Assign milestones to infrastructure and security issues based on strategic triage

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "🗓️  GitHub Issues Milestone Assignment"
echo "========================================"
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

# Define milestone assignments based on strategic triage
# Format: "issue_number|milestone_name|reason"
declare -a ASSIGNMENTS=(
    # Month 3 (After BGAI Sprint 3) - 12h total
    "706|Month 3|Operational runbooks critical for on-call readiness (6h)"
    "707|Month 3|Docker override example for DX improvement (2h)"
    "575|Month 3|2FA admin override after 2FA stable (4h)"

    # Month 4 (Pre-Load Testing) - 18h total
    "701|Month 4|Docker resource limits for performance baseline (4h)"
    "704|Month 4|Backup automation for data protection (8h)"
    "705|Month 4|Infrastructure monitoring for observability (6h)"

    # Month 6 (Pre-Production) - 40h total
    "576|Month 6|Penetration testing for security validation (40h)"

    # Phase 2 (Post-Launch) - 22h total
    "702|Phase 2|Docker Compose profiles for DX (4h)"
    "703|Phase 2|Traefik reverse proxy for production (8h)"
    "818|Phase 2|Quarterly security review process (2h)"
    "936|Phase 2|Infisical POC for secret rotation (8h)"
)

echo "📋 Milestone Assignment Plan:"
echo ""
echo "Month 3 (After Sprint 3):"
echo "  - #706: Operational runbooks (6h)"
echo "  - #707: Docker override example (2h)"
echo "  - #575: 2FA admin override (4h)"
echo "  Total: 12h"
echo ""
echo "Month 4 (Pre-Load Testing):"
echo "  - #701: Docker resource limits (4h)"
echo "  - #704: Backup automation (8h)"
echo "  - #705: Infrastructure monitoring (6h)"
echo "  Total: 18h"
echo ""
echo "Month 6 (Pre-Production):"
echo "  - #576: Penetration testing (40h)"
echo "  Total: 40h"
echo ""
echo "Phase 2 (Post-Launch):"
echo "  - #702: Docker Compose profiles (4h)"
echo "  - #703: Traefik reverse proxy (8h)"
echo "  - #818: Security review process (2h)"
echo "  - #936: Infisical POC (8h)"
echo "  Total: 22h"
echo ""

read -p "⚠️  Proceed with milestone assignments? [y/N] " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "⏭️  Aborted"
    exit 0
fi

# Function to assign milestone
assign_milestone() {
    local issue_num=$1
    local milestone=$2
    local reason=$3

    echo "🔄 Assigning #$issue_num to '$milestone'..."

    # Note: GitHub CLI doesn't support milestone assignment directly
    # We need to use the API
    gh api \
        --method PATCH \
        "repos/:owner/:repo/issues/$issue_num" \
        -f milestone="$milestone" \
        &> /dev/null

    if [ $? -eq 0 ]; then
        echo "   ✅ Assigned #$issue_num to '$milestone'"

        # Add comment explaining the assignment
        gh issue comment "$issue_num" \
            --body "Assigned to **$milestone** milestone based on strategic triage.

**Rationale**: $reason

See full analysis: \`tools/issue-triage-analysis.md\`" \
            &> /dev/null
    else
        echo "   ⚠️  Failed to assign #$issue_num (milestone may not exist)"
        return 1
    fi
}

# Process assignments
SUCCESS_COUNT=0
FAILED_COUNT=0

for assignment in "${ASSIGNMENTS[@]}"; do
    IFS='|' read -r issue milestone reason <<< "$assignment"

    if assign_milestone "$issue" "$milestone" "$reason"; then
        ((SUCCESS_COUNT++))
    else
        ((FAILED_COUNT++))
    fi

    sleep 1  # Rate limit protection
done

echo ""
echo "✅ Milestone assignment complete!"
echo ""
echo "📊 Summary:"
echo "   - Successfully assigned: $SUCCESS_COUNT"
echo "   - Failed: $FAILED_COUNT"
echo ""

if [ $FAILED_COUNT -gt 0 ]; then
    echo "⚠️  Some assignments failed. Possible reasons:"
    echo "   1. Milestones don't exist yet (create them first)"
    echo "   2. Issue numbers incorrect"
    echo "   3. API rate limiting"
    echo ""
    echo "To create milestones, run:"
    echo "   gh api repos/:owner/:repo/milestones -f title='Month 3' -f state='open'"
    echo "   gh api repos/:owner/:repo/milestones -f title='Month 4' -f state='open'"
    echo "   gh api repos/:owner/:repo/milestones -f title='Month 6' -f state='open'"
    echo "   gh api repos/:owner/:repo/milestones -f title='Phase 2' -f state='open'"
fi

echo ""
echo "Next steps:"
echo "   1. Verify: gh issue list --search 'is:open no:milestone'"
echo "   2. Run: ./tools/phase-2-issue-labels.sh"
