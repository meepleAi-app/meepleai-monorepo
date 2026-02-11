#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Verifica integrità documentazione dopo consolidamento

.DESCRIPTION
    - Controlla link rotti nei file markdown
    - Verifica convenzione naming
    - Genera report di verifica

.EXAMPLE
    .\consolidate-docs-verify.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$basePath = Split-Path -Parent $PSScriptRoot
$docsPath = Join-Path $basePath "docs"

# Colori output
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }
function Write-Error { param($msg) Write-Host "❌ $msg" -ForegroundColor Red }

# Banner
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  VERIFICA CONSOLIDAMENTO DOCUMENTAZIONE" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Funzione verifica kebab-case
function Test-IsKebabCase {
    param([string]$name)
    # kebab-case: solo lettere minuscole, numeri e trattini
    return $name -match '^[a-z0-9]+(-[a-z0-9]+)*$'
}

# ═══════════════════════════════════════════════════════════════════
# CHECK 1: Naming Convention
# ═══════════════════════════════════════════════════════════════════
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host " CHECK 1: Naming Convention (kebab-case)" -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

$excludedFiles = @("README.md", "CLAUDE.md")
$allFiles = Get-ChildItem -Path $docsPath -Filter "*.md" -Recurse |
    Where-Object { $_.Name -notin $excludedFiles }

$invalidNaming = @()

foreach ($file in $allFiles) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)

    if (-not (Test-IsKebabCase $baseName)) {
        $relativePath = $file.FullName.Replace($docsPath, "docs")
        $invalidNaming += [PSCustomObject]@{
            File = $file.Name
            Path = $relativePath
        }
    }
}

if ($invalidNaming.Count -eq 0) {
    Write-Success "Tutti i file seguono la convenzione kebab-case"
} else {
    Write-Warning "Trovati $($invalidNaming.Count) file con naming non conforme:"
    $invalidNaming | Format-Table -AutoSize
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# CHECK 2: Duplicati Bounded Contexts
# ═══════════════════════════════════════════════════════════════════
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host " CHECK 2: Duplicati Bounded Contexts" -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

$bcPath = Join-Path $docsPath "09-bounded-contexts"
$completeFiles = Get-ChildItem -Path $bcPath -Filter "*-COMPLETE.md" -ErrorAction SilentlyContinue

if ($completeFiles.Count -eq 0) {
    Write-Success "Nessun file *-COMPLETE.md trovato (atteso dopo Fase 1)"
} else {
    Write-Warning "Trovati $($completeFiles.Count) file *-COMPLETE.md:"
    $completeFiles | Select-Object Name | Format-Table -AutoSize
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# CHECK 3: Link Rotti (Basic Check)
# ═══════════════════════════════════════════════════════════════════
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host " CHECK 3: Link Markdown (Basic)" -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

Write-Info "Ricerca link interni nei file markdown..."

$brokenLinks = @()
$totalLinks = 0

foreach ($file in $allFiles) {
    $content = Get-Content $file.FullName -Raw

    # Trova tutti i link markdown [text](path)
    $links = [regex]::Matches($content, '\[([^\]]+)\]\(([^)]+)\)')

    foreach ($link in $links) {
        $linkPath = $link.Groups[2].Value
        $totalLinks++

        # Ignora link esterni e ancore
        if ($linkPath -match '^https?://' -or $linkPath -match '^#') {
            continue
        }

        # Rimuove anchor (#section)
        $linkPath = $linkPath -replace '#.*$', ''

        # Costruisce path assoluto
        $targetPath = if ($linkPath -match '^/') {
            Join-Path $basePath ($linkPath.TrimStart('/'))
        } else {
            $linkDir = Split-Path $file.FullName -Parent
            Join-Path $linkDir $linkPath
        }

        # Verifica esistenza
        if (-not (Test-Path $targetPath)) {
            $brokenLinks += [PSCustomObject]@{
                File = $file.Name
                Link = $linkPath
                Target = $targetPath
            }
        }
    }
}

Write-Info "Links analizzati: $totalLinks"

if ($brokenLinks.Count -eq 0) {
    Write-Success "Nessun link rotto trovato"
} else {
    Write-Warning "Trovati $($brokenLinks.Count) potenziali link rotti:"
    $brokenLinks | Format-Table File, Link -Wrap
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# SUMMARY
# ═══════════════════════════════════════════════════════════════════
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  VERIFICA SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

$totalIssues = $invalidNaming.Count + $completeFiles.Count + $brokenLinks.Count

Write-Host "  Files totali:         $($allFiles.Count)" -ForegroundColor Cyan
Write-Host "  Naming non conforme:  $($invalidNaming.Count)" -ForegroundColor $(if ($invalidNaming.Count -eq 0) { "Green" } else { "Yellow" })
Write-Host "  Duplicati COMPLETE:   $($completeFiles.Count)" -ForegroundColor $(if ($completeFiles.Count -eq 0) { "Green" } else { "Yellow" })
Write-Host "  Link potenziali rotti: $($brokenLinks.Count)" -ForegroundColor $(if ($brokenLinks.Count -eq 0) { "Green" } else { "Yellow" })
Write-Host ""
Write-Host "  Totale issues:        $totalIssues" -ForegroundColor $(if ($totalIssues -eq 0) { "Green" } else { "Yellow" })
Write-Host ""

if ($totalIssues -eq 0) {
    Write-Success "✨ Documentazione consolidata correttamente! ✨"
} else {
    Write-Warning "⚠️  Trovate $totalIssues issues da risolvere"
}

Write-Host ""
