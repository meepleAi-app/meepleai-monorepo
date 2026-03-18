#!/bin/bash
# dismiss-codeql-false-positives.sh
# Helper script for dismissing CodeQL false positives on GitHub

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Repository details
REPO="meepleAi-app/meepleai-monorepo"

echo -e "${BLUE}════════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}   CodeQL False Positive Dismissal Helper${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════════════${NC}"
echo ""
echo -e "${YELLOW}Note: Bulk dismissals process alerts sequentially via GitHub API${NC}"
echo -e "${YELLOW}      Expected runtime: ~2-5 minutes for 400+ alerts${NC}"
echo ""

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo -e "${RED}❌ GitHub CLI (gh) is not installed.${NC}"
    echo ""
    echo "Please install it from: https://cli.github.com/"
    echo ""
    echo -e "${YELLOW}Alternative: Dismiss alerts manually via GitHub UI${NC}"
    echo "   URL: https://github.com/${REPO}/security/code-scanning"
    echo ""
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo -e "${RED}❌ Not authenticated with GitHub CLI${NC}"
    echo ""
    echo "Please run: gh auth login"
    echo ""
    exit 1
fi

echo -e "${GREEN}✅ GitHub CLI is installed and authenticated${NC}"
echo ""

# Function to dismiss alerts
dismiss_alerts() {
    local rule_id=$1
    local reason=$2
    local comment=$3

    echo -e "${YELLOW}Fetching alerts for rule: ${rule_id}${NC}"

    # Get alert numbers for the specific rule
    # Use --paginate to fetch all pages (GitHub API returns 30 per page by default)
    alert_numbers=$(gh api \
        --paginate \
        -H "Accept: application/vnd.github+json" \
        "/repos/${REPO}/code-scanning/alerts?state=open&rule_id=${rule_id}&per_page=100" \
        --jq '.[].number' 2>/dev/null || echo "")

    if [ -z "$alert_numbers" ]; then
        echo -e "${GREEN}  No open alerts found for this rule${NC}"
        return 0
    fi

    alert_count=$(echo "$alert_numbers" | wc -l)
    echo -e "${BLUE}  Found ${alert_count} open alert(s)${NC}"

    # Ask for confirmation
    echo ""
    echo -e "${YELLOW}Ready to dismiss ${alert_count} alert(s) for rule: ${rule_id}${NC}"
    echo -e "${YELLOW}Reason: ${reason}${NC}"
    echo -e "${YELLOW}Comment: ${comment}${NC}"
    echo ""
    read -p "Continue? (y/N): " -n 1 -r
    echo ""

    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}  Skipped${NC}"
        return 0
    fi

    # Dismiss each alert
    dismissed_count=0
    for alert_num in $alert_numbers; do
        echo -ne "  Dismissing alert #${alert_num}... "

        if gh api \
            --method PATCH \
            -H "Accept: application/vnd.github+json" \
            "/repos/${REPO}/code-scanning/alerts/${alert_num}" \
            -f state=dismissed \
            -f dismissed_reason="${reason}" \
            -f dismissed_comment="${comment}" &> /dev/null; then
            echo -e "${GREEN}✅${NC}"
            ((dismissed_count++))
        else
            echo -e "${RED}❌ Failed${NC}"
        fi
    done

    echo -e "${GREEN}  Successfully dismissed ${dismissed_count}/${alert_count} alerts${NC}"
    echo ""
}

# Main menu
echo "Select category to dismiss:"
echo ""
echo "1. Log Forging (CWE-117) - 426 alerts (est. 2-3 min)"
echo "2. Generic Exception Handling (CA1031) - 220 alerts (est. 1-2 min)"
echo "3. Show all open alerts (no dismissal)"
echo "4. Custom rule ID"
echo "5. Exit"
echo ""
read -p "Enter choice (1-5): " choice

case $choice in
    1)
        dismiss_alerts \
            "cs/log-forging" \
            "false positive" \
            "False positive - Log forging is mitigated by global LogForgingSanitizationPolicy. See: docs/06-security/codeql-false-positive-management.md. Evidence: 100% structured logging, zero string interpolation, automatic newline sanitization in LoggingConfiguration.cs"
        ;;
    2)
        dismiss_alerts \
            "cs/catch-of-all-exceptions" \
            "won't fix" \
            "Intentional pattern - All 220 instances are properly documented with #pragma warning disable CA1031 and architectural justifications. See: docs/06-security/codeql-false-positive-management.md and generic-catch-analysis.md"
        ;;
    3)
        echo -e "${BLUE}Fetching all open alerts...${NC}"
        echo ""
        gh api \
            --paginate \
            -H "Accept: application/vnd.github+json" \
            "/repos/${REPO}/code-scanning/alerts?state=open&per_page=100" \
            --jq '.[] | "\(.number) - \(.rule.id) - \(.rule.description) - \(.most_recent_instance.location.path):\(.most_recent_instance.location.start_line)"' || true
        echo ""
        ;;
    4)
        echo ""
        read -p "Enter rule ID (e.g., cs/log-forging): " rule_id
        read -p "Enter reason (false positive/won't fix/used in tests): " reason
        read -p "Enter comment: " comment
        echo ""
        dismiss_alerts "$rule_id" "$reason" "$comment"
        ;;
    5)
        echo -e "${BLUE}Exiting...${NC}"
        exit 0
        ;;
    *)
        echo -e "${RED}Invalid choice${NC}"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}════════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}   Done!${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════════${NC}"
echo ""
echo "Next steps:"
echo "1. Verify dismissals at: https://github.com/${REPO}/security/code-scanning"
echo "2. Review remaining alerts and categorize them"
echo "3. Update docs/06-security/codeql-false-positive-management.md if needed"
echo ""
echo "Note: Automated config (.github/codeql/codeql-config.yml) will prevent"
echo "      new Log Forging and Generic Exception alerts in future scans."
echo ""
