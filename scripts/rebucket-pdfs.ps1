#!/usr/bin/env pwsh
# Rebucket script: pdf_documents storage from {gameId} to {pdfId} bucket.
# Usage: ./scripts/rebucket-pdfs.ps1 -StorageRoot "apps/api/src/Api/storage/pdfs" -DryRun

param(
    [Parameter(Mandatory=$true)][string]$StorageRoot,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# NOTE: DB credentials in dev are meepleai/meepleai_staging — adjust per environment.
$pdfs = docker exec meepleai-postgres psql -U meepleai -d meepleai -t -A -F "," -c `
    "SELECT ""Id""::text, COALESCE(""PrivateGameId""::text, ""SharedGameId""::text) FROM pdf_documents WHERE ""FilePath"" IS NOT NULL;"

$moved = 0; $skipped = 0; $missing = 0

foreach ($row in ($pdfs -split "`n" | Where-Object { $_.Trim() })) {
    $parts = $row -split ","
    $pdfId = $parts[0].Replace("-","")
    $gameId = if ($parts[1]) { $parts[1].Replace("-","") } else { $null }

    if (-not $gameId) { $skipped++; continue }

    $oldPath = Join-Path $StorageRoot "$gameId/$pdfId.pdf"
    $newPath = Join-Path $StorageRoot "$pdfId/$pdfId.pdf"

    if (-not (Test-Path $oldPath)) { $missing++; continue }
    if (Test-Path $newPath) { $skipped++; continue }

    if ($DryRun) {
        Write-Host "[DRY] Move: $oldPath -> $newPath"
    } else {
        New-Item -ItemType Directory -Path (Split-Path $newPath) -Force | Out-Null
        Move-Item $oldPath $newPath
    }
    $moved++
}

Write-Host "Moved: $moved | Skipped: $skipped | Missing: $missing"
