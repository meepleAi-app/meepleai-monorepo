# Docker Compose Resource Monitoring Script (PowerShell)
# Purpose: Monitor resource usage of all containers and help identify resource limit issues
# Usage: .\docker-resource-monitor.ps1 [options]

[CmdletBinding()]
param(
    [Parameter(ParameterSetName = 'Baseline')]
    [switch]$Baseline,

    [Parameter(ParameterSetName = 'Watch')]
    [switch]$Watch,

    [Parameter(ParameterSetName = 'Watch')]
    [int]$WatchInterval = 2,

    [Parameter(ParameterSetName = 'Export')]
    [string]$Export,

    [Parameter(ParameterSetName = 'Analyze')]
    [string]$Analyze,

    [int]$Threshold = 85,

    [switch]$Help
)

$ErrorActionPreference = 'Stop'

# Paths
$ScriptDir = Split-Path -Parent $PSCommandPath
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$BaselineFile = Join-Path $ProjectRoot 'infra/resource-baseline.txt'

# =============================================================================
# Functions
# =============================================================================

function Show-Help {
    Write-Host "Docker Compose Resource Monitoring Tool" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\docker-resource-monitor.ps1 [options]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Baseline              Capture baseline (one-time snapshot)"
    Write-Host "  -Watch                 Watch in real-time (default: 2 seconds)"
    Write-Host "  -WatchInterval <sec>   Watch interval in seconds"
    Write-Host "  -Export <file>         Export to CSV file"
    Write-Host "  -Analyze <file>        Analyze saved snapshot"
    Write-Host "  -Threshold <percent>   Alert threshold (default: 85%)"
    Write-Host "  -Help                  Show this help"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  # Capture baseline for later comparison"
    Write-Host "  .\docker-resource-monitor.ps1 -Baseline"
    Write-Host ""
    Write-Host "  # Watch in real-time (updates every 2 seconds)"
    Write-Host "  .\docker-resource-monitor.ps1 -Watch"
    Write-Host ""
    Write-Host "  # Watch with 5-second intervals"
    Write-Host "  .\docker-resource-monitor.ps1 -Watch -WatchInterval 5"
    Write-Host ""
    Write-Host "  # Export stats to CSV"
    Write-Host "  .\docker-resource-monitor.ps1 -Export stats.csv"
    Write-Host ""
}

function Get-DockerStats {
    $stats = docker stats --all --no-stream --format "{{.Container}}`t{{.MemPerc}}`t{{.MemUsage}}`t{{.CPUPerc}}`t{{.NetIO}}" 2>$null
    return $stats
}

function Show-StatsTable {
    param([int]$MemoryThreshold = 85)

    Write-Host "╔════════════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Blue
    Write-Host "║ Container            │ Memory %-Memory/Limit  │ CPU%  │ Net I/O  │ Status        ║" -ForegroundColor Blue
    Write-Host "╠════════════════════════════════════════════════════════════════════════════════════╣" -ForegroundColor Blue

    $stats = Get-DockerStats
    foreach ($line in $stats) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }

        $parts = $line -split "`t"
        if ($parts.Count -lt 5) { continue }

        $container = $parts[0].Substring(0, [Math]::Min(20, $parts[0].Length))
        $memPercStr = $parts[1] -replace '%', ''
        $memUsage = $parts[2]
        $cpuPercStr = $parts[3] -replace '%', ''
        $netIO = $parts[4]

        # Parse memory percentage
        $memPerc = 0
        if ([double]::TryParse($memPercStr, [ref]$memPerc)) {
            # Color code high usage
            $memColor = 'Green'
            if ($memPerc -gt $MemoryThreshold) {
                $memColor = 'Red'
            }
            elseif ($memPerc -gt 70) {
                $memColor = 'Yellow'
            }

            $memPercFormatted = "{0,6}" -f "$memPercStr%"
            Write-Host ("║ {0,-20} │ " -f $container) -NoNewline
            Write-Host $memPercFormatted -ForegroundColor $memColor -NoNewline
            Write-Host ("{0,-16} │ {1,-5} │ {2,-8} │             ║" -f $memUsage, "$cpuPercStr%", $netIO)
        }
    }

    Write-Host "╚════════════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Blue
}

