#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Fase 2: Standardizzazione naming globale - Converte tutti i file in kebab-case

.DESCRIPTION
    - Converte UPPERCASE e snake_case in kebab-case
    - Mantiene README.md e CLAUDE.md invariati
    - Gestisce conversioni PascalCase → kebab-case

.EXAMPLE
    .\consolidate-docs-phase2.ps1
    .\consolidate-docs-phase2.ps1 -DryRun
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
$docsPath = Join-Path $basePath "docs"

# Colori output
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }
function Write-Error { param($msg) Write-Host "❌ $msg" -ForegroundColor Red }

# Funzione conversione kebab-case
function Convert-ToKebabCase {
    param([string]$name)

    # UPPERCASE_SNAKE → kebab-case
    $result = $name -replace "_", "-"

    # PascalCase/camelCase → kebab-case
    # Inserisce trattino prima di lettere maiuscole
    $result = $result -creplace '([a-z0-9])([A-Z])', '$1-$2'

    # Converte tutto in lowercase
    $result = $result.ToLower()

    # Rimuove trattini multipli consecutivi
    $result = $result -replace "-+", "-"

    # Rimuove trattini iniziali/finali
    $result = $result.Trim('-')

    return $result
}

# Banner
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  STANDARDIZZAZIONE NAMING - FASE 2" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Warning "DRY RUN MODE - Nessuna modifica verrà applicata"
    Write-Host ""
}

Write-Info "Scanning directory: $docsPath"
Write-Host ""

# Backup (se non disabilitato)
if (-not $NoBackup -and -not $DryRun) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupPath = Join-Path $basePath "docs-backup-naming-$timestamp"

    Write-Info "Creazione backup in: $backupPath"
    Copy-Item -Path $docsPath -Destination $backupPath -Recurse
    Write-Success "Backup creato"
    Write-Host ""
}

# Trova tutti i file .md eccetto eccezioni
$excludedFiles = @("README.md", "CLAUDE.md")

Write-Info "Ricerca file da rinominare..."
$files = Get-ChildItem -Path $docsPath -Filter "*.md" -Recurse |
    Where-Object { $_.Name -notin $excludedFiles }

Write-Info "Files trovati: $($files.Count)"
Write-Host ""

# Analizza e rinomina
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host " CONVERSIONE NAMING" -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

$renamedCount = 0
$skippedCount = 0
$errorCount = 0

foreach ($file in $files) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $newBaseName = Convert-ToKebabCase $baseName

    # Se il nome è già corretto, skip
    if ($baseName -ceq $newBaseName) {
        $skippedCount++
        continue
    }

    $newFileName = "$newBaseName.md"
    $newPath = Join-Path $file.DirectoryName $newFileName

    # Verifica conflitti
    if ((Test-Path $newPath) -and ($file.FullName -ne $newPath)) {
        Write-Error "CONFLICT: $($file.Name) → $newFileName (file already exists)"
        $errorCount++
        continue
    }

    # Mostra conversione
    $relativePath = $file.FullName.Replace($docsPath, "docs")

    if ($DryRun) {
        Write-Warning "[DRY RUN] $($file.Name) → $newFileName"
    } else {
        try {
            Rename-Item -Path $file.FullName -NewName $newFileName -ErrorAction Stop
            Write-Success "$($file.Name) → $newFileName"
            $renamedCount++
        } catch {
            Write-Error "FAILED: $($file.Name) - $($_.Exception.Message)"
            $errorCount++
        }
    }
}

Write-Host ""

# Summary
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Files scanned:   $($files.Count)" -ForegroundColor Cyan
Write-Host "  Files renamed:   $renamedCount" -ForegroundColor Green
Write-Host "  Files skipped:   $skippedCount" -ForegroundColor Gray
Write-Host "  Errors:          $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Gray" })
Write-Host ""

if ($DryRun) {
    Write-Warning "DRY RUN completato - Nessuna modifica applicata"
    Write-Info "Esegui senza -DryRun per applicare le modifiche"
} else {
    if ($errorCount -eq 0) {
        Write-Success "Fase 2 completata con successo!"
    } else {
        Write-Warning "Fase 2 completata con $errorCount errori"
    }

    if (-not $NoBackup) {
        Write-Info "Backup disponibile in: docs-backup-naming-*"
    }
}

Write-Host ""
