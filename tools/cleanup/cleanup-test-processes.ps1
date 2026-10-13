<#
.SYNOPSIS
    Cleanup stale test processes that lock DLL files during dotnet test

.DESCRIPTION
    Kills hanging test processes (testhost, VBCSCompiler, node, dotnet) of THIS checkout
    to prevent MSB3027 errors (DLL file locking) during dotnet build/test.

    Only processes whose command line runs from this checkout are touched (#4310): on a machine
    where several checkouts (git worktrees) work in parallel, a test process of another checkout
    is somebody else's test run, not an orphan. Use -AllCheckouts to clean the whole machine.

    Includes safety protections for IDEs and development tools.

.PARAMETER TestHostOnly
    Only kill testhost processes (focused cleanup for DLL locking issues)

.PARAMETER MemoryThresholdMB
    Memory threshold in MB for process cleanup (default: 100)

.PARAMETER RepoRoot
    Root of the checkout whose test processes may be killed.
    Default: the checkout this script belongs to.

.PARAMETER AllCheckouts
    Kill matching test processes of every checkout on the machine. This aborts the test runs of
    anyone else working on it: use it only when nothing else is running.

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

.EXAMPLE
    # Whole machine, when no other checkout is running tests
    .\cleanup-test-processes.ps1 -TestHostOnly -AllCheckouts

.NOTES
    Author: MeepleAI Team
    Issue: #2210 - Automated cleanup for stale testhost processes
    Issue: #4310 - Scoped to the checkout doing the cleanup
    Platform: Windows only (Unix-like systems don't experience DLL locking)
    The rules live in TestProcessCleanup.psm1 and are tested in
    infra/scripts/tests/cleanup-test-processes.Tests.ps1.
#>

param(
    [switch]$TestHostOnly = $false,
    [int]$MemoryThresholdMB = 100,
    [string]$RepoRoot = '',
    [switch]$AllCheckouts = $false,
    [switch]$DryRun = $false,
    [switch]$Verbose = $false
)

Import-Module (Join-Path $PSScriptRoot 'TestProcessCleanup.psm1') -Force

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

# The other checkouts of this repository. One of them can sit inside this one
# (.claude/worktrees/...): its processes are its own. Without git, there are none to exclude.
$otherRoots = @()
if (-not $AllCheckouts) {
    try {
        $worktreeLines = & git -C $RepoRoot worktree list --porcelain 2>$null
        if ($LASTEXITCODE -eq 0 -and $worktreeLines) {
            $thisRoot = ConvertTo-ComparablePath $RepoRoot
            $otherRoots = @(ConvertFrom-WorktreeList -Lines $worktreeLines |
                Where-Object { (ConvertTo-ComparablePath $_) -ne $thisRoot })
        }
    } catch {
        $otherRoots = @()
    }
}

Write-Host "=== Test Process Cleanup ===" -ForegroundColor Cyan

# Define processes to check
$processesToCheck = if ($TestHostOnly) {
    @('testhost')
    Write-Host "Mode: TestHost Only (DLL locking fix)" -ForegroundColor Yellow
} else {
    @('node', 'dotnet', 'testhost', 'VBCSCompiler')
    Write-Host "Mode: Full Cleanup" -ForegroundColor Gray
}

if ($AllCheckouts) {
    Write-Host "Scope: ALL checkouts on this machine" -ForegroundColor Red
} else {
    Write-Host "Scope: this checkout only ($RepoRoot)" -ForegroundColor Gray
}

Write-Host "Memory threshold: $MemoryThresholdMB MB" -ForegroundColor Gray
Write-Host ""

$killedCount = 0
$otherCheckoutCount = 0
$orphanedTestHosts = @()
$dllLockingFixed = $false

foreach ($processName in $processesToCheck) {
    $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

    if ($processes) {
        foreach ($proc in $processes) {
            $memoryMB = [math]::Round($proc.WorkingSet / 1MB, 2)

            # PROTECTION: never the process running this script
            if ($proc.Id -eq $PID) {
                continue
            }

            # A command line that cannot be read makes the process Protected (safer approach)
            $cmdLine = $null
            $runtimeMinutes = 0
            try {
                $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)").CommandLine
                $runtimeMinutes = ((Get-Date) - $proc.StartTime).TotalMinutes
            } catch {
                $cmdLine = $null
            }

            $decision = Get-TestProcessDecision -ProcessName $processName -CommandLine $cmdLine `
                -RuntimeMinutes $runtimeMinutes -MemoryMB $memoryMB -RepoRoot $RepoRoot -OtherRoots $otherRoots `
                -TestHostOnly:$TestHostOnly -AllCheckouts:$AllCheckouts -MemoryThresholdMB $MemoryThresholdMB

            $procInfo = "$processName (PID: $($proc.Id), Memory: $memoryMB MB)"

            switch ($decision.Action) {
                'Kill' {
                    if ($processName -eq 'testhost') {
                        $dllLockingFixed = $true
                        if ($runtimeMinutes -gt 10) {
                            $orphanedTestHosts += $proc.Id
                        }
                    }

                    if ($DryRun) {
                        Write-Host "[DRY RUN] Would kill: $procInfo - $($decision.Reason)" -ForegroundColor Yellow
                    } else {
                        try {
                            Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                            Write-Host "[KILLED] $procInfo - $($decision.Reason)" -ForegroundColor Red
                            $killedCount++
                        } catch {
                            Write-Host "[ERROR] Failed to kill $procInfo : $_" -ForegroundColor Magenta
                        }
                    }
                }
                'OtherCheckout' {
                    $otherCheckoutCount++
                    if ($Verbose -or $DryRun) {
                        Write-Host "[LEFT ALONE] $procInfo - another checkout's test run" -ForegroundColor Cyan
                    }
                }
                'Protected' {
                    if ($Verbose) {
                        Write-Host "[PROTECTED] $processName (PID: $($proc.Id)) - $($decision.Reason)" -ForegroundColor Cyan
                    }
                }
                'Active' {
                    if ($Verbose) {
                        Write-Host "[ACTIVE] $procInfo" -ForegroundColor Green
                    }
                }
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

if ($otherCheckoutCount -gt 0) {
    Write-Host "Left $otherCheckoutCount test processes of other checkouts alone." -ForegroundColor Cyan
}

Write-Host ""
