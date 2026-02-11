#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Analizza gap tra endpoint API e route UI frontend

.DESCRIPTION
    Identifica quali endpoint API non hanno corrispondenti UI per chiamarli.
    Produce report con gap critici e raccomandazioni.

.EXAMPLE
    .\analyze-ui-api-gaps.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$basePath = Split-Path -Parent $PSScriptRoot

# Colori
function Write-Success { param($msg) Write-Host "✅ $msg" -ForegroundColor Green }
function Write-Info { param($msg) Write-Host "ℹ️  $msg" -ForegroundColor Cyan }
function Write-Warning { param($msg) Write-Host "⚠️  $msg" -ForegroundColor Yellow }
function Write-Gap { param($msg) Write-Host "🔴 $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  UI ↔ API GAP ANALYSIS" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# STEP 1: Estrai Endpoint API
# ═══════════════════════════════════════════════════════════════════
Write-Info "STEP 1: Analisi endpoint API (.NET)"
Write-Host ""

$apiPath = Join-Path $basePath "apps\api\src\Api\Routing"
$endpointFiles = Get-ChildItem -Path $apiPath -Filter "*.cs" -Recurse

Write-Info "File di routing trovati: $($endpointFiles.Count)"
Write-Host ""

# Pattern per estrarre route API
$apiEndpoints = @()

foreach ($file in $endpointFiles) {
    $content = Get-Content $file.FullName -Raw

    # Estrai tutti i Map* calls con pattern regex
    $matches = [regex]::Matches($content, 'Map(Get|Post|Put|Delete|Patch)\s*\(\s*"([^"]+)"')

    foreach ($match in $matches) {
        $method = $match.Groups[1].Value
        $route = $match.Groups[2].Value

        $apiEndpoints += [PSCustomObject]@{
            File = $file.BaseName
            Method = $method
            Route = $route
            FullRoute = $route
        }
    }
}

Write-Success "Endpoint API trovati: $($apiEndpoints.Count)"
Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# STEP 2: Estrai Route Frontend
# ═══════════════════════════════════════════════════════════════════
Write-Info "STEP 2: Analisi route frontend (Next.js)"
Write-Host ""

$webAppPath = Join-Path $basePath "apps\web\src\app"
$frontendRoutes = @()

# Recursively find route directories
Get-ChildItem -Path $webAppPath -Recurse -Directory | ForEach-Object {
    $dirPath = $_.FullName
    $relativePath = $dirPath.Replace($webAppPath, "").Replace("\", "/")

    # Skip route groups (parentheses), and look for page.tsx
    if ($relativePath -match '\([^)]+\)') {
        $relativePath = $relativePath -replace '\([^)]+\)/', ''
    }

    # Check if directory has page.tsx or route.ts
    $hasPage = Test-Path (Join-Path $dirPath "page.tsx")
    $hasRoute = Test-Path (Join-Path $dirPath "route.ts")

    if ($hasPage -or $hasRoute) {
        $frontendRoutes += [PSCustomObject]@{
            Route = $relativePath
            Type = if ($hasPage) { "Page" } else { "API Route" }
        }
    }
}

Write-Success "Route frontend trovate: $($frontendRoutes.Count)"
Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# STEP 3: Identifica GAP Critici
# ═══════════════════════════════════════════════════════════════════
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  GAP ANALYSIS RESULTS" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host ""

# Group API endpoints by context
$endpointsByContext = $apiEndpoints | Group-Object {
    if ($_.Route -match '^/api/v1/([^/]+)') {
        $matches[1]
    } else {
        "other"
    }
}

# Analizza gap per bounded context
$gapReport = @()

foreach ($context in $endpointsByContext) {
    $contextName = $context.Name
    $endpoints = $context.Group

    # Cerca route frontend corrispondenti
    $hasUI = $frontendRoutes | Where-Object {
        $_.Route -match $contextName -or
        $_.Route -match "/admin.*$contextName" -or
        $_.Route -match "/$contextName"
    }

    $coverage = if ($hasUI) { "Partial" } else { "None" }
    $priority = if ($endpoints.Count -gt 5 -and -not $hasUI) { "HIGH" } else { "Medium" }

    $gapReport += [PSCustomObject]@{
        Context = $contextName
        Endpoints = $endpoints.Count
        UICoverage = $coverage
        Priority = $priority
        HasAdminUI = $($frontendRoutes | Where-Object { $_.Route -match "/admin.*$contextName" }) -ne $null
    }
}

# Ordina per priorità
$gapReport = $gapReport | Sort-Object -Property Priority -Descending

Write-Host "📊 GAP SUMMARY BY BOUNDED CONTEXT" -ForegroundColor Yellow
Write-Host ""
$gapReport | Format-Table Context, Endpoints, UICoverage, HasAdminUI, Priority -AutoSize
Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# STEP 4: Endpoint Critici Senza UI
# ═══════════════════════════════════════════════════════════════════
Write-Host "🔴 CRITICAL GAPS - Endpoint senza UI" -ForegroundColor Red
Write-Host ""

# Identifica endpoint senza UI corrispondente
$criticalGaps = @()

# Admin endpoints senza pagina admin
$adminEndpoints = $apiEndpoints | Where-Object { $_.Route -match '^/api/v1/admin' }
foreach ($ep in $adminEndpoints) {
    $route = $ep.Route -replace '^/api/v1', ''
    $hasUI = $frontendRoutes | Where-Object { $_.Route -match [regex]::Escape($route) }

    if (-not $hasUI) {
        $criticalGaps += [PSCustomObject]@{
            Endpoint = $ep.Route
            Method = $ep.Method
            File = $ep.File
            Severity = "High"
        }
    }
}

# User endpoints senza UI
$userEndpoints = $apiEndpoints | Where-Object {
    $_.Route -notmatch '^/api/v1/admin' -and
    $_.Route -notmatch '^/api/v1/(health|monitoring|test)'
}

foreach ($ep in $userEndpoints | Select-Object -First 20) {
    $segment = if ($ep.Route -match '^/api/v1/([^/]+)') { $matches[1] } else { "" }

    if ($segment) {
        $hasUI = $frontendRoutes | Where-Object {
            $_.Route -match "/$segment" -or
            $_.Route -match "/library.*$segment" -or
            $_.Route -match "/dashboard.*$segment"
        }

        if (-not $hasUI) {
            $criticalGaps += [PSCustomObject]@{
                Endpoint = $ep.Route
                Method = $ep.Method
                File = $ep.File
                Severity = "Medium"
            }
        }
    }
}

# Mostra primi 30 gap
Write-Warning "Primi 30 gap critici identificati:"
$criticalGaps | Select-Object -First 30 | Format-Table Endpoint, Method, Severity -Wrap

Write-Host ""
Write-Info "Total critical gaps: $($criticalGaps.Count)"
Write-Host ""

# ═══════════════════════════════════════════════════════════════════
# SUMMARY
# ═══════════════════════════════════════════════════════════════════
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "  FINAL SUMMARY" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "  API Endpoints totali:    $($apiEndpoints.Count)" -ForegroundColor Cyan
Write-Host "  Frontend Routes:         $($frontendRoutes.Count)" -ForegroundColor Cyan
Write-Host "  Critical Gaps:           $($criticalGaps.Count)" -ForegroundColor Red
Write-Host "  Coverage Rate:           $([math]::Round((1 - $criticalGaps.Count / $apiEndpoints.Count) * 100, 1))%" -ForegroundColor Yellow
Write-Host ""

Write-Info "Report salvato per analisi dettagliata"
Write-Host ""
