# Cleanup script for tmpclaude temporary files (PowerShell)
# Usage: .\scripts\cleanup-tmpclaude.ps1

$ErrorActionPreference = "Stop"

$RootDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Write-Host "🧹 Searching for tmpclaude temporary files..." -ForegroundColor Cyan

# Find all tmpclaude files
$files = Get-ChildItem -Path $RootDir -Filter "tmpclaude*" -File -Recurse -ErrorAction SilentlyContinue

if ($files.Count -eq 0) {
    Write-Host "✅ No tmpclaude files found. Repository is clean!" -ForegroundColor Green
    exit 0
}

Write-Host "📁 Found $($files.Count) tmpclaude file(s):" -ForegroundColor Yellow
$files | ForEach-Object { Write-Host "  - $($_.FullName)" }

Write-Host ""
$response = Read-Host "❓ Do you want to delete these files? (y/N)"

if ($response -eq 'y' -or $response -eq 'Y') {
    $files | Remove-Item -Force
    Write-Host "✅ Cleanup completed successfully!" -ForegroundColor Green
} else {
    Write-Host "ℹ️  Cleanup cancelled." -ForegroundColor Blue
}
