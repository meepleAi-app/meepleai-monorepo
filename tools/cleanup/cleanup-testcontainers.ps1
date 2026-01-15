<#
.SYNOPSIS
    Quick cleanup for orphaned Testcontainers and test processes

.DESCRIPTION
    Removes orphaned Docker containers from Testcontainers and kills
    hanging testhost processes that can lock DLL files.

.EXAMPLE
    .\cleanup-testcontainers.ps1

.NOTES
    Author: MeepleAI Team
    Issue: #2449 - Testcontainers cleanup automation
#>

Write-Host "🧹 Cleaning up Testcontainers and test processes..." -ForegroundColor Cyan
Write-Host ""

# Check if Docker is available
$dockerAvailable = Get-Command docker -ErrorAction SilentlyContinue

if ($dockerAvailable) {
    # Get orphaned Testcontainers
    $orphanedIds = docker ps -a --filter "label=org.testcontainers=true" -q 2>$null

    if ($orphanedIds) {
        $count = ($orphanedIds | Measure-Object -Line).Lines
        Write-Host "   Found $count orphaned Testcontainers" -ForegroundColor Yellow

        # Show what we're removing
        docker ps -a --filter "label=org.testcontainers=true" --format "table {{.ID}}\t{{.Image}}\t{{.Status}}" 2>$null
        Write-Host ""

        # Remove them
        $orphanedIds | ForEach-Object { docker rm -f $_ 2>$null } | Out-Null
        Write-Host "   ✅ Removed orphaned containers" -ForegroundColor Green
    } else {
        Write-Host "   ✅ No orphaned Testcontainers found" -ForegroundColor Green
    }
} else {
    Write-Host "   ⚠️  Docker not found, skipping container cleanup" -ForegroundColor Yellow
}

Write-Host ""

# Cleanup testhost processes
$scriptPath = Join-Path $PSScriptRoot "cleanup-test-processes.ps1"
if (Test-Path $scriptPath) {
    Write-Host "   Cleaning up testhost processes..." -ForegroundColor Gray
    & $scriptPath -TestHostOnly
} else {
    # Inline cleanup if script not found
    $testHosts = Get-Process -Name "testhost" -ErrorAction SilentlyContinue
    if ($testHosts) {
        $testHosts | ForEach-Object {
            $runtime = (Get-Date) - $_.StartTime
            if ($runtime.TotalMinutes -gt 1) {
                Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
                Write-Host "   Killed testhost (PID: $($_.Id), running $([math]::Round($runtime.TotalMinutes, 1)) min)" -ForegroundColor Red
            }
        }
    }
}

Write-Host ""
Write-Host "✅ Cleanup complete!" -ForegroundColor Green
Write-Host ""

# Show current Docker status
if ($dockerAvailable) {
    Write-Host "Current Docker containers:" -ForegroundColor Cyan
    docker ps --format "table {{.ID}}\t{{.Image}}\t{{.Status}}" 2>$null | Select-Object -First 10
}
