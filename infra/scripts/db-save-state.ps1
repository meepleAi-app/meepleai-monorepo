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

# ============================================================================
# Snapshot directory
# ============================================================================
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$snap = Join-Path $SnapshotRoot $timestamp
New-Item -ItemType Directory -Path $snap -Force | Out-Null
$logFile = Join-Path $snap 'save.log'
Write-Timestamped "Created snapshot dir: $snap" -LogFile $logFile

# ============================================================================
# Step 1: Safety-net dump (full schema + data, ALWAYS complete)
# ============================================================================
$safetyNetPath = Join-Path $snap 'safety-net.dump'
Write-Timestamped '[1/6] Safety-net dump (full schema + data)...' -LogFile $logFile
try {
    Invoke-PgDump -Config $cfg -Arguments @(
        '-Fc', '--no-owner', '--no-acl',
        '-f', $safetyNetPath
    )
} catch {
    # Safety-net failure is fatal
    Remove-Item $snap -Recurse -Force -ErrorAction SilentlyContinue
    throw "Safety-net dump failed (FATAL): $_"
}
$safetyNetSize = (Get-Item $safetyNetPath).Length
Write-Timestamped "    safety-net.dump: $safetyNetSize bytes" -LogFile $logFile

# ============================================================================
# Step 2: Schema-pre dump
# ============================================================================
$schemaPrePath = Join-Path $snap 'schema-pre.sql'
Write-Timestamped '[2/6] Schema-pre dump...' -LogFile $logFile
Invoke-PgDump -Config $cfg -Arguments @(
    '-s', '--no-owner', '--no-acl',
    '-f', $schemaPrePath
)

# ============================================================================
# Step 3: Data-only dump
# ============================================================================
$dataDumpPath = Join-Path $snap 'data.dump'
Write-Timestamped '[3/6] Data-only dump...' -LogFile $logFile
$dataArgs = @(
    '-Fc', '--data-only', '--no-owner', '--no-acl',
    '--exclude-table', '*__EFMigrationsHistory*'
)
$excludedTables = @()
if ($Sanitize) {
    foreach ($table in $SENSITIVE_TABLES) {
        $dataArgs += @('--exclude-table-data', "*.$table")
        $excludedTables += $table
    }
    Write-Timestamped "    Sanitize: excluding data from $($SENSITIVE_TABLES -join ', ')" -LogFile $logFile
}
$dataArgs += @('-f', $dataDumpPath)
Invoke-PgDump -Config $cfg -Arguments $dataArgs
$dataDumpSize = (Get-Item $dataDumpPath).Length
Write-Timestamped "    data.dump: $dataDumpSize bytes" -LogFile $logFile

# ============================================================================
# Step 4: Row counts (exact COUNT(*), not n_live_tup)
# ============================================================================
$rowCountsPath = Join-Path $snap 'rowcounts-pre.tsv'
Write-Timestamped '[4/6] Row counts (exact)...' -LogFile $logFile

$tableListSql = @"
SELECT table_schema || '.' || table_name
FROM information_schema.tables
WHERE table_type = 'BASE TABLE'
  AND table_schema NOT IN ('pg_catalog','information_schema')
  AND table_name NOT LIKE '\_\_EFMigrationsHistory%' ESCAPE '\'
ORDER BY table_schema, table_name;
"@
$tables = Invoke-Psql -Config $cfg -Sql $tableListSql | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$rowCounts = @{}
$tsvLines = New-Object System.Collections.Generic.List[string]
foreach ($qualified in $tables) {
    $parts = $qualified -split '\.', 2
    $schema = $parts[0]
    $table = $parts[1]
    $countSql = "SELECT COUNT(*) FROM ""$schema"".""$table"";"
    $count = [long]((Invoke-Psql -Config $cfg -Sql $countSql).Trim())
    $rowCounts[$qualified] = $count
    $tsvLines.Add("$schema`t$table`t$count")
}
Set-Content -LiteralPath $rowCountsPath -Value $tsvLines -Encoding UTF8
Write-Timestamped "    $($rowCounts.Count) tables counted" -LogFile $logFile

# ============================================================================
# Step 5: PDF volume backup (optional)
# ============================================================================
$pdfDumpPath = $null
$pdfFileCount = 0
if ($IncludePdfVolume) {
    $pdfDumpPath = Join-Path $snap 'pdf_uploads.tar.gz'
    Write-Timestamped '[5/6] PDF volume backup...' -LogFile $logFile
    & docker run --rm -v "meepleai_pdf_uploads:/src:ro" -v "${snap}:/dst" alpine tar czf /dst/pdf_uploads.tar.gz -C /src .
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to tar PDF volume (exit $LASTEXITCODE)"
    }
    if ($pdfVolumeStats) { $pdfFileCount = $pdfVolumeStats.FileCount }
    $pdfDumpSize = (Get-Item $pdfDumpPath).Length
    Write-Timestamped "    pdf_uploads.tar.gz: $pdfDumpSize bytes ($pdfFileCount files)" -LogFile $logFile
} else {
    Write-Timestamped '[5/6] PDF volume backup: SKIPPED (S3 storage or empty volume)' -LogFile $logFile
}

# ============================================================================
# Step 6: xact_commit snapshot for drift check during reset
# ============================================================================
$xactAtSave = Get-XactCommitCount -Config $cfg
Write-Timestamped "[6/6] xact_commit at save: $xactAtSave" -LogFile $logFile

# Capture for next task
$script:snap = $snap
$script:safetyNetSize = $safetyNetSize
$script:dataDumpSize = $dataDumpSize
$script:dbSize = $dbSize
$script:storageProvider = $storageProvider
$script:pdfDumpPath = $pdfDumpPath
$script:pdfFileCount = $pdfFileCount
$script:rowCounts = $rowCounts
$script:xactAtSave = $xactAtSave
$script:excludedTables = $excludedTables
$script:logFile = $logFile

# Placeholder for next task
Write-Timestamped 'TODO: manifest + summary (Task 13)' -LogFile $logFile
