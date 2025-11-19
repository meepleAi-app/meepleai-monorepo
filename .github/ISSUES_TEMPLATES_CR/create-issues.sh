#!/bin/bash

# Script to create GitHub issues from templates
# Generated from Code Review - Backend-Frontend Interactions (2025-01-19)

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

echo "🚀 Creating GitHub issues from code review templates..."
echo ""

# Check if gh CLI is installed
if ! command -v gh &> /dev/null; then
    echo "❌ Error: GitHub CLI (gh) is not installed"
    echo "📦 Install: https://cli.github.com/"
    exit 1
fi

# Check if authenticated
if ! gh auth status &> /dev/null; then
    echo "❌ Error: Not authenticated with GitHub CLI"
    echo "🔑 Run: gh auth login"
    exit 1
fi

echo "✅ GitHub CLI authenticated"
echo ""

# Issue 1: SecurityHeadersMiddleware
echo "📝 Creating Issue #1: SecurityHeadersMiddleware..."
gh issue create \
  --title "🔐 [Security] Implement SecurityHeadersMiddleware" \
  --body-file "$SCRIPT_DIR/01-security-headers-middleware.md" \
  --label "priority: critical,type: security,area: backend,effort: small,sprint: 1"

# Issue 2: CORS Whitelist Headers
echo "📝 Creating Issue #2: CORS Whitelist Headers..."
gh issue create \
  --title "🔒 [Security] CORS Whitelist Headers (Remove AllowAnyHeader)" \
  --body-file "$SCRIPT_DIR/02-cors-whitelist-headers.md" \
  --label "priority: critical,type: security,area: backend,effort: small,sprint: 1"

# Issue 3: FluentValidation Authentication
echo "📝 Creating Issue #3: FluentValidation Authentication..."
gh issue create \
  --title "✅ [Validation] FluentValidation for Authentication Context" \
  --body-file "$SCRIPT_DIR/03-fluentvalidation-authentication.md" \
  --label "priority: high,type: enhancement,area: backend,effort: medium,sprint: 2"

# Issue 4: NSwag Code Generation
echo "📝 Creating Issue #4: NSwag Code Generation..."
gh issue create \
  --title "🔧 [DX] NSwag TypeScript Code Generation from OpenAPI" \
  --body-file "$SCRIPT_DIR/04-nswag-code-generation.md" \
  --label "priority: high,type: enhancement,area: frontend,area: backend,effort: large,sprint: 3"

# Issue 5: Streaming Hooks Consolidation
echo "📝 Creating Issue #5: Streaming Hooks Consolidation..."
gh issue create \
  --title "🔄 [Refactor] Consolidate Streaming Hooks" \
  --body-file "$SCRIPT_DIR/05-streaming-hooks-consolidation.md" \
  --label "priority: medium,type: refactor,area: frontend,effort: medium,sprint: 4"

# Issue 6: Rate Limiting UX
echo "📝 Creating Issue #6: Rate Limiting UX..."
gh issue create \
  --title "⏱️ [UX] Rate Limiting User Experience with Retry-After" \
  --body-file "$SCRIPT_DIR/06-rate-limiting-ux.md" \
  --label "priority: medium,type: enhancement,area: frontend,effort: small,ux,sprint: 4"

# Issue 7: Retry Logic
echo "📝 Creating Issue #7: Retry Logic Exponential Backoff..."
gh issue create \
  --title "🔄 [Resilience] Retry Logic with Exponential Backoff" \
  --body-file "$SCRIPT_DIR/07-retry-logic-exponential-backoff.md" \
  --label "priority: medium,type: enhancement,area: frontend,effort: medium,resilience,sprint: 5"

# Issue 8: Request Deduplication
echo "📝 Creating Issue #8: Request Deduplication..."
gh issue create \
  --title "🔀 [Performance] Request Deduplication Cache" \
  --body-file "$SCRIPT_DIR/08-request-deduplication.md" \
  --label "priority: medium,type: enhancement,area: frontend,effort: medium,performance,sprint: 5"

echo ""
echo "✅ All 8 issues created successfully!"
echo ""
echo "📊 View issues: gh issue list --label 'sprint: 1'"
echo "🔗 Or visit: https://github.com/DegrassiAaron/meepleai-monorepo/issues"
