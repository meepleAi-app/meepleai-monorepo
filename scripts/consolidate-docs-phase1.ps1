#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fase 1: Consolidamento Bounded Contexts - Rimuove duplicati e rinomina file COMPLETE

.DESCRIPTION
    - Elimina 11 file brevi di bounded contexts
    - Rinomina 11 file -COMPLETE.md rimuovendo il suffisso
    - Crea backup prima dell'esecuzione

.EXAMPLE
    .\consolidate-docs-phase1.ps1
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [switch]$NoBackup
)

$ErrorActionPreference = "Stop"
$basePath = Split-Path -Parent $PSScriptRoot
$docsPath = Join-Path $basePath "docs\09-bounded-contexts"

# Colori output
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }
function Write-Error { param($msg) Write-Host "❌ $msg" -ForegroundColor Red }

# Banner
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  CONSOLIDAMENTO BOUNDED CONTEXTS - FASE 1" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Warning "DRY RUN MODE - Nessuna modifica verrà applicata"
    Write-Host ""
}

# Verifica path
if (-not (Test-Path $docsPath)) {
    Write-Error "Path non trovato: $docsPath"
    exit 1
}

Write-Info "Working directory: $docsPath"
Write-Host ""

# Backup (se non disabilitato)
if (-not $NoBackup -and -not $DryRun) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupPath = Join-Path $basePath "docs\09-bounded-contexts-backup-$timestamp"

    Write-Info "Creazione backup in: $backupPath"
    Copy-Item -Path $docsPath -Destination $backupPath -Recurse
    Write-Success "Backup creato"
    Write-Host ""
}

# Lista file da eliminare (versioni brevi)
$shortFiles = @(
    "administration",
    "authentication",
    "document-processing",
    "game-management",
    "knowledge-base",
    "shared-game-catalog",
    "system-configuration",
    "user-library",
    "user-notifications",
    "workflow-integration"
)

Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host " STEP 1: Rimozione file brevi" -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

$deletedCount = 0
foreach ($file in $shortFiles) {
    $filePath = Join-Path $docsPath "$file.md"

    if (Test-Path $filePath) {
        $fileInfo = Get-Item $filePath
        $sizeKB = [math]::Round($fileInfo.Length / 1KB, 2)

        if ($DryRun) {
            Write-Warning "[DRY RUN] Would delete: $file.md ($sizeKB KB)"
        } else {
            Remove-Item $filePath -Force
            Write-Success "Deleted: $file.md ($sizeKB KB)"
        }
        $deletedCount++
    } else {
        Write-Info "Skip (not found): $file.md"
    }
}

Write-Host ""
Write-Info "Files deleted: $deletedCount/$($shortFiles.Count)"
Write-Host ""

# Lista file COMPLETE da rinominare
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host " STEP 2: Rinominazione file COMPLETE" -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

$completeFiles = Get-ChildItem -Path $docsPath -Filter "*-COMPLETE.md"
$renamedCount = 0

foreach ($file in $completeFiles) {
    $newName = $file.Name -replace "-COMPLETE", ""
    $newPath = Join-Path $file.DirectoryName $newName
    $sizeKB = [math]::Round($file.Length / 1KB, 2)

    if ($DryRun) {
        Write-Warning "[DRY RUN] Would rename: $($file.Name) → $newName ($sizeKB KB)"
    } else {
        Rename-Item -Path $file.FullName -NewName $newName
        Write-Success "Renamed: $($file.Name) → $newName ($sizeKB KB)"
    }
    $renamedCount++
}

Write-Host ""
Write-Info "Files renamed: $renamedCount"
Write-Host ""

# Summary
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Files deleted:  $deletedCount" -ForegroundColor Green
Write-Host "  Files renamed:  $renamedCount" -ForegroundColor Green
Write-Host "  Total changes:  $($deletedCount + $renamedCount)" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Warning "DRY RUN completato - Nessuna modifica applicata"
    Write-Info "Esegui senza -DryRun per applicare le modifiche"
} else {
    Write-Success "Fase 1 completata con successo!"
    if (-not $NoBackup) {
        Write-Info "Backup disponibile in: docs\09-bounded-contexts-backup-*"
    }
}

Write-Host ""
