#!/bin/bash
#
# Create Security Issues on GitHub
#
# This script creates 3 security issues based on the security audit:
# 1. P1 (HIGH): XSS Vulnerability in Rich Text Editor
# 2. P2 (MEDIUM): Hardcoded Database Credentials
# 3. P3 (LOW): Security Improvements (CORS, JSON, Logging)
#
# Prerequisites:
#   - GitHub CLI (gh) installed
#   - Authenticated with: gh auth login
#
# Usage:
#   ./tools/create-security-issues.sh

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Repository
REPO="DegrassiAaron/meepleai-monorepo"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  MeepleAI Security Issues Creator${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check if gh is installed
if ! command -v gh &> /dev/null; then
    echo -e "${RED}Error: GitHub CLI (gh) is not installed.${NC}"
    echo ""
    echo "Install instructions:"
    echo "  macOS:   brew install gh"
    echo "  Linux:   sudo apt install gh"
    echo "  Windows: winget install GitHub.cli"
    echo ""
    echo "Or visit: https://cli.github.com/"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo -e "${RED}Error: Not authenticated with GitHub CLI.${NC}"
    echo ""
    echo "Run: gh auth login"
    exit 1
fi

echo -e "${GREEN}✓ GitHub CLI is installed and authenticated${NC}"
echo ""

# Ask for confirmation
echo -e "${YELLOW}This will create 3 security issues on:${NC}"
echo -e "${YELLOW}  Repository: $REPO${NC}"
echo ""
echo "Issues to create:"
echo "  1. [P1-HIGH] XSS Vulnerability in Rich Text Editor (6-7h)"
echo "  2. [P2-MEDIUM] Hardcoded Database Credentials (3h)"
echo "  3. [P3-LOW] Security Improvements (7-9h)"
echo ""
read -p "Continue? (y/n) " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Aborted.${NC}"
    exit 0
fi

echo ""
echo -e "${BLUE}Creating issues...${NC}"
echo ""

# Issue 1: XSS Vulnerability (P1)
echo -e "${BLUE}[1/3] Creating P1 issue: XSS Vulnerability...${NC}"

ISSUE_1_URL=$(gh issue create \
    --repo "$REPO" \
    --title "[SECURITY] XSS Vulnerability in Rich Text Editor" \
    --label "security,xss,P1,bug,frontend" \
    --body-file .github/ISSUE_SECURITY_01_XSS.md)

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Created: $ISSUE_1_URL${NC}"
else
    echo -e "${RED}✗ Failed to create P1 issue${NC}"
fi

echo ""

# Issue 2: Hardcoded Credentials (P2)
echo -e "${BLUE}[2/3] Creating P2 issue: Hardcoded Credentials...${NC}"

ISSUE_2_URL=$(gh issue create \
    --repo "$REPO" \
    --title "[SECURITY] Hardcoded Database Credentials" \
    --label "security,credentials,P2,backend,configuration" \
    --body-file .github/ISSUE_SECURITY_02_HARDCODED_CREDENTIALS.md)

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Created: $ISSUE_2_URL${NC}"
else
    echo -e "${RED}✗ Failed to create P2 issue${NC}"
fi

echo ""

# Issue 3: Security Improvements (P3)
echo -e "${BLUE}[3/3] Creating P3 issue: Security Improvements...${NC}"

ISSUE_3_URL=$(gh issue create \
    --repo "$REPO" \
    --title "[SECURITY] Security Improvements: CORS & JSON Deserialization" \
    --label "security,hardening,P3,backend" \
    --body-file .github/ISSUE_SECURITY_03_IMPROVEMENTS.md)

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Created: $ISSUE_3_URL${NC}"
else
    echo -e "${RED}✗ Failed to create P3 issue${NC}"
fi

echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${GREEN}✓ All security issues created!${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""
echo "View all security issues:"
echo "  gh issue list --repo $REPO --label security"
echo ""
echo "Or visit:"
echo "  https://github.com/$REPO/issues?q=is%3Aissue+is%3Aopen+label%3Asecurity"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. Review the created issues"
echo "  2. Assign to team members"
echo "  3. Add to current milestone/sprint"
echo "  4. Start remediation (P1 first - 6-7 hours)"
echo ""
echo "For more info, see:"
echo "  docs/06-security/security-issue-audit.md"
echo ""
