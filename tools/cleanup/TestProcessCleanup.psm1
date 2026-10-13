# tools/cleanup/TestProcessCleanup.psm1
# Pure rules of cleanup-test-processes.ps1: which test process may be killed, and whose it is.
#
# Issue #4310. The script used to kill every testhost on the machine. With several checkouts
# (git worktrees) working in parallel, the cleanup of one checkout - run by its pre-push hook -
# aborted the test runs of all the others. A process is now killed only when its command line
# shows that it runs from the checkout doing the cleanup.
#
# Everything here is a pure function over strings and numbers, so the rules are tested on any
# platform (infra/scripts/tests/cleanup-test-processes.Tests.ps1). Keep this file ASCII-only and
# free of PowerShell 7 syntax: the pre-push hook loads it with Windows PowerShell 5.1.

# Editors, IDEs and build tools: never killed, whatever checkout they come from.
$script:ProtectedTools = 'claude-code|code\.exe|Visual Studio|Rider|WebStorm|cursor|cline|devenv|MSBuild'

# What makes a command line a test process.
$script:TestProcessMarkers = 'test|jest|xunit|nunit|testhost'

function ConvertTo-ComparablePath {
    <#
    .SYNOPSIS
        Lower-cased path with forward slashes and no trailing slash, so two spellings of the same
        Windows path compare equal. Accepts the /d/dir form that Git Bash may print.
    #>
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ''
    }

    $normalized = $Path.Trim().Replace('\', '/').ToLowerInvariant()
    if ($normalized -match '^/([a-z])/(.*)$') {
        $normalized = "$($Matches[1]):/$($Matches[2])"
    }

    return $normalized.TrimEnd('/')
}

function Test-CommandLineBelongsToRepo {
    <#
    .SYNOPSIS
        True when the command line names a path under RepoRoot, and not under another checkout
        nested inside it.
    .DESCRIPTION
        The root must be followed by a separator: wt-4244 does not own the processes of wt-4244b.
        OtherRoots are the other checkouts of the repository; one of them can sit inside RepoRoot
        (for instance .claude/worktrees/...), and its processes are its own.
    #>
    param(
        [string]$CommandLine,
        [string]$RepoRoot,
        [string[]]$OtherRoots = @()
    )

    $root = ConvertTo-ComparablePath $RepoRoot
    if ([string]::IsNullOrWhiteSpace($CommandLine) -or $root -eq '') {
        return $false
    }

    $line = $CommandLine.Replace('\', '/').ToLowerInvariant()
    if (-not $line.Contains("$root/")) {
        return $false
    }

    foreach ($other in $OtherRoots) {
        $otherRoot = ConvertTo-ComparablePath $other
        if ($otherRoot.StartsWith("$root/") -and $line.Contains("$otherRoot/")) {
            return $false
        }
    }

    return $true
}

function Get-TestProcessDecision {
    <#
    .SYNOPSIS
        What to do with one process: Kill, Active, OtherCheckout, Protected or NotATestProcess.
    .DESCRIPTION
        OtherCheckout is the #4310 rule: a test process of another checkout is somebody else's
        test run, not an orphan. AllCheckouts restores the old machine-wide behaviour for the
        rare case in which that is really wanted.
    #>
    param(
        [string]$ProcessName,
        [string]$CommandLine,
        [double]$RuntimeMinutes,
        [double]$MemoryMB,
        [string]$RepoRoot,
        [string[]]$OtherRoots = @(),
        [switch]$TestHostOnly,
        [switch]$AllCheckouts,
        [int]$MemoryThresholdMB = 100
    )

    if ([string]::IsNullOrWhiteSpace($CommandLine)) {
        return [pscustomobject]@{ Action = 'Protected'; Reason = 'Unable to analyze' }
    }

    if ($CommandLine -match $script:ProtectedTools) {
        return [pscustomobject]@{ Action = 'Protected'; Reason = 'Development tool' }
    }

    if ($CommandLine -notmatch $script:TestProcessMarkers) {
        return [pscustomobject]@{ Action = 'NotATestProcess'; Reason = '' }
    }

    if (-not $AllCheckouts -and -not (Test-CommandLineBelongsToRepo -CommandLine $CommandLine -RepoRoot $RepoRoot -OtherRoots $OtherRoots)) {
        return [pscustomobject]@{ Action = 'OtherCheckout'; Reason = 'Not started from this checkout' }
    }

    if ($ProcessName -eq 'testhost') {
        if ($RuntimeMinutes -gt 10) {
            $minutes = [math]::Round($RuntimeMinutes, 1)
            return [pscustomobject]@{ Action = 'Kill'; Reason = "Orphaned testhost (running $minutes min) - potential DLL lock" }
        }

        if ($TestHostOnly -and $RuntimeMinutes -gt 1) {
            return [pscustomobject]@{ Action = 'Kill'; Reason = 'TestHost cleanup for DLL locking fix' }
        }
    }

    if (-not $TestHostOnly -and $MemoryMB -gt $MemoryThresholdMB) {
        return [pscustomobject]@{ Action = 'Kill'; Reason = "High memory: $MemoryMB MB" }
    }

    return [pscustomobject]@{ Action = 'Active'; Reason = '' }
}

function ConvertFrom-WorktreeList {
    <#
    .SYNOPSIS
        The checkout roots in the output of "git worktree list --porcelain".
    #>
    param([string[]]$Lines = @())

    foreach ($line in $Lines) {
        if ($line -match '^worktree (.+)$') {
            $Matches[1].Trim()
        }
    }
}

Export-ModuleMember -Function ConvertTo-ComparablePath, Test-CommandLineBelongsToRepo, Get-TestProcessDecision, ConvertFrom-WorktreeList
