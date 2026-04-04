<#
.SYNOPSIS
    Cleanup zombie development processes on common ports

.DESCRIPTION
    Detects and terminates zombie node.exe and other development processes
    that may conflict with Docker containers. Prevents port conflict issues
    like Issue #3797.

.PARAMETER DryRun
    Preview what would be cleaned up without actually killing processes

.PARAMETER ShowDetails
    Show detailed information about port scanning and process detection

.EXAMPLE
    .\cleanup-dev-ports.ps1
    Cleans up zombie processes on all development ports

.EXAMPLE
    .\cleanup-dev-ports.ps1 -DryRun -ShowDetails
    Shows what would be cleaned up with detailed logging

.NOTES
    Issue #3797: Created to prevent zombie node.exe processes from
    intercepting Docker port forwarding on Windows/WSL2
#>

[CmdletBinding()]
param(
    [switch]$DryRun,
    [switch]$ShowDetails
)

# Development ports to check
$DevPorts = @{
    3000 = @{
        Name = "Next.js Web"
        AllowedProcesses = @("node")
        Description = "Frontend development server"
    }
    8080 = @{
        Name = "API Backend"
        AllowedProcesses = @("dotnet", "Api")
        Description = ".NET API server"
    }
    5432 = @{
        Name = "PostgreSQL"
        AllowedProcesses = @("postgres")
        Description = "Database server"
    }
    6379 = @{
        Name = "Redis"
        AllowedProcesses = @("redis-server")
        Description = "Cache server"
    }
    6333 = @{
        Name = "Qdrant"
        AllowedProcesses = @("qdrant")
        Description = "Vector database"
    }
}

function Write-Status {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Warning', 'Error', 'Success')]
        [string]$Level = 'Info'
    )

    $color = switch ($Level) {
        'Info'    { 'Cyan' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
        'Success' { 'Green' }
    }

    $prefix = switch ($Level) {
        'Info'    { '[INFO]' }
        'Warning' { '[WARN]' }
        'Error'   { '[ERROR]' }
        'Success' { '[OK]' }
    }

    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Get-ProcessOnPort {
    param([int]$Port)

    try {
        # Use netstat (fast) instead of Get-NetTCPConnection (slow, can hang >10s)
        $netstatLines = netstat -ano 2>$null | Select-String "LISTENING" |
            Where-Object { $_ -match ":$Port\s" }

        if ($netstatLines) {
            $pids = $netstatLines | ForEach-Object {
                if ($_ -match '\s+(\d+)\s*$') { [int]$Matches[1] }
            } | Sort-Object -Unique

            return $pids | ForEach-Object {
                $proc = Get-Process -Id $_ -ErrorAction SilentlyContinue
                if ($proc) {
                    [PSCustomObject]@{
                        Port = $Port
                        PID = $proc.Id
                        ProcessName = $proc.ProcessName
                        Path = $proc.Path
                        StartTime = $proc.StartTime
                    }
                }
            }
        }
    }
    catch {
        if ($ShowDetails) {
            Write-Status "Error checking port ${Port}: $_" -Level Error
        }
    }

    return $null
}

function Test-IsDockerProcess {
    param([string]$ProcessPath)

    if ([string]::IsNullOrEmpty($ProcessPath)) {
        return $false
    }

    # Docker processes typically run from Docker installation directory
    $dockerPaths = @(
        "Docker\Docker",
        "com.docker",
        "docker-proxy"
    )

    foreach ($path in $dockerPaths) {
        if ($ProcessPath -like "*$path*") {
            return $true
        }
    }

    return $false
}

# Main execution
Write-Status "Starting development port cleanup..." -Level Info
Write-Status "Mode: $(if ($DryRun) { 'DRY RUN (no processes will be killed)' } else { 'LIVE EXECUTION' })" -Level Warning

if ($DryRun) {
    Write-Host ""
}

$totalFound = 0
$totalKilled = 0
$dockerProcessesSkipped = 0

foreach ($portEntry in $DevPorts.GetEnumerator() | Sort-Object Name) {
    $port = $portEntry.Key
    $config = $portEntry.Value

    if ($ShowDetails) {
        Write-Status "Checking port $port ($($config.Name))..." -Level Info
    }

    $processes = Get-ProcessOnPort -Port $port

    if ($processes) {
        foreach ($proc in $processes) {
            $totalFound++

            # Skip Docker processes
            if (Test-IsDockerProcess -ProcessPath $proc.Path) {
                $dockerProcessesSkipped++
                if ($ShowDetails) {
                    Write-Status "  Skipping Docker process: $($proc.ProcessName) (PID $($proc.PID)) on port $port" -Level Info
                }
                continue
            }

            # Check if it's an allowed development process
            $isDevProcess = $config.AllowedProcesses -contains $proc.ProcessName

            if ($isDevProcess) {
                $uptime = (Get-Date) - $proc.StartTime
                $uptimeStr = "{0:hh\:mm\:ss}" -f $uptime

                Write-Status "  Found: $($proc.ProcessName) (PID $($proc.PID)) on port $port - Uptime: $uptimeStr" -Level Warning

                if ($DryRun) {
                    Write-Status "  [DRY RUN] Would kill: PID $($proc.PID)" -Level Info
                }
                else {
                    try {
                        Stop-Process -Id $proc.PID -Force -ErrorAction Stop
                        $totalKilled++
                        Write-Status "  Killed: PID $($proc.PID) on port $port" -Level Success
                    }
                    catch {
                        Write-Status "  Failed to kill PID $($proc.PID): $_" -Level Error
                    }
                }
            }
            else {
                if ($ShowDetails) {
                    Write-Status "  Skipping non-dev process: $($proc.ProcessName) (PID $($proc.PID)) on port $port" -Level Info
                }
            }
        }
    }
    else {
        if ($ShowDetails) {
            Write-Status "  Port $port is free" -Level Success
        }
    }
}

# Summary
Write-Host ""
Write-Status "=== Cleanup Summary ===" -Level Info
Write-Status "Processes found: $totalFound" -Level Info
Write-Status "Docker processes (skipped): $dockerProcessesSkipped" -Level Info

if ($DryRun) {
    Write-Status "Processes that would be killed: $($totalFound - $dockerProcessesSkipped)" -Level Warning
}
else {
    Write-Status "Processes killed: $totalKilled" -Level Success
}

if ($totalKilled -gt 0 -or ($DryRun -and ($totalFound - $dockerProcessesSkipped) -gt 0)) {
    Write-Host ""
    Write-Status "Port cleanup completed! Safe to start Docker containers." -Level Success
}
elseif ($totalFound -eq 0) {
    Write-Status "All ports are free! No cleanup needed." -Level Success
}
else {
    Write-Status "Only Docker processes found - all good!" -Level Success
}

# Exit code
if ($DryRun) {
    exit 0
}
else {
    exit $(if ($totalKilled -eq ($totalFound - $dockerProcessesSkipped)) { 0 } else { 1 })
}
