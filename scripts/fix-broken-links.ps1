#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Aggiorna automaticamente i link rotti dopo consolidamento documentazione

.DESCRIPTION
    Sostituisce riferimenti a file rinominati con i nuovi nomi kebab-case.
    Processa tutti i file .md nella directory docs/

.EXAMPLE
    .\fix-broken-links.ps1
    .\fix-broken-links.ps1 -DryRun
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$basePath = Split-Path -Parent $PSScriptRoot
$docsPath = Join-Path $basePath "docs"

# Colori output
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }

# Banner
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  FIX BROKEN LINKS - Aggiornamento Automatico" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Warning "DRY RUN MODE - Nessuna modifica verrà applicata"
    Write-Host ""
}

# Mappa file rinominati (old → new)
$linkReplacements = @{
    # 02-development/
    'AZUL_TEST_INSTRUCTIONS.md' = 'azul-test-instructions.md'
    'BGG_API_TOKEN_SETUP.md' = 'bgg-api-token-setup.md'
    'BRANCH_PROTECTION_SETUP.md' = 'branch-protection-setup.md'
    'QUICK_START_GUIDE.md' = 'quick-start-guide.md'
    'WORKFLOW_AUDIT_REPORT.md' = 'workflow-audit-report.md'

    # 02-development/monitoring/
    'METRICS-LIMITATION.md' = 'metrics-limitation.md'

    # 04-deployment/
    'CAPACITY_PLANNING.md' = 'capacity-planning.md'
    'NEW-GUIDES-INDEX.md' = 'new-guides-index.md'

    # 05-testing/
    'CI_CD_PIPELINE.md' = 'ci-cd-pipeline.md'

    # 05-testing/backend/
    'BACKEND_E2E_TESTING.md' = 'backend-e2-e-testing.md'
    'INTEGRATION_TEST_OPTIMIZATION.md' = 'integration-test-optimization.md'

    # 05-testing/e2e/
    'BackgroundRulebookAnalysis-ManualTesting.md' = 'background-rulebook-analysis-manual-testing.md'
    'E2E_TEST_GUIDE.md' = 'e2-e-test-guide.md'
    'RulebookAnalysis-ManualTesting.md' = 'rulebook-analysis-manual-testing.md'

    # 06-security/
    'totp_vulnerability_analysis.md' = 'totp-vulnerability-analysis.md'

    # 07-frontend/epics/
    'EPIC-GC-001-game-carousel-integration.md' = 'epic-gc-001-game-carousel-integration.md'
    'EPIC-GC-001-SUMMARY.md' = 'epic-gc-001-summary.md'

    # 04-features/admin-dashboard-enterprise/
    'EPICS-AND-ISSUES.md' = 'epics-and-issues.md'
    'ISSUE-TRACKING.md' = 'issue-tracking.md'
    'SPECIFICATION.md' = 'specification.md'

    # 04-features/private-games-proposal/
    '_ordineDashboardAdmin.md' = 'ordine-dashboard-admin.md'
    '_ordineissue.md' = 'ordineissue.md'
    'DESIGN.md' = 'design.md'
    'IMPLEMENTATION-PLAN.md' = 'implementation-plan.md'
    'USER-STORIES.md' = 'user-stories.md'

    # 09-bounded-contexts/
    'DIAGRAM_SUMMARY.md' = 'diagram-summary.md'
    'administration-COMPLETE.md' = 'administration.md'
    'authentication-COMPLETE.md' = 'authentication.md'
    'document-processing-COMPLETE.md' = 'document-processing.md'
    'game-management-COMPLETE.md' = 'game-management.md'
    'knowledge-base-COMPLETE.md' = 'knowledge-base.md'
    'session-tracking-COMPLETE.md' = 'session-tracking.md'
    'shared-game-catalog-COMPLETE.md' = 'shared-game-catalog.md'
    'system-configuration-COMPLETE.md' = 'system-configuration.md'
    'user-library-COMPLETE.md' = 'user-library.md'
    'user-notifications-COMPLETE.md' = 'user-notifications.md'
    'workflow-integration-COMPLETE.md' = 'workflow-integration.md'

    # Root docs/
    'INDEX.md' = 'index.md'
    'SKILLS-REFERENCE.md' = 'skills-reference.md'

    # 03-api/rag/
    'DELIVERABLES.md' = 'deliverables.md'
    'HOW-IT-WORKS.md' = 'how-it-works.md'
    'SUMMARY.md' = 'summary.md'

    # 03-api/rag/appendix/
    'A-research-sources.md' = 'a-research-sources.md'
    'C-token-cost-breakdown.md' = 'c-token-cost-breakdown.md'
    'D-data-consistency-audit.md' = 'd-data-consistency-audit.md'
    'E-model-pricing-2026.md' = 'e-model-pricing-2026.md'
    'F-calculation-formulas.md' = 'f-calculation-formulas.md'
    'G-admin-configuration-system.md' = 'g-admin-configuration-system.md'

    # 11-user-flows/
    'COMPLETE-TEST-FLOWS.md' = 'complete-test-flows.md'

    # frontend/migrations/
    'DASHBOARD-HUB-MIGRATION-GUIDE.md' = 'dashboard-hub-migration-guide.md'
    'DASHBOARD-HUB-INDEX.md' = 'dashboard-hub-index.md'
    'DASHBOARD-HUB-QUICK-REFERENCE.md' = 'dashboard-hub-quick-reference.md'

    # templates/
    'TEAM-INSTRUCTIONS.md' = 'team-instructions.md'
}

