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
    [switch]$Force,
    [string]$SecretFile = (Join-Path $PSScriptRoot '..' 'secrets' 'database.secret')
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
$logFile = Join-Path $SnapshotPath 'restore.log'
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
Write-Timestamped '[1/9] Snapshot validated' -LogFile $logFile

# Localhost-only enforcement
$cfg = Get-PostgresConfig -SecretPath $SecretFile
Assert-LocalhostOnly -Config $cfg
Write-Timestamped '[2/9] Localhost host check passed' -LogFile $logFile

# Empty-DB check for standard restore mode (skip in -UseSafetyNet which drops/recreates anyway)
if (-not $UseSafetyNet) {
    $rowCheckSql = @"
SELECT COALESCE(SUM(n_live_tup), 0)::bigint
FROM pg_stat_user_tables
WHERE schemaname NOT IN ('pg_catalog','information_schema')
"@
    try {
        $totalRows = [long]((Invoke-Psql -Config $cfg -Sql $rowCheckSql).Trim())
    } catch {
        $totalRows = 0
    }
    if ($totalRows -gt 0) {
        Write-Host ''
        Write-Host "⚠ Target DB '$($cfg.Db)' is NOT empty (~$totalRows rows in user tables)." -ForegroundColor Yellow
        Write-Host '   Standard restore expects a fresh schema. Restoring into a populated DB' -ForegroundColor Yellow
        Write-Host '   will likely cause primary key conflicts.' -ForegroundColor Yellow
        if (-not (Confirm-UserAction -Prompt 'Continue restore into non-empty DB anyway?' -Force:$Force)) {
            Write-Timestamped 'Aborted by user.' -LogFile $logFile
            exit 0
        }
    }
}

# Resolve RestorePdfVolume default
if ($null -eq $RestorePdfVolume) {
    $RestorePdfVolume = ($manifest.pdfVolumeIncluded -eq $true)
}

