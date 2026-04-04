#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Restores a database from a seed dump with safety guards.
.PARAMETER DumpFile
    Path to the .dump file to restore.
.PARAMETER ConnectionString
    PostgreSQL connection string. Defaults to local dev database.
.PARAMETER Force
    Skip confirmation prompt.
.PARAMETER DryRun
    Show what would happen without executing.
#>
param(
    [Parameter(Mandatory)]
    [string]$DumpFile,

    [string]$ConnectionString = "Host=localhost;Port=5432;Database=meepleai;Username=postgres;Password=meeplepass",
    [switch]$Force,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $DumpFile)) {
    throw "Dump file not found: $DumpFile"
}

# Verify checksum if .sha256 file exists
$checksumFile = "$DumpFile.sha256"
if (Test-Path $checksumFile) {
    $expectedHash = (Get-Content $checksumFile -Raw).Trim().Split(' ')[0]
    $actualHash = (Get-FileHash -Path $DumpFile -Algorithm SHA256).Hash
    if ($expectedHash -ne $actualHash) {
        throw "Checksum mismatch! Expected: $expectedHash, Got: $actualHash"
    }
    Write-Host "Checksum verified OK"
}

# Parse connection string
$parts = @{}
$ConnectionString -split ';' | ForEach-Object {
    $kv = $_ -split '=', 2
    $parts[$kv[0].Trim()] = $kv[1].Trim()
}

# Safety guard: prevent restoring staging dump to production
$dumpName = Split-Path $DumpFile -Leaf
if ($dumpName -match 'staging' -and $parts['Host'] -notmatch 'localhost|127\.0\.0\.1|staging') {
    throw "Safety guard: refusing to restore staging dump to non-staging/non-local host: $($parts['Host'])"
}

if ($DryRun) {
    Write-Host "[DRY RUN] Would restore: $DumpFile"
    Write-Host "[DRY RUN] To: $($parts['Host']):$($parts['Port'])/$($parts['Database'])"
    exit 0
}

if (-not $Force) {
    $confirm = Read-Host "This will REPLACE the database '$($parts['Database'])' on $($parts['Host']). Continue? (yes/no)"
    if ($confirm -ne 'yes') {
        Write-Host "Aborted."
        exit 0
    }
}

Write-Host "Restoring database from $DumpFile..."

$env:PGPASSWORD = $parts['Password']

# Drop and recreate database
psql -h $parts['Host'] -p $parts['Port'] -U $parts['Username'] -d postgres `
    -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='$($parts['Database'])' AND pid <> pg_backend_pid();"
psql -h $parts['Host'] -p $parts['Port'] -U $parts['Username'] -d postgres `
    -c "DROP DATABASE IF EXISTS `"$($parts['Database'])`";"
psql -h $parts['Host'] -p $parts['Port'] -U $parts['Username'] -d postgres `
    -c "CREATE DATABASE `"$($parts['Database'])`";"

# Restore
pg_restore -h $parts['Host'] -p $parts['Port'] -U $parts['Username'] -d $parts['Database'] `
    --no-owner --no-acl --clean --if-exists `
    $DumpFile

if ($LASTEXITCODE -ne 0) {
    Write-Warning "pg_restore completed with warnings (exit code $LASTEXITCODE) - this is often normal"
}

Write-Host "Restore complete!"
