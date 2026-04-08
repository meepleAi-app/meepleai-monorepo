#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Wipes the local DB, deletes apps/api/src/Api/Migrations/, and creates a fresh Initial migration.
.DESCRIPTION
    Step 2 of the migration consolidation workflow. Requires a snapshot already
    created by db-save-state.ps1. All destructive actions are gated behind
    explicit confirmation. Refuses to run if dotnet build fails.
.PARAMETER SnapshotPath
    Required. Path to a snapshot directory created by db-save-state.ps1.
.PARAMETER InitialName
    Name of the new consolidated migration. Default: "Initial".
.PARAMETER ApiProjectPath
    Path to the API .csproj root. Default: apps/api/src/Api.
.PARAMETER Force
    Skip the confirmation prompt.
.PARAMETER DryRun
    Print intended commands without executing destructive actions.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SnapshotPath,
    [string]$InitialName = 'Initial',
    [string]$ApiProjectPath = (Join-Path $PSScriptRoot '..' '..' 'apps' 'api' 'src' 'Api'),
    [switch]$Force,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'db-snapshot-common.psm1') -Force

Write-Timestamped '=== db-reset-migrations ==='

# ============================================================================
# Pre-flight
# ============================================================================

# Check 1: snapshot is present and valid
if (-not (Test-Path -LiteralPath $SnapshotPath)) {
    throw "Snapshot path not found: $SnapshotPath"
}
$manifestPath = Join-Path $SnapshotPath 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "manifest.json not found in $SnapshotPath — is this a valid snapshot?"
}
$manifest = Read-Manifest -Path $manifestPath

# Check 1b: re-verify dump file hashes
foreach ($prop in $manifest.files.PSObject.Properties) {
    $f = $prop.Value
    $filePath = Join-Path $SnapshotPath $f.name
    if (-not (Test-Path -LiteralPath $filePath)) {
        throw "Snapshot file missing: $($f.name)"
    }
    $actual = Get-FileSha256 -Path $filePath
    if ($actual -ne $f.sha256) {
        throw "Snapshot file CORRUPTED: $($f.name) hash mismatch (expected $($f.sha256), got $actual)"
    }
}
Write-Timestamped "[1/7] Snapshot validated: $SnapshotPath"

# Check 2: localhost-only enforcement
$secretFile = Join-Path $PSScriptRoot '..' 'secrets' 'database.secret'
$cfg = Get-PostgresConfig -SecretPath $secretFile
Assert-LocalhostOnly -Config $cfg
Write-Timestamped '[2/7] Localhost host check passed'

# Check 3: DB drift since save (xact_commit delta, warning only)
try {
    $xactNow = Get-XactCommitCount -Config $cfg
    $delta = $xactNow - $manifest.xactCommitAtSave
    if ($delta -gt 10) {
        Write-Host "⚠ The DB has had $delta committed transactions since the snapshot was taken." -ForegroundColor Yellow
        Write-Host "   The snapshot may be stale. Consider creating a fresh snapshot first." -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠ Could not check xact_commit drift: $_" -ForegroundColor Yellow
}
Write-Timestamped '[3/7] DB drift check complete'

# Check 4: dotnet and dotnet ef present
foreach ($tool in @('dotnet')) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "$tool not found in PATH"
    }
}
$efCheck = & dotnet ef --version 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "dotnet ef tool not installed. Run: dotnet tool install --global dotnet-ef"
}
Write-Timestamped "[4/7] dotnet ef found: $($efCheck -join ' ')"

# Check 5: API project exists
if (-not (Test-Path -LiteralPath $ApiProjectPath)) {
    throw "API project path not found: $ApiProjectPath"
}
$migrationsFolder = Join-Path $ApiProjectPath 'Migrations'
if (-not (Test-Path -LiteralPath $migrationsFolder)) {
    throw "Migrations folder not found in $ApiProjectPath. Nothing to reset."
}
$migrationFiles = @(Get-ChildItem -LiteralPath $migrationsFolder -File -Recurse)
Write-Timestamped "[5/7] API project found, $($migrationFiles.Count) files in Migrations/"

# Check 6: dotnet build sanity check (HARD BLOCK if fails)
Write-Timestamped '[6/7] Running dotnet build sanity check...'
$buildOutput = & dotnet build $ApiProjectPath -c Debug --nologo --verbosity quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host '❌ dotnet build failed:' -ForegroundColor Red
    $buildOutput | ForEach-Object { Write-Host "    $_" }
    throw 'Refusing to drop the database while the project does not compile. Fix the build first.'
}
Write-Timestamped '    Build OK'

# Check 7: git status (warning only)
$gitDirty = & git -C $ApiProjectPath status --porcelain Migrations/ 2>$null
if ($gitDirty) {
    Write-Host "⚠ Migrations/ has uncommitted changes:" -ForegroundColor Yellow
    $gitDirty | ForEach-Object { Write-Host "    $_" }
    Write-Host "   These changes will be LOST. Consider committing first." -ForegroundColor Yellow
}
Write-Timestamped '[7/7] Pre-flight checks complete'

# ============================================================================
# Confirmation
# ============================================================================
$safetyNetSize = $manifest.files.safetyNet.bytes
$prompt = @"
This will:
  1. DROP database '$($cfg.Db)' on $($cfg.Host):$($cfg.Port)
  2. DELETE folder $migrationsFolder ($($migrationFiles.Count) files)
  3. CREATE new migration '$InitialName'
  4. APPLY it to a fresh database

Snapshot safety net: $($manifest.files.safetyNet.name) ($safetyNetSize bytes)

To rollback if anything goes wrong:
  pwsh db-restore-state.ps1 -SnapshotPath $SnapshotPath -UseSafetyNet
"@
if (-not (Confirm-UserAction -Prompt $prompt -Force:$Force)) {
    Write-Timestamped 'Aborted by user. Snapshot intact, DB intact.'
    exit 0
}

# Placeholder for next task
Write-Timestamped 'TODO: destructive actions (Task 15)'