# Branch on mode
if ($UseSafetyNet) {
    Write-Timestamped '[3/9] Mode: SAFETY-NET ROLLBACK' -LogFile $logFile

    $safetyNetPath = Join-Path $SnapshotPath $manifest.files.safetyNet.name
    if (-not (Test-Path -LiteralPath $safetyNetPath)) {
        throw "safety-net.dump not found at $safetyNetPath"
    }

    # Confirmation
    $prompt = @"
This will:
  1. DROP database '$($cfg.Db)' (current contents will be LOST)
  2. RESTORE from $($manifest.files.safetyNet.name) (full schema + data)

After rollback, the DB will be in the state it had at $($manifest.createdAt).

NOTE: If you have already run db-reset-migrations and the Migrations/ folder
has been replaced, the restored DB will be INCONSISTENT with the code.
You will need to also restore Migrations/ from $SnapshotPath/migrations-backup/.
"@
    if (-not (Confirm-UserAction -Prompt $prompt -Force:$Force)) {
        Write-Timestamped 'Aborted by user.' -LogFile $logFile
        exit 0
    }

    # Drop and recreate DB
    Write-Timestamped '[4/9] Dropping and recreating DB...' -LogFile $logFile
    $cfgPostgres = [pscustomobject]@{
        Host = $cfg.Host; Port = $cfg.Port; User = $cfg.User; Password = $cfg.Password; Db = 'postgres'
    }
    $null = Invoke-Psql -Config $cfgPostgres -Sql "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$($cfg.Db)' AND pid <> pg_backend_pid();"
    $null = Invoke-Psql -Config $cfgPostgres -Sql "DROP DATABASE IF EXISTS ""$($cfg.Db)"";"
    $null = Invoke-Psql -Config $cfgPostgres -Sql "CREATE DATABASE ""$($cfg.Db)"";"
    Write-Timestamped '    DB recreated' -LogFile $logFile

    # Restore from safety-net
    Write-Timestamped '[5/9] pg_restore from safety-net...' -LogFile $logFile
    $restoreStart = Get-Date
    Invoke-PgRestore -Config $cfg -Arguments @(
        '--clean', '--if-exists', '--no-owner', '--no-acl',
        '--single-transaction',
        $safetyNetPath
    )
    $restoreSeconds = ((Get-Date) - $restoreStart).TotalSeconds
    Write-Timestamped "    Restored in $([math]::Round($restoreSeconds,1))s" -LogFile $logFile

    # Verify row counts (must all match)
    Write-Timestamped '[6/9] Verifying row counts...' -LogFile $logFile
    $mismatches = New-Object System.Collections.Generic.List[pscustomobject]
    $matched = 0
    foreach ($prop in $manifest.rowCountsPre.PSObject.Properties) {
        $qualified = $prop.Name
        $expected = [long]$prop.Value
        $parts = $qualified -split '\.', 2
        $sql = "SELECT COUNT(*) FROM ""$($parts[0])"".""$($parts[1])"";"
        try {
            $actual = [long]((Invoke-Psql -Config $cfg -Sql $sql).Trim())
        } catch {
            $actual = -1
        }
        if ($actual -eq $expected) { $matched++ } else {
            $mismatches.Add([pscustomobject]@{ Table = $qualified; Expected = $expected; Actual = $actual })
        }
    }
    $rowCountMatch = ($mismatches.Count -eq 0)
    if (-not $rowCountMatch) {
        Write-Host '⚠ Row count mismatches after safety-net restore:' -ForegroundColor Yellow
        foreach ($m in $mismatches) { Write-Host "    $($m.Table): expected=$($m.Expected), actual=$($m.Actual)" -ForegroundColor Yellow }
    }
    Write-Timestamped "    $matched/$($matched + $mismatches.Count) tables match" -LogFile $logFile

    # Restore PDF volume too if requested
    $pdfRestored = $false
    $pdfFilesRestored = 0
    if ($RestorePdfVolume -and $manifest.pdfVolumeIncluded) {
        $pdfTarPath = Join-Path $SnapshotPath 'pdf_uploads.tar.gz'
        if (Test-Path -LiteralPath $pdfTarPath) {
            Write-Timestamped '[7/9] Restoring PDF volume...' -LogFile $logFile
            & docker run --rm -v "meepleai_pdf_uploads:/dst" -v "${SnapshotPath}:/src:ro" alpine sh -c 'rm -rf /dst/* && tar xzf /src/pdf_uploads.tar.gz -C /dst'
            if ($LASTEXITCODE -eq 0) {
                $stats = Get-DockerVolumeStats -VolumeName 'meepleai_pdf_uploads'
                $pdfFilesRestored = $stats.FileCount
                $pdfRestored = $true
                Write-Timestamped "    $pdfFilesRestored PDF files restored" -LogFile $logFile
            }
        }
    }

    # Write restore-result.json
    $restoreResult = [pscustomobject]@{
        schemaVersion          = 1
        completedAt            = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        mode                   = 'safety-net'
        rowCountMatch          = $rowCountMatch
        rowCountMismatches     = @($mismatches)
        pdfVolumeRestored      = $pdfRestored
        pdfFilesRestoredCount  = $pdfFilesRestored
        durationSeconds        = [math]::Round($restoreSeconds, 1)
    }
    Write-Manifest -Path (Join-Path $SnapshotPath 'restore-result.json') -Object $restoreResult

    # Migrations folder warning
    $migrationsBackup = Join-Path $SnapshotPath 'migrations-backup'
    if (Test-Path -LiteralPath $migrationsBackup) {
        Write-Host ''
        Write-Host '⚠ DB restored to pre-reset state.' -ForegroundColor Yellow
        Write-Host '  Your Migrations/ folder may now conflict with the DB state.' -ForegroundColor Yellow
        Write-Host "  To fully rollback, restore Migrations/ from $migrationsBackup :" -ForegroundColor Yellow
        Write-Host "    Remove-Item -Recurse apps/api/src/Api/Migrations" -ForegroundColor Yellow
        Write-Host "    Copy-Item -Recurse $migrationsBackup apps/api/src/Api/Migrations" -ForegroundColor Yellow
    }

    Write-Timestamped '' -LogFile $logFile
    Write-Timestamped '=== Safety-net rollback complete ===' -LogFile $logFile
    exit 0
}

Write-Timestamped '[3/9] Mode: standard data restore' -LogFile $logFile

