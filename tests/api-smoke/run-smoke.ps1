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
      1  one or more scenarios failed, or Bruno ran 0 requests
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

Environment:
  SMOKE_AARON_PASSWORD  Password of smoke-aaron. With -Env local it defaults to the one
                        of tests/fixtures/smoke-test-users.sql.
  SMOKE_API_BASE        Overrides apiBase of the env file (e.g. an API on another port).

Examples:
  .\run-smoke.ps1 -Env local
  .\run-smoke.ps1 -Env staging -Collection private-game

Exit codes:
  0  all scenarios passed
  1  one or more scenarios failed, or Bruno ran 0 requests
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

# #4978: Bruno runs only from the root of a collection (the folder with bruno.json), and takes the
# sub-collection as a path relative to it. Passing an absolute path from elsewhere failed with
# "You can run only at the root of a collection".
$Target = '.'
if (-not [string]::IsNullOrWhiteSpace($Collection)) {
    $Target = $Collection
    $TargetPath = Join-Path $CollectionRoot $Collection
    if (-not (Test-Path $TargetPath)) {
        [Console]::Error.WriteLine("Error: sub-collection not found: $TargetPath")
        exit 2
    }
}

# The local env reads the password from the environment, like staging; this is the fixture's.
if ($Env -eq 'local' -and [string]::IsNullOrEmpty($env:SMOKE_AARON_PASSWORD)) {
    $env:SMOKE_AARON_PASSWORD = 'SmokeAaron1!!'
}

# #4978: -r, because without it Bruno runs only the requests directly inside the target folder: the
# collection root has none, and a run of the whole collection executed 0 requests and reported PASS.
# The JSON report, relative to the collection root, is read below to refuse such a run.
$ReportRel = '../reports/bruno-report.json'
$ReportPath = Join-Path $ScriptDir 'reports' 'bruno-report.json'
New-Item -ItemType Directory -Force (Join-Path $ScriptDir 'reports') | Out-Null
Remove-Item -Force -ErrorAction SilentlyContinue $ReportPath

$BruArgs = @('run', '-r', '--env', $Env, '--reporter-json', $ReportRel)
if (-not [string]::IsNullOrEmpty($env:SMOKE_API_BASE)) {
    $BruArgs += @('--env-var', "apiBase=$($env:SMOKE_API_BASE)")
}

Write-Host "Running Bruno (v$BrunoVersion) — env=$Env, target=bruno-collection/$($Target.TrimStart('.', '/'))"
Push-Location $CollectionRoot
try {
    & npx -y "@usebruno/cli@$BrunoVersion" @BruArgs $Target
    $BruExit = $LASTEXITCODE
} finally {
    Pop-Location
}
$TotalRequests = 0
if (Test-Path $ReportPath) {
    $Report = Get-Content $ReportPath -Raw | ConvertFrom-Json
    $TotalRequests = ($Report | ForEach-Object { $_.summary.totalRequests } | Measure-Object -Sum).Sum
}
Write-Host "Bruno: exit $BruExit, $TotalRequests request(s) run (report: tests/api-smoke/reports/bruno-report.json)"
if ($BruExit -eq 0 -and $TotalRequests -eq 0) {
    [Console]::Error.WriteLine("Error: Bruno ran 0 requests against bruno-collection/$($Target.TrimStart('.', '/')): nothing was tested")
    exit 1
}
exit $BruExit
