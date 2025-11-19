# PowerShell script to create GitHub issues from templates
# Generated from Code Review - Backend-Frontend Interactions (2025-01-19)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "🚀 Creating GitHub issues from code review templates..." -ForegroundColor Cyan
Write-Host ""

# Check if gh CLI is installed
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Error: GitHub CLI (gh) is not installed" -ForegroundColor Red
    Write-Host "📦 Install: https://cli.github.com/" -ForegroundColor Yellow
    exit 1
}

# Check if authenticated
gh auth status 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Error: Not authenticated with GitHub CLI" -ForegroundColor Red
    Write-Host "🔑 Run: gh auth login" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ GitHub CLI authenticated" -ForegroundColor Green
Write-Host ""

# Issue 1: SecurityHeadersMiddleware
Write-Host "📝 Creating Issue #1: SecurityHeadersMiddleware..." -ForegroundColor Yellow
gh issue create `
  --title "🔐 [Security] Implement SecurityHeadersMiddleware" `
  --body-file "$ScriptDir/01-security-headers-middleware.md" `
  --label "priority: critical,type: security,area: backend,effort: small,sprint: 1"

# Issue 2: CORS Whitelist Headers
Write-Host "📝 Creating Issue #2: CORS Whitelist Headers..." -ForegroundColor Yellow
gh issue create `
  --title "🔒 [Security] CORS Whitelist Headers (Remove AllowAnyHeader)" `
  --body-file "$ScriptDir/02-cors-whitelist-headers.md" `
  --label "priority: critical,type: security,area: backend,effort: small,sprint: 1"

# Issue 3: FluentValidation Authentication
Write-Host "📝 Creating Issue #3: FluentValidation Authentication..." -ForegroundColor Yellow
gh issue create `
  --title "✅ [Validation] FluentValidation for Authentication Context" `
  --body-file "$ScriptDir/03-fluentvalidation-authentication.md" `
  --label "priority: high,type: enhancement,area: backend,effort: medium,sprint: 2"

# Issue 4: NSwag Code Generation
Write-Host "📝 Creating Issue #4: NSwag Code Generation..." -ForegroundColor Yellow
gh issue create `
  --title "🔧 [DX] NSwag TypeScript Code Generation from OpenAPI" `
  --body-file "$ScriptDir/04-nswag-code-generation.md" `
  --label "priority: high,type: enhancement,area: frontend,area: backend,effort: large,sprint: 3"

# Issue 5: Streaming Hooks Consolidation
Write-Host "📝 Creating Issue #5: Streaming Hooks Consolidation..." -ForegroundColor Yellow
gh issue create `
  --title "🔄 [Refactor] Consolidate Streaming Hooks" `
  --body-file "$ScriptDir/05-streaming-hooks-consolidation.md" `
  --label "priority: medium,type: refactor,area: frontend,effort: medium,sprint: 4"

# Issue 6: Rate Limiting UX
Write-Host "📝 Creating Issue #6: Rate Limiting UX..." -ForegroundColor Yellow
gh issue create `
  --title "⏱️ [UX] Rate Limiting User Experience with Retry-After" `
  --body-file "$ScriptDir/06-rate-limiting-ux.md" `
  --label "priority: medium,type: enhancement,area: frontend,effort: small,ux,sprint: 4"

# Issue 7: Retry Logic
Write-Host "📝 Creating Issue #7: Retry Logic Exponential Backoff..." -ForegroundColor Yellow
gh issue create `
  --title "🔄 [Resilience] Retry Logic with Exponential Backoff" `
  --body-file "$ScriptDir/07-retry-logic-exponential-backoff.md" `
  --label "priority: medium,type: enhancement,area: frontend,effort: medium,resilience,sprint: 5"

# Issue 8: Request Deduplication
Write-Host "📝 Creating Issue #8: Request Deduplication..." -ForegroundColor Yellow
gh issue create `
  --title "🔀 [Performance] Request Deduplication Cache" `
  --body-file "$ScriptDir/08-request-deduplication.md" `
  --label "priority: medium,type: enhancement,area: frontend,effort: medium,performance,sprint: 5"

Write-Host ""
Write-Host "✅ All 8 issues created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 View issues: gh issue list --label 'sprint: 1'" -ForegroundColor Cyan
Write-Host "🔗 Or visit: https://github.com/DegrassiAaron/meepleai-monorepo/issues" -ForegroundColor Cyan