# Standard mode requires the new schema in place
$schemaPostPath = Join-Path $SnapshotPath 'schema-post.sql'
Write-Timestamped '[4/9] Re-dumping fresh schema-post.sql...' -LogFile $logFile
Invoke-PgDump -Config $cfg -Arguments @('-s', '--no-owner', '--no-acl', '-f', $schemaPostPath)

# Read both schemas, normalize
$schemaPrePath = Join-Path $SnapshotPath 'schema-pre.sql'
$preSql = Get-Content -LiteralPath $schemaPrePath -Raw
$postSql = Get-Content -LiteralPath $schemaPostPath -Raw
$preNormalized = Normalize-PgSchema -Sql $preSql
$postNormalized = Normalize-PgSchema -Sql $postSql
Set-Content -LiteralPath (Join-Path $SnapshotPath 'schema-pre.normalized.sql') -Value $preNormalized -Encoding UTF8
Set-Content -LiteralPath (Join-Path $SnapshotPath 'schema-post.normalized.sql') -Value $postNormalized -Encoding UTF8
Write-Timestamped '[5/9] Schema normalized' -LogFile $logFile

# Classify drift
$driftClass = Test-SchemaDriftClass -PreSchema $preNormalized -PostSchema $postNormalized
Write-Timestamped "[6/9] Drift classification: $driftClass" -LogFile $logFile

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
    Write-Timestamped 'Aborting. DB unchanged (fresh schema, empty).' -LogFile $logFile
    exit 2
}

if ($driftClass -eq 'minor') {
    Write-Host "⚠ Minor schema drift detected (whitespace/cosmetic). Proceeding." -ForegroundColor Yellow
}

# ============================================================================
# Restore data
# ============================================================================
$dataDumpPath = Join-Path $SnapshotPath $manifest.files.dataDump.name
Write-Timestamped "[7/9] Restoring data from $($manifest.files.dataDump.name)..." -LogFile $logFile
$restoreStart = Get-Date
Invoke-PgRestore -Config $cfg -Arguments @(
    '--data-only', '--no-owner', '--no-acl',
    '--disable-triggers', '--single-transaction',
    $dataDumpPath
)
$restoreSeconds = ((Get-Date) - $restoreStart).TotalSeconds
Write-Timestamped "    Data restored in $([math]::Round($restoreSeconds,1))s" -LogFile $logFile

# ============================================================================
# Reset sequences (via pg_depend join, owns table+column for each sequence)
# ============================================================================
Write-Timestamped '[8/9] Resetting sequences...' -LogFile $logFile
$seqDiscoverySql = @"
SELECT
  ns.nspname || '|' || s.relname || '|' || tns.nspname || '|' || t.relname || '|' || a.attname
FROM pg_class s
JOIN pg_namespace ns ON ns.oid = s.relnamespace
JOIN pg_depend d ON d.objid = s.oid AND d.deptype = 'a'
JOIN pg_class t ON t.oid = d.refobjid
JOIN pg_namespace tns ON tns.oid = t.relnamespace
JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = d.refobjsubid
WHERE s.relkind = 'S'
  AND ns.nspname NOT IN ('pg_catalog','information_schema');
"@
$seqRows = Invoke-Psql -Config $cfg -Sql $seqDiscoverySql | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$sequencesReset = 0
foreach ($row in $seqRows) {
    $parts = $row -split '\|'
    if ($parts.Count -ne 5) { continue }
    $seqSchema = $parts[0]
    $seqName = $parts[1]
    $tblSchema = $parts[2]
    $tblName = $parts[3]
    $colName = $parts[4]
    $setvalSql = "SELECT setval('""$seqSchema"".""$seqName""', (SELECT COALESCE(MAX(""$colName""),0) + 1 FROM ""$tblSchema"".""$tblName""), false);"
    $null = Invoke-Psql -Config $cfg -Sql $setvalSql
    $sequencesReset++
}
Write-Timestamped "    $sequencesReset sequences reset" -LogFile $logFile

