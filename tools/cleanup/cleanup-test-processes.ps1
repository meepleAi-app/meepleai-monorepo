<#
.SYNOPSIS
    Cleanup stale test processes that lock DLL files during dotnet test

.DESCRIPTION
    Automatically kills hanging test processes (testhost, VBCSCompiler, node, dotnet, jest)
    to prevent MSB3027 errors (DLL file locking) during dotnet build/test.

    Includes safety protections for IDEs and development tools.

.PARAMETER TestHostOnly
    Only kill testhost processes (focused cleanup for DLL locking issues)

.PARAMETER MemoryThresholdMB
    Memory threshold in MB for process cleanup (default: 100)

.PARAMETER DryRun
    Show what would be killed without actually killing processes

.PARAMETER Verbose
    Show detailed process information

.EXAMPLE
    # Quick cleanup before dotnet test (recommended)
    .\cleanup-test-processes.ps1 -TestHostOnly

.EXAMPLE
    # Full cleanup with dry run
    .\cleanup-test-processes.ps1 -DryRun -Verbose

.EXAMPLE
    # Cleanup processes using >100MB memory
    .\cleanup-test-processes.ps1 -MemoryThresholdMB 100

.NOTES
    Author: MeepleAI Team
    Issue: #2210 - Automated cleanup for stale testhost processes
    Platform: Windows only (Unix-like systems don't experience DLL locking)
#>

param(
    [switch]$TestHostOnly = $false,
    [int]$MemoryThresholdMB = 100,
    [switch]$DryRun = $false,
    [switch]$Verbose = $false
)

Write-Host "=== Test Process Cleanup ===" -ForegroundColor Cyan

# Define processes to check
$processesToCheck = if ($TestHostOnly) {
    @('testhost')
    Write-Host "Mode: TestHost Only (DLL locking fix)" -ForegroundColor Yellow
} else {
    @('node', 'dotnet', 'jest', 'testhost', 'VBCSCompiler')
    Write-Host "Mode: Full Cleanup" -ForegroundColor Gray
}

Write-Host "Memory threshold: $MemoryThresholdMB MB" -ForegroundColor Gray
Write-Host ""

$killedCount = 0
$orphanedTestHosts = @()
$dllLockingFixed = $false

foreach ($processName in $processesToCheck) {
    $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

    if ($processes) {
        foreach ($proc in $processes) {
            $memoryMB = [math]::Round($proc.WorkingSet / 1MB, 2)

            # Check if process should be killed
            $shouldKill = $false
            $reason = ""
            $isProtected = $false

            # Get command line for analysis
            try {
                $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)").CommandLine

                # PROTECTION: Skip Claude Code, IDEs, build tools, and development tools
                if ($cmdLine -match "claude-code|code.exe|Visual Studio|Rider|WebStorm|cursor|cline|devenv|MSBuild") {
                    $isProtected = $true
                    if ($Verbose) {
                        Write-Host "[PROTECTED] $processName (PID: $($proc.Id)) - Development tool" -ForegroundColor Cyan
                    }
                }

                # PROTECTION: Skip current script's parent process chain
                $currentProcess = Get-Process -Id $PID -ErrorAction SilentlyContinue
                if ($currentProcess -and $proc.Id -eq $currentProcess.Id) {
                    $isProtected = $true
                }

                # Check if it's a test process (contains test-related command line)
                if (-not $isProtected -and $cmdLine -match "test|jest|xunit|nunit|testhost") {
                    # Special handling for testhost (DLL locking culprit)
                    if ($processName -eq "testhost") {
                        $runtime = (Get-Date) - $proc.StartTime

                        # Kill old testhost processes (>10 minutes)
                        if ($runtime.TotalMinutes -gt 10) {
                            $shouldKill = $true
                            $reason = "Orphaned testhost (running $([math]::Round($runtime.TotalMinutes, 1)) min) - potential DLL lock"
                            $orphanedTestHosts += $proc.Id
                            $dllLockingFixed = $true
                        }

                        # Kill testhost processes even if young (TestHostOnly mode)
                        if ($TestHostOnly -and $runtime.TotalMinutes -gt 1) {
                            $shouldKill = $true
                            $reason = "TestHost cleanup for DLL locking fix"
                            $dllLockingFixed = $true
                        }
                    }

                    # Check memory threshold only if not already marked and not in TestHostOnly mode
                    if (-not $shouldKill -and -not $TestHostOnly -and $memoryMB -gt $MemoryThresholdMB) {
                        $shouldKill = $true
                        $reason = "High memory: $memoryMB MB"
                    }
                }
            } catch {
                # If we can't get command line, mark as protected (safer approach)
                $isProtected = $true
                if ($Verbose) {
                    Write-Host "[PROTECTED] $processName (PID: $($proc.Id)) - Unable to analyze" -ForegroundColor Yellow
                }
            }

            if ($shouldKill) {
                $procInfo = "$processName (PID: $($proc.Id), Memory: $memoryMB MB)"

                if ($DryRun) {
                    Write-Host "[DRY RUN] Would kill: $procInfo - $reason" -ForegroundColor Yellow
                } else {
                    try {
                        Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                        Write-Host "[KILLED] $procInfo - $reason" -ForegroundColor Red
                        $killedCount++
                    } catch {
                        Write-Host "[ERROR] Failed to kill $procInfo : $_" -ForegroundColor Magenta
                    }
                }
            } elseif ($Verbose -and -not $isProtected) {
                Write-Host "[ACTIVE] $processName (PID: $($proc.Id), Memory: $memoryMB MB)" -ForegroundColor Green
            }
        }
    }
}

# Summary
Write-Host "`n=== Summary ===" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "Dry run completed. No processes were killed." -ForegroundColor Yellow
} else {
    Write-Host "Killed $killedCount hanging test processes." -ForegroundColor $(if ($killedCount -gt 0) { "Red" } else { "Green" })

    if ($orphanedTestHosts.Count -gt 0) {
        Write-Host "  - Including $($orphanedTestHosts.Count) orphaned testhost processes" -ForegroundColor Yellow
    }

    if ($dllLockingFixed) {
        Write-Host "  [OK] DLL locking issue should be resolved" -ForegroundColor Green
    }
}

Write-Host ""
