#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Rimuove documentazione obsoleta identificata durante consolidamento

.DESCRIPTION
    Rimuove file marcati come obsoleti, archiviati o non più rilevanti:
    - File in italiano (guida-visualcode.md)
    - File archiviati esplicitamente
    - File HTML fuori contesto (issue-sequences.html)

.EXAMPLE
    .\remove-obsolete-docs.ps1 -DryRun
    .\remove-obsolete-docs.ps1
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$basePath = Split-Path -Parent $PSScriptRoot

# Colori output
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }

# Banner
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  RIMOZIONE DOCUMENTAZIONE OBSOLETA" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Warning "DRY RUN MODE - Nessuna modifica verrà applicata"
    Write-Host ""
}

# File da rimuovere
$obsoleteFiles = @(
    # File in italiano (unico, sostituito da versioni inglesi)
    @{
        Path = "docs\02-development\guida-visualcode.md"
        Reason = "Unico file in italiano, obsoleto (2026-01-18)"
        Category = "Language"
    },

    # File archiviati esplicitamente
    @{
        Path = "docs\01-architecture\overview\archive\consolidation-strategy.md"
        Reason = "Marcato ARCHIVED (completato 2026-02-03)"
        Category = "Archived"
    },

    # File HTML fuori contesto
    @{
        Path = "docs\issue-sequences.html"
        Reason = "File HTML senza contesto chiaro, non referenziato"
        Category = "Orphaned"
    }
)

Write-Info "File obsoleti identificati: $($obsoleteFiles.Count)"
Write-Host ""

Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host " RIMOZIONE FILE" -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

$removedCount = 0
$notFoundCount = 0

foreach ($file in $obsoleteFiles) {
    $fullPath = Join-Path $basePath $file.Path

    if (Test-Path $fullPath) {
        $fileInfo = Get-Item $fullPath
        $sizeKB = [math]::Round($fileInfo.Length / 1KB, 2)

        if ($DryRun) {
            Write-Warning "[DRY RUN] $($file.Path)"
            Write-Host "  Category: $($file.Category)" -ForegroundColor Gray
            Write-Host "  Reason: $($file.Reason)" -ForegroundColor Gray
            Write-Host "  Size: $sizeKB KB" -ForegroundColor Gray
            Write-Host ""
        } else {
            Remove-Item $fullPath -Force
            Write-Success "Removed: $($file.Path) ($sizeKB KB)"
            Write-Host "  Reason: $($file.Reason)" -ForegroundColor Gray
            Write-Host ""
            $removedCount++
        }
    } else {
        Write-Info "Not found (already removed?): $($file.Path)"
        $notFoundCount++
    }
}

# Summary
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "  Files da rimuovere:  $($obsoleteFiles.Count - $notFoundCount)" -ForegroundColor Yellow
    Write-Host "  Files non trovati:   $notFoundCount" -ForegroundColor Gray
    Write-Host ""
    Write-Warning "DRY RUN completato - Nessuna modifica applicata"
    Write-Info "Esegui senza -DryRun per applicare le modifiche"
} else {
    Write-Host "  Files rimossi:       $removedCount" -ForegroundColor Green
    Write-Host "  Files non trovati:   $notFoundCount" -ForegroundColor Gray
    Write-Host ""
    Write-Success "Rimozione completata!"
    Write-Host ""
    Write-Info "Next step:"
    Write-Host "  git status"
    Write-Host "  git add -A"
    Write-Host "  git commit -m 'docs: remove obsolete documentation'"
}

Write-Host ""
