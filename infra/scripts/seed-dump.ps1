#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Dumps the current database state for seed restore on other environments.
.DESCRIPTION
    Creates a PostgreSQL dump of the seeded database with checksum for integrity verification.
    Output: infra/seed-dumps/{timestamp}-{profile}.dump + .sha256
.PARAMETER Profile
    The seed profile that was used (dev, staging, prod). Used for naming only.
.PARAMETER ConnectionString
    PostgreSQL connection string. Defaults to local dev database.
.EXAMPLE
    ./seed-dump.ps1 -Profile dev
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('dev', 'staging', 'prod')]
    [string]$Profile,

    [string]$ConnectionString = "Host=localhost;Port=5432;Database=meepleai;Username=postgres;Password=meeplepass",
    [string]$OutputDir = "$PSScriptRoot/../seed-dumps",
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# Parse connection string
$parts = @{}
$ConnectionString -split ';' | ForEach-Object {
    $kv = $_ -split '=', 2
    $parts[$kv[0].Trim()] = $kv[1].Trim()
}

$timestamp = Get-Date -Format 'yyyy-MM-dd-HHmmss'
$dumpFile = "$OutputDir/$timestamp-$Profile.dump"

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

if ($DryRun) {
    Write-Host "[DRY RUN] Would dump to: $dumpFile"
    Write-Host "[DRY RUN] Host=$($parts['Host']), Database=$($parts['Database'])"
    exit 0
}

Write-Host "Dumping database ($Profile profile)..."

$env:PGPASSWORD = $parts['Password']
pg_dump -h $parts['Host'] -p $parts['Port'] -U $parts['Username'] -d $parts['Database'] `
    -Fc --no-owner --no-acl `
    -f $dumpFile

if ($LASTEXITCODE -ne 0) {
    throw "pg_dump failed with exit code $LASTEXITCODE"
}

# Generate checksum
$hash = Get-FileHash -Path $dumpFile -Algorithm SHA256
"$($hash.Hash)  $(Split-Path $dumpFile -Leaf)" | Out-File "$dumpFile.sha256" -Encoding utf8

Write-Host "Dump complete: $dumpFile"
Write-Host "Checksum: $($hash.Hash)"
Write-Host "Size: $([math]::Round((Get-Item $dumpFile).Length / 1MB, 2)) MB"