# Trova tutti i file markdown
Write-Info "Scanning file markdown in: $docsPath"
$mdFiles = Get-ChildItem -Path $docsPath -Filter "*.md" -Recurse
Write-Info "Files trovati: $($mdFiles.Count)"
Write-Host ""

Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host " AGGIORNAMENTO LINK" -ForegroundColor Yellow
Write-Host "─────────────────────────────────────────────────────────────" -ForegroundColor Gray
Write-Host ""

$totalReplacements = 0
$filesModified = 0

foreach ($file in $mdFiles) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    $fileReplacements = 0

    # Applica tutte le sostituzioni
    foreach ($oldName in $linkReplacements.Keys) {
        $newName = $linkReplacements[$oldName]

        # Pattern per link markdown: [text](path/old-name.md)
        $pattern = [regex]::Escape($oldName)
        $replacement = $newName

        if ($content -match $pattern) {
            $content = $content -replace $pattern, $replacement
            $fileReplacements++
            $totalReplacements++
        }
    }

    # Se ci sono state modifiche, salva il file
    if ($content -ne $originalContent) {
        $relativePath = $file.FullName.Replace($docsPath + "\", "")

        if ($DryRun) {
            Write-Warning "[DRY RUN] $relativePath ($fileReplacements sostituzioni)"
        } else {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            Write-Success "$relativePath ($fileReplacements sostituzioni)"
            $filesModified++
        }
    }
}

Write-Host ""

# Summary
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Files analizzati:       $($mdFiles.Count)" -ForegroundColor Cyan
Write-Host "  Files modificati:       $filesModified" -ForegroundColor Green
Write-Host "  Link sostituiti:        $totalReplacements" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Warning "DRY RUN completato - Nessuna modifica applicata"
    Write-Info "Esegui senza -DryRun per applicare le modifiche"
} else {
    if ($filesModified -gt 0) {
        Write-Success "Link aggiornati con successo!"
        Write-Host ""
        Write-Info "Next step:"
        Write-Host "  git add docs/"
        Write-Host "  git commit -m 'docs: fix broken links after consolidation'"
    } else {
        Write-Info "Nessun link da aggiornare trovato"
    }
}

Write-Host ""
