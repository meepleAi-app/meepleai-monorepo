#!/usr/bin/env pwsh
# Contract test for run-smoke.ps1.
# Verifica che -Help: (a) exit 0, (b) stampa "Usage:" line, (c) menziona -Env.
# Verifica anche negative paths: missing -Env -> exit 2.

$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $PSScriptRoot
$RunSmoke = Join-Path $ScriptDir 'run-smoke.ps1'

if (-not (Test-Path $RunSmoke)) {
    Write-Host "FAIL: $RunSmoke does not exist"
    exit 1
}

# Test 1: -Help -> exit 0 + "Usage:" + "-Env"
$Output = & pwsh -NoProfile -File $RunSmoke -Help 2>&1
$ExitCode = $LASTEXITCODE

if ($ExitCode -ne 0) {
    Write-Host "FAIL: -Help exited with $ExitCode (expected 0)"
    Write-Host ($Output -join "`n")
    exit 1
}

$OutputText = $Output -join "`n"

if ($OutputText -notmatch 'Usage:') {
    Write-Host "FAIL: -Help output missing 'Usage:'"
    Write-Host $OutputText
    exit 1
}

if ($OutputText -notmatch '-Env') {
    Write-Host "FAIL: -Help output missing '-Env' flag"
    Write-Host $OutputText
    exit 1
}

Write-Host "PASS: run-smoke.ps1 -Help contract"

# Test 2: Missing -Env -> exit 2 + error message
$NegOutput = & pwsh -NoProfile -File $RunSmoke 2>&1
$NegExit = $LASTEXITCODE

if ($NegExit -ne 2) {
    Write-Host "FAIL: missing -Env expected exit 2, got $NegExit"
    Write-Host ($NegOutput -join "`n")
    exit 1
}

# Test 3: -Env with non-existent env file -> exit 2 + error
$NegOutput2 = & pwsh -NoProfile -File $RunSmoke -Env nonexistent_env_xyz 2>&1
$NegExit2 = $LASTEXITCODE

if ($NegExit2 -ne 2) {
    Write-Host "FAIL: nonexistent env expected exit 2, got $NegExit2"
    Write-Host ($NegOutput2 -join "`n")
    exit 1
}

$NegOutputText = $NegOutput2 -join "`n"
if ($NegOutputText -notmatch 'env file not found') {
    Write-Host "FAIL: nonexistent env missing 'env file not found' in output"
    Write-Host $NegOutputText
    exit 1
}

Write-Host "PASS: run-smoke.ps1 negative-path contracts"
exit 0
