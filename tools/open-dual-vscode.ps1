# Open Dual VS Code Instances for Backend + Frontend Development
# Usage: pwsh tools/open-dual-vscode.ps1

param(
    [switch]$BackendOnly,
    [switch]$FrontendOnly
)

Write-Host "🚀 Opening VS Code instances for parallel development..." -ForegroundColor Cyan
Write-Host ""

$backendPath = "D:/Repositories/meepleai-monorepo-backend"
$frontendPath = "D:/Repositories/meepleai-monorepo-frontend"

# Verify worktrees exist
if (-not (Test-Path $backendPath) -and -not $BackendOnly) {
    Write-Host "❌ Backend worktree not found at: $backendPath" -ForegroundColor Red
    Write-Host "   Run: git worktree add $backendPath -b backend-dev" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $frontendPath) -and -not $FrontendOnly) {
    Write-Host "❌ Frontend worktree not found at: $frontendPath" -ForegroundColor Red
    Write-Host "   Run: git worktree add $frontendPath -b frontend-dev" -ForegroundColor Yellow
    exit 1
}

# Open Backend VS Code
if (-not $FrontendOnly) {
    Write-Host "📂 Opening Backend VS Code..." -ForegroundColor Green
    Write-Host "   Location: $backendPath" -ForegroundColor Gray
    code $backendPath
    Start-Sleep -Seconds 2
}

# Open Frontend VS Code
if (-not $BackendOnly) {
    Write-Host "🎨 Opening Frontend VS Code..." -ForegroundColor Blue
    Write-Host "   Location: $frontendPath" -ForegroundColor Gray
    code $frontendPath
    Start-Sleep -Seconds 1
}

Write-Host ""
Write-Host "✅ VS Code instances opened!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  Backend: cd apps/api; dotnet run" -ForegroundColor Gray
Write-Host "  Frontend: cd apps/web; pnpm dev" -ForegroundColor Gray
Write-Host ""
Write-Host "Documentation: claudedocs/DUAL-VSCODE-SETUP.md" -ForegroundColor Gray
