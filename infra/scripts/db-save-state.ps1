#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Saves the local PostgreSQL state as a timestamped snapshot before a migration consolidation.
.DESCRIPTION
    Creates infra/db-snapshots/<timestamp>/ containing safety-net.dump (full),
    data.dump (data-only), schema-pre.sql, rowcounts-pre.tsv, optional pdf_uploads.tar.gz,
    and manifest.json. Refuses to run against non-localhost hosts.
.PARAMETER Sanitize
    Exclude sensitive tables (Users, Sessions, RefreshTokens, ApiKeys, AuditLogs)
    from data.dump only. The safety-net.dump always contains all data.
.PARAMETER IncludePdfVolume
    Include the meepleai_pdf_uploads Docker volume. Defaults to true if local storage
    is detected, false if S3 storage.
.PARAMETER SnapshotRoot
    Directory under which the timestamped snapshot is created.
.EXAMPLE
    pwsh infra/scripts/db-save-state.ps1
.EXAMPLE
    pwsh infra/scripts/db-save-state.ps1 -Sanitize
#>
[CmdletBinding()]
param(
    [switch]$Sanitize,
    [Nullable[bool]]$IncludePdfVolume = $null,
    [string]$SnapshotRoot = (Join-Path $PSScriptRoot '..' 'db-snapshots'),
    [string]$SecretFile = (Join-Path $PSScriptRoot '..' 'secrets' 'database.secret'),
    [string]$StorageSecretFile = (Join-Path $PSScriptRoot '..' 'secrets' 'storage.secret'),
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'db-snapshot-common.psm1') -Force

$SENSITIVE_TABLES = @(
    'Users', 'Sessions', 'RefreshTokens', 'ApiKeys', 'AuditLogs'
)

# ============================================================================
# Pre-flight
# ============================================================================

Write-Timestamped '=== db-save-state ==='

# Check 1: pg_dump and psql available
foreach ($tool in @('pg_dump', 'psql')) {
    $cmd = Get-Command $tool -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "Required tool not found in PATH: $tool"
    }
}
Write-Timestamped '[1/9] pg_dump and psql found'

# Check 2-3: secret file + load config
if (-not (Test-Path -LiteralPath $SecretFile)) {
    throw "Secret file not found: $SecretFile"
}
$cfg = Get-PostgresConfig -SecretPath $SecretFile
Write-Timestamped "[2/9] Loaded config for db '$($cfg.Db)' on $($cfg.Host):$($cfg.Port)"

# Check 4: localhost-only enforcement
Assert-LocalhostOnly -Config $cfg
Write-Timestamped '[3/9] Localhost host check passed'

# Check 5: PostgreSQL reachable
$null = Invoke-Psql -Config $cfg -Sql 'SELECT 1;'
Write-Timestamped '[4/9] PostgreSQL reachable'

# Check 6: SnapshotRoot writable
if (-not (Test-Path -LiteralPath $SnapshotRoot)) {
    New-Item -ItemType Directory -Path $SnapshotRoot -Force | Out-Null
}
Write-Timestamped "[5/9] Snapshot root writable: $SnapshotRoot"

# Check 7: Storage provider detection + PDF volume probe
$storageProvider = Get-StorageProvider -StorageSecretPath $StorageSecretFile
$pdfVolumeStats = $null
if ($storageProvider -eq 'local') {
    $pdfVolumeStats = Get-DockerVolumeStats -VolumeName 'meepleai_pdf_uploads'
    Write-Timestamped "[6/9] Storage=local, PDF volume: exists=$($pdfVolumeStats.Exists), files=$($pdfVolumeStats.FileCount), size=$($pdfVolumeStats.SizeBytes) bytes"
} else {
    Write-Timestamped "[6/9] Storage=$storageProvider (PDFs on remote, not affected)"
}

# Resolve IncludePdfVolume default
if ($null -eq $IncludePdfVolume) {
    $IncludePdfVolume = ($storageProvider -eq 'local' -and $pdfVolumeStats -and $pdfVolumeStats.Exists -and $pdfVolumeStats.FileCount -gt 0)
}

# Check 8: Disk space
$dbSize = Get-DbSizeBytes -Config $cfg
$volSize = if ($IncludePdfVolume -and $pdfVolumeStats) { $pdfVolumeStats.SizeBytes } else { 0L }
$requiredBytes = Get-RequiredDiskSpaceBytes -DbSizeBytes $dbSize -VolumeSizeBytes $volSize
$drive = (Get-Item $SnapshotRoot).PSDrive
$freeBytes = $drive.Free
if ($freeBytes -lt $requiredBytes) {
    throw "Not enough free disk space on $($drive.Name): need $requiredBytes bytes, have $freeBytes bytes"
}
Write-Timestamped "[7/9] Disk space OK (need $requiredBytes bytes, have $freeBytes bytes)"

# Check 9: Active backend connections
$activeConns = Get-ActiveBackendConnections -Config $cfg
if ($activeConns.Count -gt 0) {
    Write-Host ''
    Write-Host '⚠ Active backend connections detected:' -ForegroundColor Yellow
    foreach ($c in $activeConns) { Write-Host "    $c" }
    Write-Host ''
    if (-not (Confirm-UserAction -Prompt "Concurrent writes during pg_dump can produce inconsistent business state. Stop the API container first, or continue at your own risk." -Force:$Force)) {
        throw 'Aborted by user'
    }
}
Write-Timestamped '[8/9] Backend connection check complete'

# All checks passed
Write-Timestamped '[9/9] Pre-flight checks complete'

# Placeholder for next task
Write-Timestamped 'TODO: dump operations (Task 12)'
