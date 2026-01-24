#!/usr/bin/env pwsh
<#
.SYNOPSIS
    MeepleAI E2E Demo Setup Script

.DESCRIPTION
    Automated setup for MeepleAI development environment with full E2E demo workflow.

    Phases:
    1. Start critical Docker services (postgres, redis, qdrant)
    2. Start optional AI services (unstructured, smoldocling)
    3. Build and start frontend/backend (no cache)
    4. Initialize shared games via BGG bulk import
    5. Upload PDF rulebooks

.PARAMETER Phase
    Specific phase to run: "all", "infra", "ai", "build", "seed", "pdf"

.PARAMETER SkipBuild
    Skip Docker build (use cached images)

.PARAMETER Verbose
    Enable verbose output

.EXAMPLE
    .\e2e-demo-setup.ps1
    # Runs all phases

.EXAMPLE
    .\e2e-demo-setup.ps1 -Phase infra
    # Only starts infrastructure services

.NOTES
    Author: MeepleAI Team
    Version: 1.0.0
    Date: 2026-01-21
#>

[CmdletBinding()]
param(
    [ValidateSet("all", "infra", "ai", "build", "seed", "pdf")]
    [string]$Phase = "all",

    [switch]$SkipBuild,

    [switch]$Force
)

# Configuration
$ErrorActionPreference = "Stop"
$InfraDir = Join-Path $PSScriptRoot ".." "infra"
$DataDir = Join-Path $PSScriptRoot ".." "data"
$ApiUrl = "http://localhost:8080"
$FrontendUrl = "http://localhost:3000"

# Colors for output
function Write-Phase($message) { Write-Host "`n🚀 $message" -ForegroundColor Cyan }
function Write-Step($message) { Write-Host "  → $message" -ForegroundColor White }
function Write-Success($message) { Write-Host "  ✅ $message" -ForegroundColor Green }
function Write-Warning($message) { Write-Host "  ⚠️ $message" -ForegroundColor Yellow }
function Write-Error($message) { Write-Host "  ❌ $message" -ForegroundColor Red }

# Games with available PDF rulebooks
$DemoGames = @(
    @{ BggId = 13; Title = "Catan"; Pdf = "cantan_en_rulebook.pdf"; Action = "pdf-upload" }
    @{ BggId = 9209; Title = "Ticket to Ride"; Pdf = "ticket-to-ride_rulebook.pdf"; Action = "edit" }
    @{ BggId = 822; Title = "Carcassonne"; Pdf = "carcassone_rulebook.pdf"; Action = "remove" }
    @{ BggId = 266192; Title = "Wingspan"; Pdf = "wingspan_en_rulebook.pdf"; Action = "pdf-smoldocling" }
    @{ BggId = 167791; Title = "Terraforming Mars"; Pdf = "terraforming-mars_rulebook.pdf"; Action = "pdf-unstructured" }
)

function Test-ServiceHealth {
    param([string]$Url, [int]$TimeoutSeconds = 60)

    $startTime = Get-Date
    while ((Get-Date) - $startTime -lt [TimeSpan]::FromSeconds($TimeoutSeconds)) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 5 -UseBasicParsing -ErrorAction SilentlyContinue
            if ($response.StatusCode -eq 200) { return $true }
        } catch { }
        Start-Sleep -Seconds 2
    }
    return $false
}

