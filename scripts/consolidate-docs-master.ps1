#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Master script - Esegue tutte le fasi di consolidamento documentazione

.DESCRIPTION
    Esegue in sequenza:
    1. Fase 1: Consolidamento Bounded Contexts
    2. Fase 2: Standardizzazione Naming
    3. Verifica: Controllo integrità finale

.PARAMETER DryRun
    Esegue simulazione senza modificare file

.PARAMETER SkipPhase1
    Salta la fase 1 (bounded contexts)

.PARAMETER SkipPhase2
    Salta la fase 2 (naming standardization)

.PARAMETER SkipVerify
    Salta la verifica finale

.EXAMPLE
    .\consolidate-docs-master.ps1 -DryRun
    .\consolidate-docs-master.ps1
    .\consolidate-docs-master.ps1 -SkipPhase1
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$DryRun,

    [Parameter()]
    [switch]$SkipPhase1,

    [Parameter()]
    [switch]$SkipPhase2,

    [Parameter()]
    [switch]$SkipVerify
)

$ErrorActionPreference = "Stop"
$scriptPath = $PSScriptRoot

# Colori output
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }
function Write-Error { param($msg) Write-Host "❌ $msg" -ForegroundColor Red }
function Write-Section { param($msg) Write-Host "`n═══════════════════════════════════════════════════════════" -ForegroundColor Cyan; Write-Host "  $msg" -ForegroundColor Cyan; Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan; Write-Host "" }

# Banner
Clear-Host
Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║                                                           ║" -ForegroundColor Magenta
Write-Host "║      CONSOLIDAMENTO DOCUMENTAZIONE - MASTER SCRIPT        ║" -ForegroundColor Magenta
Write-Host "║                                                           ║" -ForegroundColor Magenta
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

if ($DryRun) {
    Write-Warning "🔍 DRY RUN MODE ATTIVO - Nessuna modifica verrà applicata"
    Write-Host ""
}

# Timestamp inizio
$startTime = Get-Date
Write-Info "Inizio esecuzione: $($startTime.ToString('yyyy-MM-dd HH:mm:ss'))"
Write-Host ""

# Verifica git status
Write-Info "Verifica stato repository..."
$gitStatus = git status --porcelain 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Repository git non inizializzato o non trovato"
} elseif ($gitStatus) {
    Write-Warning "⚠️  Repository ha modifiche non committate"
    Write-Host ""
    Write-Host "Si consiglia di committare prima di procedere:"
    Write-Host "  git add ."
    Write-Host "  git commit -m 'docs: pre-consolidamento backup'"
    Write-Host ""

    $response = Read-Host "Continuare comunque? (y/N)"
    if ($response -ne 'y' -and $response -ne 'Y') {
        Write-Info "Esecuzione annullata dall'utente"
        exit 0
    }
} else {
    Write-Success "Repository pulito (nessuna modifica uncommitted)"
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# FASE 1: BOUNDED CONTEXTS
# ═══════════════════════════════════════════════════════════════════
if (-not $SkipPhase1) {
    Write-Section "FASE 1: CONSOLIDAMENTO BOUNDED CONTEXTS"

    $phase1Script = Join-Path $scriptPath "consolidate-docs-phase1.ps1"

    if (-not (Test-Path $phase1Script)) {
        Write-Error "Script Fase 1 non trovato: $phase1Script"
        exit 1
    }

    try {
        if ($DryRun) {
            & $phase1Script -DryRun -NoBackup
        } else {
            & $phase1Script
        }

        if ($LASTEXITCODE -eq 0) {
            Write-Success "Fase 1 completata"
        } else {
            Write-Error "Fase 1 fallita con exit code: $LASTEXITCODE"
            exit $LASTEXITCODE
        }
    } catch {
        Write-Error "Errore durante Fase 1: $($_.Exception.Message)"
        exit 1
    }

    Write-Host ""
} else {
    Write-Info "⏭️  Fase 1 saltata (--SkipPhase1)"
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════
# FASE 2: NAMING STANDARDIZATION
# ═══════════════════════════════════════════════════════════════════
if (-not $SkipPhase2) {
    Write-Section "FASE 2: STANDARDIZZAZIONE NAMING"

    $phase2Script = Join-Path $scriptPath "consolidate-docs-phase2.ps1"

    if (-not (Test-Path $phase2Script)) {
        Write-Error "Script Fase 2 non trovato: $phase2Script"
        exit 1
    }

    try {
        if ($DryRun) {
            & $phase2Script -DryRun -NoBackup
        } else {
            & $phase2Script
        }

        if ($LASTEXITCODE -eq 0) {
            Write-Success "Fase 2 completata"
        } else {
            Write-Error "Fase 2 fallita con exit code: $LASTEXITCODE"
            exit $LASTEXITCODE
        }
    } catch {
        Write-Error "Errore durante Fase 2: $($_.Exception.Message)"
        exit 1
    }

    Write-Host ""
} else {
    Write-Info "⏭️  Fase 2 saltata (--SkipPhase2)"
    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════
# VERIFICA FINALE
# ═══════════════════════════════════════════════════════════════════
if (-not $SkipVerify -and -not $DryRun) {
    Write-Section "VERIFICA FINALE"

    $verifyScript = Join-Path $scriptPath "consolidate-docs-verify.ps1"

    if (-not (Test-Path $verifyScript)) {
        Write-Warning "Script verifica non trovato: $verifyScript"
    } else {
        try {
            & $verifyScript

            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Verifica completata con warnings"
            }
        } catch {
            Write-Warning "Errore durante verifica: $($_.Exception.Message)"
        }
    }

    Write-Host ""
}

# ═══════════════════════════════════════════════════════════════════
# SUMMARY FINALE
# ═══════════════════════════════════════════════════════════════════
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                                                           ║" -ForegroundColor Green
Write-Host "║                  CONSOLIDAMENTO COMPLETATO                ║" -ForegroundColor Green
Write-Host "║                                                           ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-Host "  Durata totale:    $($duration.ToString('mm\:ss'))" -ForegroundColor Cyan
Write-Host "  Fase 1:           $(if ($SkipPhase1) { 'Saltata' } else { 'Eseguita' })" -ForegroundColor $(if ($SkipPhase1) { 'Gray' } else { 'Green' })
Write-Host "  Fase 2:           $(if ($SkipPhase2) { 'Saltata' } else { 'Eseguita' })" -ForegroundColor $(if ($SkipPhase2) { 'Gray' } else { 'Green' })
Write-Host "  Verifica:         $(if ($SkipVerify -or $DryRun) { 'Saltata' } else { 'Eseguita' })" -ForegroundColor $(if ($SkipVerify -or $DryRun) { 'Gray' } else { 'Green' })
Write-Host ""

if ($DryRun) {
    Write-Warning "📝 DRY RUN completato - Nessuna modifica applicata"
    Write-Info "Esegui senza -DryRun per applicare le modifiche"
} else {
    Write-Success "✨ Consolidamento eseguito con successo!"
    Write-Host ""
    Write-Info "Next steps:"
    Write-Host "  1. Verifica modifiche: git status"
    Write-Host "  2. Aggiorna link rotti (se presenti)"
    Write-Host "  3. Commit: git add docs/ && git commit -m 'docs: consolidate structure'"
}

Write-Host ""