function Save-Baseline {
    Write-Host "📊 Capturing resource baseline..." -ForegroundColor Blue

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $content = "Timestamp: $timestamp`r`n`r`n"
    $content += (Get-DockerStats | Out-String)

    $content | Out-File -FilePath $BaselineFile -Encoding UTF8

    Write-Host "Saved to: $BaselineFile"
    Write-Host "✓ Baseline captured" -ForegroundColor Green
}

function Watch-Resources {
    param([int]$Interval)

    Write-Host "📊 Watching resource usage (interval: ${Interval}s)..." -ForegroundColor Blue
    Write-Host "Press Ctrl+C to stop"
    Write-Host ""

    while ($true) {
        Clear-Host
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        Write-Host "Resource Usage Monitor - $timestamp" -ForegroundColor Blue
        Write-Host ""

        Show-StatsTable -MemoryThreshold $Threshold

        Write-Host ""
        Write-Host "Note: Memory usage includes OS page cache. Watch % increase over time for leaks." -ForegroundColor Yellow
        Write-Host "Threshold for alerts: $Threshold%"

        Start-Sleep -Seconds $Interval
    }
}

function Export-ToCsv {
    param([string]$OutputFile)

    Write-Host "📊 Exporting stats to CSV: $OutputFile" -ForegroundColor Blue

    $csv = "Timestamp,Container,CPUUsage%,MemoryUsage,MemoryUsage%`r`n"

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $stats = Get-DockerStats

    foreach ($line in $stats) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $csv += "$timestamp,$line`r`n"
    }

    $csv | Out-File -FilePath $OutputFile -Encoding UTF8
    Write-Host "✓ Exported to $OutputFile" -ForegroundColor Green
}

function Analyze-Baseline {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) {
        Write-Host "✗ File not found: $FilePath" -ForegroundColor Red
        return
    }

    Write-Host "📊 Analyzing: $FilePath" -ForegroundColor Blue
    Write-Host ""

    $content = Get-Content -Path $FilePath
    $analyzing = $false

    foreach ($line in $content) {
        if ($line -match '^[a-zA-Z0-9]') {
            $analyzing = $true

            $parts = $line -split '\s+'
            if ($parts.Count -ge 4) {
                $container = $parts[0]
                $memPercStr = $parts[1] -replace '%', ''
                $cpuPercStr = $parts[3] -replace '%', ''

                Write-Host "$container: Memory=$memPercStr%, CPU=$cpuPercStr%"

                $memPerc = 0
                if ([double]::TryParse($memPercStr, [ref]$memPerc)) {
                    if ($memPerc -gt 80) {
                        Write-Host "  ⚠️  Memory usage high: $memPerc%" -ForegroundColor Yellow
                    }
                    if ($memPerc -gt 90) {
                        Write-Host "  🚨 CRITICAL: Memory usage very high: $memPerc%" -ForegroundColor Red
                    }
                }
            }
        }
    }
}

# =============================================================================
# Main
# =============================================================================

if ($Help -or $PSBoundParameters.Count -eq 0) {
    Show-Help
    exit 0
}

try {
    if ($Baseline) {
        Save-Baseline
    }
    elseif ($Watch) {
        Watch-Resources -Interval $WatchInterval
    }
    elseif ($Export) {
        Export-ToCsv -OutputFile $Export
    }
    elseif ($Analyze) {
        Analyze-Baseline -FilePath $Analyze
    }
}
catch {
    Write-Host "✗ An error occurred: $_" -ForegroundColor Red
    exit 1
}
