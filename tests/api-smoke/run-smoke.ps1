#!/usr/bin/env pwsh
<#
.SYNOPSIS
    meepleai-api-smoke runner (Windows / cross-platform PowerShell 7+).

.DESCRIPTION
    Invokes Bruno CLI against the bruno-collection/ directory with the chosen env.
    Prints a JSON summary + table.

.PARAMETER Env
    Environment name (local|staging). REQUIRED.

.PARAMETER Collection
    Run only one sub-collection (private-game|kb|agents|sessions). Optional.

.PARAMETER Help
    Show usage and exit.

.EXAMPLE
    .\run-smoke.ps1 -Env local

.EXAMPLE
    .\run-smoke.ps1 -Env staging -Collection private-game

.NOTES
    Exit codes:
      0  all scenarios passed
      1  one or more scenarios failed
      2  invalid arguments / Bruno CLI not found
#>
[CmdletBinding(DefaultParameterSetName = 'Run')]
param(
    [Parameter(ParameterSetName = 'Run', Mandatory = $false)]
    [string]$Env,

    [Parameter(ParameterSetName = 'Run')]
    [string]$Collection,

    [Parameter(ParameterSetName = 'Help')]
    [switch]$Help
)

$ErrorActionPreference = 'Stop'

function Show-Usage {
    @"
Usage: run-smoke.ps1 [OPTIONS]

Run meepleai API smoke tests via Bruno CLI.

Options:
  -Env <name>          Environment name (local|staging). REQUIRED.
  -Collection <name>   Run only one sub-collection
                       (private-game|kb|agents|sessions).
  -Help                Show this help and exit.

Examples:
  .\run-smoke.ps1 -Env local
  .\run-smoke.ps1 -Env staging -Collection private-game

Exit codes:
  0  all scenarios passed
  1  one or more scenarios failed
  2  invalid arguments / Bruno CLI not found
"@ | Write-Output
}

if ($Help) {
    Show-Usage
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Env)) {
    [Console]::Error.WriteLine("Error: -Env is required")
    exit 2
}

$ScriptDir = $PSScriptRoot
$CollectionRoot = Join-Path $ScriptDir 'bruno-collection'
$BrunoVersionFile = Join-Path $ScriptDir '.bruno-version'
$BrunoVersion = if (Test-Path $BrunoVersionFile) {
    (Get-Content $BrunoVersionFile -Raw).Trim()
} else {
    '2.15.1'
}

$EnvFile = Join-Path $CollectionRoot "environments" "$Env.bru"
if (-not (Test-Path $EnvFile)) {
    [Console]::Error.WriteLine("Error: env file not found: $EnvFile")
    exit 2
}

$Target = $CollectionRoot
if (-not [string]::IsNullOrWhiteSpace($Collection)) {
    $Target = Join-Path $CollectionRoot $Collection
    if (-not (Test-Path $Target)) {
        [Console]::Error.WriteLine("Error: sub-collection not found: $Target")
        exit 2
    }
}

Write-Host "Running Bruno (v$BrunoVersion) — env=$Env, target=$($Target.Replace($ScriptDir + [IO.Path]::DirectorySeparatorChar, ''))"
& npx -y "@usebruno/cli@$BrunoVersion" run --env $Env $Target
exit $LASTEXITCODE
