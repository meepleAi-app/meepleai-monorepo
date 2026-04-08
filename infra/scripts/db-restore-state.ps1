#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Restores data from a snapshot into the freshly-rebuilt schema.
.DESCRIPTION
    Step 3 of the migration consolidation workflow. Computes schema drift
    between the saved schema-pre.sql and the current schema-post.sql.
    If identical or minor, restores data.dump. If significant, STOPs unless
    -AllowDrift is set. Supports -UseSafetyNet for full rollback.
.PARAMETER SnapshotPath
    Required. Snapshot directory created by db-save-state.ps1.
.PARAMETER UseSafetyNet
    Use safety-net.dump (full restore, drops and recreates DB) instead of data.dump.
.PARAMETER RestorePdfVolume
    Restore the Docker PDF volume from pdf_uploads.tar.gz. Defaults to true if present.
.PARAMETER AllowDrift
    Proceed with restore even if schema drift is classified as significant.
.PARAMETER Force
    Skip confirmation prompts.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SnapshotPath,
    [switch]$UseSafetyNet,
    [Nullable[bool]]$RestorePdfVolume = $null,
    [switch]$AllowDrift,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'db-snapshot-common.psm1') -Force

Write-Timestamped '=== db-restore-state ==='

# ============================================================================
# Pre-flight
# ============================================================================
if (-not (Test-Path -LiteralPath $SnapshotPath)) {
    throw "Snapshot path not found: $SnapshotPath"
}
$manifestPath = Join-Path $SnapshotPath 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "manifest.json not found in $SnapshotPath"
}
$manifest = Read-Manifest -Path $manifestPath

# Re-verify dump file hashes (Nygard F3)
foreach ($prop in $manifest.files.PSObject.Properties) {
    $f = $prop.Value
    $filePath = Join-Path $SnapshotPath $f.name
    if (-not (Test-Path -LiteralPath $filePath)) {
        throw "Snapshot file missing: $($f.name)"
    }
    $actual = Get-FileSha256 -Path $filePath
    if ($actual -ne $f.sha256) {
        throw "Snapshot file CORRUPTED: $($f.name)"
    }
}
Write-Timestamped '[1/9] Snapshot validated'

# Localhost-only enforcement
$secretFile = Join-Path $PSScriptRoot '..' 'secrets' 'database.secret'
$cfg = Get-PostgresConfig -SecretPath $secretFile
Assert-LocalhostOnly -Config $cfg
Write-Timestamped '[2/9] Localhost host check passed'

# Resolve RestorePdfVolume default
if ($null -eq $RestorePdfVolume) {
    $RestorePdfVolume = ($manifest.pdfVolumeIncluded -eq $true)
}

# Branch on mode
if ($UseSafetyNet) {
    Write-Timestamped '[3/9] Mode: SAFETY-NET ROLLBACK'
    Write-Timestamped 'TODO: safety-net mode (Task 18)'
    exit 0
}

Write-Timestamped '[3/9] Mode: standard data restore'

# Standard mode requires the new schema in place
$schemaPostPath = Join-Path $SnapshotPath 'schema-post.sql'
Write-Timestamped '[4/9] Re-dumping fresh schema-post.sql...'
Invoke-PgDump -Config $cfg -Arguments @('-s', '--no-owner', '--no-acl', '-f', $schemaPostPath)

# Read both schemas, normalize
$schemaPrePath = Join-Path $SnapshotPath 'schema-pre.sql'
$preSql = Get-Content -LiteralPath $schemaPrePath -Raw
$postSql = Get-Content -LiteralPath $schemaPostPath -Raw
$preNormalized = Normalize-PgSchema -Sql $preSql
$postNormalized = Normalize-PgSchema -Sql $postSql
Set-Content -LiteralPath (Join-Path $SnapshotPath 'schema-pre.normalized.sql') -Value $preNormalized -Encoding UTF8
Set-Content -LiteralPath (Join-Path $SnapshotPath 'schema-post.normalized.sql') -Value $postNormalized -Encoding UTF8
Write-Timestamped '[5/9] Schema normalized'

# Classify drift
$driftClass = Test-SchemaDriftClass -PreSchema $preNormalized -PostSchema $postNormalized
Write-Timestamped "[6/9] Drift classification: $driftClass"

# Compute and save diff
$diffPath = Join-Path $SnapshotPath 'schema-diff.txt'
& git diff --no-index --no-color (Join-Path $SnapshotPath 'schema-pre.normalized.sql') (Join-Path $SnapshotPath 'schema-post.normalized.sql') 2>$null | Out-File -LiteralPath $diffPath -Encoding UTF8

if ($driftClass -eq 'significant' -and -not $AllowDrift) {
    Write-Host '' -ForegroundColor Red
    Write-Host "❌ Schema drift detected: classification = significant" -ForegroundColor Red
    Write-Host "" -ForegroundColor Red
    Write-Host "Diff saved to: $diffPath"
    Write-Host ""
    Write-Host "First 30 lines of diff:"
    Get-Content -LiteralPath $diffPath -TotalCount 30 | ForEach-Object { Write-Host "    $_" }
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  a) Fix your model code and re-run:"
    Write-Host "     pwsh db-reset-migrations.ps1 -SnapshotPath $SnapshotPath"
    Write-Host "  b) Rollback to safety net:"
    Write-Host "     pwsh db-restore-state.ps1 -SnapshotPath $SnapshotPath -UseSafetyNet"
    Write-Host "  c) Force restore (will likely fail on missing/renamed columns):"
    Write-Host "     re-run with -AllowDrift"
    Write-Host ""
    Write-Timestamped 'Aborting. DB unchanged (fresh schema, empty).'
    exit 2
}

if ($driftClass -eq 'minor') {
    Write-Host "⚠ Minor schema drift detected (whitespace/cosmetic). Proceeding." -ForegroundColor Yellow
}

# Placeholder for next task
Write-Timestamped 'TODO: data restore + sequences (Task 17)'
