#!/bin/bash
# Script to create security issues on GitHub
# Prerequisites: GitHub CLI (gh) installed and authenticated

set -e

REPO="meepleAi-app/meepleai-monorepo"
ISSUE_DIR=".github"

echo "🔒 Creating Security Issues on GitHub"
echo "======================================"
echo ""

# Check if gh is installed
if ! command -v gh &> /dev/null; then
    echo "❌ GitHub CLI (gh) is not installed."
    echo ""
    echo "Please install it:"
    echo "  macOS:   brew install gh"
    echo "  Linux:   sudo apt install gh  (or use snap: snap install gh)"
    echo "  Windows: winget install GitHub.cli"
    echo ""
    echo "Then authenticate: gh auth login"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo "❌ GitHub CLI is not authenticated."
    echo ""
    echo "Please authenticate first:"
    echo "  gh auth login"
    exit 1
fi

echo "✅ GitHub CLI is installed and authenticated"
echo ""

# Issue 1: XSS (P1 - High)
echo "Creating Issue 1/3: [SECURITY] XSS Vulnerability in Rich Text Editor (P1)..."
ISSUE_1=$(gh issue create \
    --repo "$REPO" \
    --title "[SECURITY] XSS Vulnerability in Rich Text Editor" \
    --body-file "$ISSUE_DIR/ISSUE_SECURITY_01_XSS.md" \
    --label "security,xss,p1-high,frontend,editor" \
    --assignee "@me" 2>/dev/null || echo "")

if [ -n "$ISSUE_1" ]; then
    echo "✅ Created: $ISSUE_1"
else
    echo "⚠️  Failed to create issue 1. Creating without assignee..."
    ISSUE_1=$(gh issue create \
        --repo "$REPO" \
        --title "[SECURITY] XSS Vulnerability in Rich Text Editor" \
        --body-file "$ISSUE_DIR/ISSUE_SECURITY_01_XSS.md" \
        --label "security,xss,p1-high,frontend,editor")
    echo "✅ Created: $ISSUE_1"
fi
echo ""

# Issue 2: Hardcoded Credentials (P2 - Medium)
echo "Creating Issue 2/3: [SECURITY] Hardcoded Database Credentials (P2)..."
ISSUE_2=$(gh issue create \
    --repo "$REPO" \
    --title "[SECURITY] Hardcoded Database Credentials in Fallback Configuration" \
    --body-file "$ISSUE_DIR/ISSUE_SECURITY_02_HARDCODED_CREDENTIALS.md" \
    --label "security,credentials,p2-medium,backend,configuration" \
    --assignee "@me" 2>/dev/null || echo "")

if [ -n "$ISSUE_2" ]; then
    echo "✅ Created: $ISSUE_2"
else
    echo "⚠️  Failed to create issue 2. Creating without assignee..."
    ISSUE_2=$(gh issue create \
        --repo "$REPO" \
        --title "[SECURITY] Hardcoded Database Credentials in Fallback Configuration" \
        --body-file "$ISSUE_DIR/ISSUE_SECURITY_02_HARDCODED_CREDENTIALS.md" \
        --label "security,credentials,p2-medium,backend,configuration")
    echo "✅ Created: $ISSUE_2"
fi
echo ""

# Issue 3: Security Improvements (P3 - Low)
echo "Creating Issue 3/3: [SECURITY] Security Improvements (P3)..."
ISSUE_3=$(gh issue create \
    --repo "$REPO" \
    --title "[SECURITY] Security Improvements: CORS Configuration & JSON Deserialization" \
    --body-file "$ISSUE_DIR/ISSUE_SECURITY_03_IMPROVEMENTS.md" \
    --label "security,hardening,p3-low,backend,cors,deserialization,logging" \
    --assignee "@me" 2>/dev/null || echo "")

if [ -n "$ISSUE_3" ]; then
    echo "✅ Created: $ISSUE_3"
else
    echo "⚠️  Failed to create issue 3. Creating without assignee..."
    ISSUE_3=$(gh issue create \
        --repo "$REPO" \
        --title "[SECURITY] Security Improvements: CORS Configuration & JSON Deserialization" \
        --body-file "$ISSUE_DIR/ISSUE_SECURITY_03_IMPROVEMENTS.md" \
        --label "security,hardening,p3-low,backend,cors,deserialization,logging")
    echo "✅ Created: $ISSUE_3"
fi
echo ""

# Summary
echo "======================================"
echo "🎉 Successfully created 3 security issues!"
echo ""
echo "📋 Issues Created:"
echo "  1. $ISSUE_1 (P1 - High)"
echo "  2. $ISSUE_2 (P2 - Medium)"
echo "  3. $ISSUE_3 (P3 - Low)"
echo ""
echo "📊 View all security issues:"
echo "  gh issue list --repo $REPO --label security"
echo ""
echo "🔗 Or visit:"
echo "  https://github.com/$REPO/issues?q=is%3Aissue+is%3Aopen+label%3Asecurity"
echo ""
echo "🚀 Next steps:"
echo "  1. Review and prioritize issues"
echo "  2. Assign to team members"
echo "  3. Start with P1 (XSS vulnerability, 6-7h estimate)"
echo "  4. Add to sprint planning"
echo ""