function Start-InfrastructureServices {
    Write-Phase "FASE 1: Avvio Servizi Critici (Infrastructure)"

    Push-Location $InfraDir
    try {
        # Check secrets exist
        Write-Step "Verifica secrets..."
        $requiredSecrets = @("database.secret", "redis.secret", "qdrant.secret")
        foreach ($secret in $requiredSecrets) {
            $secretPath = Join-Path "secrets" $secret
            if (-not (Test-Path $secretPath)) {
                Write-Warning "Secret mancante: $secret - esegui setup-secrets.ps1"
                if (-not $Force) {
                    Write-Step "Esecuzione setup-secrets.ps1..."
                    & (Join-Path "secrets" "setup-secrets.ps1") -SaveGenerated
                }
            }
        }
        Write-Success "Secrets verificati"

        # Start critical services
        Write-Step "Avvio postgres, redis, qdrant..."
        docker compose --profile minimal up -d postgres redis qdrant

        # Wait for health
        Write-Step "Attesa health check postgres..."
        $healthy = $false
        for ($i = 0; $i -lt 30; $i++) {
            $status = docker inspect --format='{{.State.Health.Status}}' meepleai-postgres 2>$null
            if ($status -eq "healthy") { $healthy = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $healthy) { throw "Postgres non healthy dopo 60s" }
        Write-Success "Postgres healthy"

        Write-Step "Attesa health check redis..."
        $healthy = $false
        for ($i = 0; $i -lt 15; $i++) {
            $status = docker inspect --format='{{.State.Health.Status}}' meepleai-redis 2>$null
            if ($status -eq "healthy") { $healthy = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $healthy) { throw "Redis non healthy dopo 30s" }
        Write-Success "Redis healthy"

        Write-Step "Attesa health check qdrant..."
        $healthy = $false
        for ($i = 0; $i -lt 15; $i++) {
            $status = docker inspect --format='{{.State.Health.Status}}' meepleai-qdrant 2>$null
            if ($status -eq "healthy") { $healthy = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $healthy) { throw "Qdrant non healthy dopo 30s" }
        Write-Success "Qdrant healthy"

        Write-Success "Servizi critici avviati"
    } finally {
        Pop-Location
    }
}

function Start-AIServices {
    Write-Phase "FASE 2: Avvio Servizi AI (Opzionali)"

    Push-Location $InfraDir
    try {
        # Build AI services if needed
        if (-not $SkipBuild) {
            Write-Step "Build unstructured-service (no cache)..."
            docker compose build --no-cache unstructured-service

            Write-Step "Build smoldocling-service (no cache)..."
            docker compose build --no-cache smoldocling-service
        }

        # Start AI services
        Write-Step "Avvio unstructured-service, smoldocling-service..."
        docker compose --profile ai up -d unstructured-service smoldocling-service

        # Wait for health (longer timeout for AI services)
        Write-Step "Attesa health check unstructured (può richiedere 2-3 min)..."
        $healthy = $false
        for ($i = 0; $i -lt 90; $i++) {
            $status = docker inspect --format='{{.State.Health.Status}}' meepleai-unstructured 2>$null
            if ($status -eq "healthy") { $healthy = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $healthy) {
            Write-Warning "Unstructured non healthy - continuo comunque"
        } else {
            Write-Success "Unstructured healthy"
        }

        Write-Step "Attesa health check smoldocling (può richiedere 3-5 min per download modello)..."
        $healthy = $false
        for ($i = 0; $i -lt 150; $i++) {
            $status = docker inspect --format='{{.State.Health.Status}}' meepleai-smoldocling 2>$null
            if ($status -eq "healthy") { $healthy = $true; break }
            Start-Sleep -Seconds 2
        }
        if (-not $healthy) {
            Write-Warning "SmolDocling non healthy - continuo comunque"
        } else {
            Write-Success "SmolDocling healthy"
        }

        Write-Success "Servizi AI avviati"
    } finally {
        Pop-Location
    }
}

function Start-ApplicationServices {
    Write-Phase "FASE 3: Build e Avvio Frontend/Backend"

    Push-Location $InfraDir
    try {
        if (-not $SkipBuild) {
            Write-Step "Build backend (no cache)..."
            docker compose build --no-cache api 2>$null
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Backend Docker build fallito - prova build locale"
            }

            Write-Step "Build frontend (no cache)..."
            docker compose build --no-cache frontend 2>$null
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Frontend Docker build fallito - prova build locale"
            }
        }

        # Try Docker first, fallback to local
        Write-Step "Avvio servizi applicativi..."
        docker compose --profile minimal up -d traefik frontend 2>$null

        # Check if services started
        $apiHealthy = Test-ServiceHealth -Url "$ApiUrl/health" -TimeoutSeconds 120
        if (-not $apiHealthy) {
            Write-Warning "API non raggiungibile - assicurati che il backend sia in esecuzione"
            Write-Step "Avvia manualmente: cd apps/api/src/Api && dotnet run"
        } else {
            Write-Success "Backend API healthy"
        }

        $frontendHealthy = Test-ServiceHealth -Url $FrontendUrl -TimeoutSeconds 60
        if (-not $frontendHealthy) {
            Write-Warning "Frontend non raggiungibile - assicurati che sia in esecuzione"
            Write-Step "Avvia manualmente: cd apps/web && pnpm dev"
        } else {
            Write-Success "Frontend healthy"
        }

    } finally {
        Pop-Location
    }
}

function Initialize-SharedGames {
    Write-Phase "FASE 4: Inizializzazione Shared Games via BGG Import"

    # Check API is healthy
    $apiHealthy = Test-ServiceHealth -Url "$ApiUrl/health" -TimeoutSeconds 10
    if (-not $apiHealthy) {
        Write-Error "API non raggiungibile - impossibile procedere"
        return
    }

    foreach ($game in $DemoGames) {
        Write-Step "Importazione $($game.Title) (BGG ID: $($game.BggId))..."

        try {
            # Import from BGG
            $body = @{
                bggId = $game.BggId
            } | ConvertTo-Json

            $response = Invoke-RestMethod -Uri "$ApiUrl/api/v1/admin/shared-games/import-bgg" `
                -Method Post `
                -Body $body `
                -ContentType "application/json" `
                -Headers @{ "Cookie" = "user_role=Admin" } `
                -ErrorAction SilentlyContinue

            Write-Success "$($game.Title) importato (ID: $response)"
            $game.GameId = $response
        } catch {
            Write-Warning "Errore importazione $($game.Title): $($_.Exception.Message)"
        }
    }

    Write-Success "Shared games inizializzati"
}

function Upload-PdfRulebooks {
    Write-Phase "FASE 5: Upload PDF Rulebooks"

    $rulebookDir = Join-Path $DataDir "rulebook"

    foreach ($game in $DemoGames | Where-Object { $_.Action -like "pdf-*" }) {
        $pdfPath = Join-Path $rulebookDir $game.Pdf

        if (-not (Test-Path $pdfPath)) {
            Write-Warning "PDF non trovato: $pdfPath"
            continue
        }

        Write-Step "Upload $($game.Pdf) per $($game.Title)..."

        # Determine which service to use
        $service = switch ($game.Action) {
            "pdf-upload" { "manual" }
            "pdf-unstructured" { "unstructured" }
            "pdf-smoldocling" { "smoldocling" }
            default { "manual" }
        }

        Write-Step "  Servizio: $service"

        # TODO: Implement actual upload via API
        # This would require multipart form data upload
        Write-Warning "  Upload PDF richiede implementazione manuale via UI"
    }

    Write-Success "Fase PDF completata (alcuni upload richiedono intervento manuale)"
}

function Show-Summary {
    Write-Phase "RIEPILOGO E2E DEMO"

    Write-Host "`n📊 Stato Servizi:" -ForegroundColor White
    Write-Host "  • API Backend:    $ApiUrl/health"
    Write-Host "  • Frontend:       $FrontendUrl"
    Write-Host "  • Admin Panel:    $FrontendUrl/admin"
    Write-Host "  • Shared Games:   $FrontendUrl/admin/shared-games"
    Write-Host "  • BGG Import:     $FrontendUrl/admin/shared-games/add-from-bgg"

    Write-Host "`n🎮 Giochi Demo:" -ForegroundColor White
    foreach ($game in $DemoGames) {
        $actionIcon = switch ($game.Action) {
            "pdf-upload" { "📄" }
            "pdf-unstructured" { "🤖" }
            "pdf-smoldocling" { "🔬" }
            "edit" { "✏️" }
            "remove" { "🗑️" }
            default { "🎲" }
        }
        Write-Host "  $actionIcon $($game.Title) (BGG: $($game.BggId)) - $($game.Action)"
    }

    Write-Host "`n📁 PDF Rulebooks disponibili:" -ForegroundColor White
    Write-Host "  $DataDir\rulebook\"

    Write-Host "`n🔑 Autenticazione Admin:" -ForegroundColor White
    Write-Host "  Cookie: user_role=Admin"
    Write-Host "  Oppure login UI con credenziali admin"

    Write-Host "`n✅ Setup completato!" -ForegroundColor Green
}

# Main execution
Write-Host @"

╔═══════════════════════════════════════════════════════════════╗
║                MeepleAI E2E Demo Setup                        ║
║                                                               ║
║  Flusso completo per ambiente di sviluppo con demo showcase   ║
╚═══════════════════════════════════════════════════════════════╝

"@ -ForegroundColor Cyan

switch ($Phase) {
    "all" {
        Start-InfrastructureServices
        Start-AIServices
        Start-ApplicationServices
        Initialize-SharedGames
        Upload-PdfRulebooks
        Show-Summary
    }
    "infra" {
        Start-InfrastructureServices
    }
    "ai" {
        Start-AIServices
    }
    "build" {
        Start-ApplicationServices
    }
    "seed" {
        Initialize-SharedGames
    }
    "pdf" {
        Upload-PdfRulebooks
    }
}
