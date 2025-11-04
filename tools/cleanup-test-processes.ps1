# Test Process Cleanup Script
# Automatically kills hanging test processes to free RAM

param(
    [int]$MemoryThresholdMB = 100,
    [switch]$DryRun = $false,
    [switch]$Verbose = $false
)

Write-Host "=== Test Process Cleanup ===" -ForegroundColor Cyan
Write-Host "Memory threshold: $MemoryThresholdMB MB" -ForegroundColor Gray

$processesToCheck = @('node', 'dotnet', 'jest', 'testhost', 'VBCSCompiler')
$killedCount = 0
$orphanedTestHosts = @()

foreach ($processName in $processesToCheck) {
    $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

    if ($processes) {
        foreach ($proc in $processes) {
            $memoryMB = [math]::Round($proc.WorkingSet / 1MB, 2)

            # Check if process is hanging (high memory or long runtime)
            $shouldKill = $false
            $reason = ""
            $isProtected = $false

            # Get command line for analysis
            try {
                $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)").CommandLine

                # PROTECTION: Skip Claude Code, IDEs, and development tools
                if ($cmdLine -match "claude-code|code.exe|Visual Studio|Rider|WebStorm|cursor|cline") {
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
                    if ($proc.CPU -gt 0 -and $proc.Responding -eq $false) {
                        $shouldKill = $true
                        $reason = "Not responding (test process)"
                    }
                    # Also kill orphaned testhost processes (no parent or old)
                    if ($processName -eq "testhost") {
                        $runtime = (Get-Date) - $proc.StartTime
                        if ($runtime.TotalMinutes -gt 10) {
                            $shouldKill = $true
                            $reason = "Orphaned testhost (running $([math]::Round($runtime.TotalMinutes, 1)) min)"
                            $orphanedTestHosts += $proc.Id
                        }
                    }
                }
            } catch {
                # If we can't get command line, mark as protected (safer approach)
                $isProtected = $true
                if ($Verbose) {
                    Write-Host "[PROTECTED] $processName (PID: $($proc.Id)) - Unable to analyze" -ForegroundColor Yellow
                }
            }

            # Only check memory threshold if not protected
            if (-not $isProtected -and $memoryMB -gt $MemoryThresholdMB) {
                $shouldKill = $true
                $reason = "High memory: $memoryMB MB"
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

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "Dry run completed. No processes were killed." -ForegroundColor Yellow
} else {
    Write-Host "Killed $killedCount hanging test processes." -ForegroundColor $(if ($killedCount -gt 0) { "Red" } else { "Green" })
    if ($orphanedTestHosts.Count -gt 0) {
        Write-Host "  - Including $($orphanedTestHosts.Count) orphaned testhost processes" -ForegroundColor Yellow
    }
}

# Show current memory usage
$totalMemoryGB = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 2)
$availableMemoryGB = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 2)
$usedMemoryGB = [math]::Round($totalMemoryGB - $availableMemoryGB, 2)
$usagePercent = [math]::Round(($usedMemoryGB / $totalMemoryGB) * 100, 1)

Write-Host "`nMemory: $usedMemoryGB GB / $totalMemoryGB GB ($usagePercent% used)" -ForegroundColor $(
    if ($usagePercent -gt 90) { "Red" }
    elseif ($usagePercent -gt 75) { "Yellow" }
    else { "Green" }
)
