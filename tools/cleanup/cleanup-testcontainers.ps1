<#
.SYNOPSIS
    Comprehensive cleanup for orphaned Testcontainers and test processes

.DESCRIPTION
    Removes orphaned Docker containers from Testcontainers, kills
    hanging testhost processes, and cleans up unused networks/volumes.
    Detects port conflicts and force-kills stalled containers.

.EXAMPLE
    .\cleanup-testcontainers.ps1

.NOTES
    Author: MeepleAI Team
    Issue: #2474 - Testcontainers infrastructure stability fixes
    Previous: #2449 - Testcontainers cleanup automation
#>

Write-Host "🧹 Cleaning up Testcontainers and test processes..." -ForegroundColor Cyan
Write-Host ""

# Check if Docker is available
$dockerAvailable = Get-Command docker -ErrorAction SilentlyContinue

if ($dockerAvailable) {
    # Get all Testcontainers (running and stopped)
    $allContainers = docker ps -a --filter "label=org.testcontainers=true" --format "{{.ID}}\t{{.Image}}\t{{.Status}}\t{{.CreatedAt}}" 2>$null

    if ($allContainers) {
        $containerList = $allContainers -split "`n"
        $count = $containerList.Count
        Write-Host "   Found $count Testcontainer(s)" -ForegroundColor Yellow

        # Show what we're removing
        docker ps -a --filter "label=org.testcontainers=true" --format "table {{.ID}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}" 2>$null
        Write-Host ""

        # Force-kill containers running > 5 minutes (stalled containers)
        $stalledCount = 0
        foreach ($line in $containerList) {
            $parts = $line -split "`t"
            if ($parts.Count -ge 4) {
                $containerId = $parts[0]
                $status = $parts[2]

                # Check if running
                if ($status -match "^Up") {
                    # Get container start time
                    $startTime = docker inspect $containerId --format "{{.State.StartedAt}}" 2>$null
                    if ($startTime) {
                        $started = [DateTime]::Parse($startTime)
                        $runtime = (Get-Date) - $started

                        if ($runtime.TotalMinutes -gt 5) {
                            Write-Host "   🔴 Force-killing stalled container $containerId (running $([math]::Round($runtime.TotalMinutes, 1)) min)" -ForegroundColor Red
                            docker kill $containerId 2>$null | Out-Null
                            $stalledCount++
                        }
                    }
                }
            }
        }

        if ($stalledCount -gt 0) {
            Write-Host "   ⚠️  Force-killed $stalledCount stalled container(s)" -ForegroundColor Yellow
        }

        # Remove all containers
        $containerIds = docker ps -a --filter "label=org.testcontainers=true" -q 2>$null
        if ($containerIds) {
            $containerIds | ForEach-Object { docker rm -f $_ 2>$null } | Out-Null
            Write-Host "   ✅ Removed orphaned containers" -ForegroundColor Green
        }

        # Cleanup orphaned networks
        $orphanedNetworks = docker network ls --filter "label=org.testcontainers=true" -q 2>$null
        if ($orphanedNetworks) {
            $networkCount = ($orphanedNetworks | Measure-Object -Line).Lines
            $orphanedNetworks | ForEach-Object { docker network rm $_ 2>$null } | Out-Null
            Write-Host "   ✅ Removed $networkCount orphaned network(s)" -ForegroundColor Green
        }

        # Cleanup orphaned volumes
        $orphanedVolumes = docker volume ls --filter "label=org.testcontainers=true" -q 2>$null
        if ($orphanedVolumes) {
            $volumeCount = ($orphanedVolumes | Measure-Object -Line).Lines
            $orphanedVolumes | ForEach-Object { docker volume rm $_ 2>$null } | Out-Null
            Write-Host "   ✅ Removed $volumeCount orphaned volume(s)" -ForegroundColor Green
        }
    } else {
        Write-Host "   ✅ No orphaned Testcontainers found" -ForegroundColor Green
    }

    # Check for port conflicts on common test ports
    Write-Host ""
    Write-Host "   Checking for port conflicts..." -ForegroundColor Gray
    $testPorts = @(5432, 6379, 6333)  # PostgreSQL, Redis, Qdrant
    $portsInUse = @()

    foreach ($port in $testPorts) {
        $connection = Test-NetConnection -ComputerName localhost -Port $port -InformationLevel Quiet -WarningAction SilentlyContinue 2>$null
        if ($connection) {
            $portsInUse += $port
        }
    }

    if ($portsInUse.Count -gt 0) {
        Write-Host "   ⚠️  Ports in use: $($portsInUse -join ', ')" -ForegroundColor Yellow
        Write-Host "   💡 Tip: These may be used by running services or containers" -ForegroundColor Cyan
    } else {
        Write-Host "   ✅ All test ports available (5432, 6379, 6333)" -ForegroundColor Green
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