# ============================================================================
# Verify row counts
# ============================================================================
Write-Timestamped '[9/9] Verifying row counts...' -LogFile $logFile
$mismatches = New-Object System.Collections.Generic.List[pscustomobject]
$matched = 0
foreach ($prop in $manifest.rowCountsPre.PSObject.Properties) {
    $qualified = $prop.Name
    $expected = [long]$prop.Value
    $parts = $qualified -split '\.', 2
    $schema = $parts[0]
    $table = $parts[1]
    $sql = "SELECT COUNT(*) FROM ""$schema"".""$table"";"
    try {
        $actualRaw = Invoke-Psql -Config $cfg -Sql $sql
        $actual = [long]($actualRaw.Trim())
    } catch {
        $actual = -1
    }
    # If sanitize was used and the table is in excludedTables, expect 0
    $isExcluded = $false
    if ($manifest.sanitize) {
        foreach ($ex in $manifest.excludedTables) {
            if ($table -eq $ex) { $isExcluded = $true; break }
        }
    }
    $expectedActual = if ($isExcluded) { 0 } else { $expected }
    if ($actual -eq $expectedActual) {
        $matched++
    } else {
        $mismatches.Add([pscustomobject]@{
            Table = $qualified
            Expected = $expectedActual
            Actual = $actual
        })
    }
}
$rowCountMatch = ($mismatches.Count -eq 0)
if (-not $rowCountMatch) {
    Write-Host "⚠ Row count mismatches:" -ForegroundColor Yellow
    foreach ($m in $mismatches) {
        Write-Host "    $($m.Table): expected=$($m.Expected), actual=$($m.Actual)" -ForegroundColor Yellow
    }
}
Write-Timestamped "    $matched/$($matched + $mismatches.Count) tables match" -LogFile $logFile

# ============================================================================
# Restore PDF volume (if applicable)
# ============================================================================
$pdfRestored = $false
$pdfFilesRestored = 0
if ($RestorePdfVolume -and $manifest.pdfVolumeIncluded) {
    $pdfTarPath = Join-Path $SnapshotPath 'pdf_uploads.tar.gz'
    if (Test-Path -LiteralPath $pdfTarPath) {
        Write-Timestamped 'Restoring PDF volume...' -LogFile $logFile
        & docker run --rm -v "meepleai_pdf_uploads:/dst" -v "${SnapshotPath}:/src:ro" alpine sh -c 'rm -rf /dst/* && tar xzf /src/pdf_uploads.tar.gz -C /dst'
        if ($LASTEXITCODE -ne 0) {
            Write-Host "⚠ PDF volume restore failed (exit $LASTEXITCODE)" -ForegroundColor Yellow
        } else {
            $stats = Get-DockerVolumeStats -VolumeName 'meepleai_pdf_uploads'
            $pdfFilesRestored = $stats.FileCount
            $pdfRestored = $true
            Write-Timestamped "    $pdfFilesRestored PDF files restored" -LogFile $logFile
        }
    }
}

# ============================================================================
# Write restore-result.json
# ============================================================================
$restoreResult = [pscustomobject]@{
    schemaVersion          = 1
    completedAt            = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    mode                   = 'data-only'
    schemaDriftClass       = $driftClass
    schemaDiffFile         = [pscustomobject]@{
        name = 'schema-diff.txt'
        sha256 = (Get-FileSha256 -Path $diffPath)
        bytes = (Get-Item $diffPath).Length
    }
    rowCountMatch          = $rowCountMatch
    rowCountMismatches     = @($mismatches)
    pdfVolumeRestored      = $pdfRestored
    pdfFilesRestoredCount  = $pdfFilesRestored
    sequencesReset         = $sequencesReset
    durationSeconds        = [math]::Round($restoreSeconds, 1)
}
Write-Manifest -Path (Join-Path $SnapshotPath 'restore-result.json') -Object $restoreResult

# ============================================================================
# Summary
# ============================================================================
Write-Timestamped '' -LogFile $logFile
Write-Timestamped '=== Restore complete ===' -LogFile $logFile
Write-Timestamped "    Schema:    $driftClass" -LogFile $logFile
Write-Timestamped "    Rows:      $matched/$($matched + $mismatches.Count) tables match" -LogFile $logFile
Write-Timestamped "    Sequences: $sequencesReset reset" -LogFile $logFile
if ($pdfRestored) {
    Write-Timestamped "    PDFs:      $pdfFilesRestored files restored" -LogFile $logFile
}
Write-Timestamped "    Duration:  $([math]::Round($restoreSeconds,1))s" -LogFile $logFile
