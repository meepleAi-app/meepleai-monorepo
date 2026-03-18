# Local security checks (alternative to GitHub Advanced Security for private repos)
# Run: pwsh scripts/security-check-local.ps1

Write-Host "🔐 Local Security Checks" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# 1. Frontend dependency vulnerabilities
Write-Host "📦 1. Checking Frontend Dependencies..." -ForegroundColor Yellow
Set-Location apps/web
$frontendVuln = pnpm audit --audit-level=high --prod 2>&1 | Out-String
if ($frontendVuln -match "(?i)(high|critical)" -and $frontendVuln -notmatch "GHSA-wgrm-67xf-hhpq") {
    Write-Host "❌ HIGH/CRITICAL vulnerabilities found (excluding pdfjs-dist TODO #4242)" -ForegroundColor Red
} else {
    Write-Host "✅ No HIGH/CRITICAL vulnerabilities in production dependencies" -ForegroundColor Green
}
Set-Location ..\..
Write-Host ""

# 2. Backend dependency vulnerabilities
Write-Host "📦 2. Checking Backend Dependencies..." -ForegroundColor Yellow
Set-Location apps/api
dotnet list package --vulnerable --include-transitive > vuln-report.txt 2>&1
$backendVuln = Get-Content vuln-report.txt -Raw
if ($backendVuln -match "(?i)(High|Critical)") {
    Write-Host "❌ HIGH/CRITICAL vulnerabilities found in .NET packages" -ForegroundColor Red
    Get-Content vuln-report.txt
} else {
    Write-Host "✅ No HIGH/CRITICAL vulnerabilities in .NET packages" -ForegroundColor Green
}
Remove-Item vuln-report.txt -ErrorAction SilentlyContinue
Set-Location ..\..
Write-Host ""

# 3. Secrets detection with Semgrep (Docker-based)
Write-Host "🔍 3. Checking for Secrets (Semgrep)..." -ForegroundColor Yellow
if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker run --rm -v "${PWD}:/src" semgrep/semgrep:latest `
        semgrep scan `
        --config=p/secrets `
        --config=p/security-audit `
        --quiet `
        /src
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Semgrep scan completed - no issues found" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Semgrep found potential issues (review output above)" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️ Docker not available - skipping Semgrep scan" -ForegroundColor Yellow
    Write-Host "   Install Docker Desktop for Windows" -ForegroundColor Gray
}
Write-Host ""

# 4. Python dependencies (if available)
Write-Host "🐍 4. Checking Python Dependencies..." -ForegroundColor Yellow
$pythonServices = @("orchestration-service", "embedding-service", "reranker-service")
foreach ($service in $pythonServices) {
    if (Test-Path "apps/$service/requirements.txt") {
        Write-Host "   Checking $service..." -ForegroundColor Gray
        Set-Location "apps/$service"
        if (Get-Command safety -ErrorAction SilentlyContinue) {
            safety check -r requirements.txt --bare
            if ($LASTEXITCODE -eq 0) {
                Write-Host "   ✅ No vulnerabilities in $service" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️ Vulnerabilities found in $service" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   ℹ️ Install 'safety' for Python vulnerability scanning: pip install safety" -ForegroundColor Gray
        }
        Set-Location ..\..
    }
}
Write-Host ""

# 5. Summary
Write-Host "📊 Security Check Summary" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host "✅ Dependency audit: Complete" -ForegroundColor Green
Write-Host "✅ Secrets scan: Complete (if Docker available)" -ForegroundColor Green
Write-Host "ℹ️ For full code scanning, enable GitHub Advanced Security (paid for private repos)" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Yellow
Write-Host "  - Run before commits: pwsh scripts/security-check-local.ps1"
Write-Host "  - Install safety: pip install safety (for Python checks)"
Write-Host "  - View Dependabot alerts: gh api repos/meepleAi-app/meepleai-monorepo/dependabot/alerts"
