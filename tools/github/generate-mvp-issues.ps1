# MeepleAI MVP - GitHub Issues Generation Script (PowerShell)
# Creates issues for Sprint 1-5 based on product specification
# Prerequisites: gh CLI installed and authenticated

$ErrorActionPreference = "Stop"

Write-Host "🚀 Generating GitHub Issues for MeepleAI MVP (Sprint 1-5)" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host ""

# Check if gh CLI is installed
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Error: GitHub CLI (gh) is not installed" -ForegroundColor Red
    Write-Host "Install it from: https://cli.github.com/" -ForegroundColor Yellow
    exit 1
}

# Check if authenticated
try {
    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Not authenticated"
    }
} catch {
    Write-Host "❌ Error: Not authenticated with GitHub CLI" -ForegroundColor Red
    Write-Host "Run: gh auth login" -ForegroundColor Yellow
    exit 1
}

Write-Host "✅ GitHub CLI authenticated" -ForegroundColor Green
Write-Host ""

# Execute the bash script (if on Windows with WSL)
if (Get-Command bash -ErrorAction SilentlyContinue) {
    Write-Host "Executing bash script..." -ForegroundColor Cyan
    bash ./tools/generate-mvp-issues.sh
} else {
    Write-Host "⚠️  Warning: bash not found. Please run the Linux script or install WSL." -ForegroundColor Yellow
    Write-Host "Alternatively, use GitHub web UI to create issues manually from the bash script." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✅ Script execution complete!" -ForegroundColor Green
