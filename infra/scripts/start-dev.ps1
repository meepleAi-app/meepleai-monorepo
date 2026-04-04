<#
.SYNOPSIS
    Enhanced Docker development environment startup script

.DESCRIPTION
    Orchestrates MeepleAI development environment startup with:
    - Port conflict detection and cleanup
    - Orchestrated service startup (infrastructure → application)
    - Health validation and monitoring
    - Service URL display

.PARAMETER SkipPortCheck
    Skip port conflict detection and cleanup

.PARAMETER Profile
    Docker Compose profile to use (minimal, dev, ai, full)
    Default: dev

.PARAMETER WaitForHealthy
    Wait for all services to become healthy before exiting
    Default: true

.EXAMPLE
    .\start-dev.ps1
    Start development environment with default settings

.EXAMPLE
    .\start-dev.ps1 -Profile full -WaitForHealthy
    Start full stack and wait for all services to be healthy

.EXAMPLE
    .\start-dev.ps1 -SkipPortCheck
    Start without port cleanup (useful if you know ports are free)

.NOTES
    Issue #3797: Created to prevent port conflicts and improve developer experience
    Location: infra/scripts/start-dev.ps1
#>

[CmdletBinding()]
param(
    [switch]$SkipPortCheck,
    [ValidateSet('minimal', 'dev', 'ai', 'automation', 'observability', 'full')]
    [string]$Profile = 'dev',
    [switch]$WaitForHealthy = $true
)

$ErrorActionPreference = 'Stop'

# Colors for output
$script:Colors = @{
    Info    = 'Cyan'
    Success = 'Green'
    Warning = 'Yellow'
    Error   = 'Red'
    Header  = 'Magenta'
}

function Write-Header {
    param([string]$Message)
    Write-Host "`n$('=' * 70)" -ForegroundColor $Colors.Header
    Write-Host $Message -ForegroundColor $Colors.Header
    Write-Host "$('=' * 70)`n" -ForegroundColor $Colors.Header
}

function Write-Step {
    param([string]$Message)
    Write-Host ">>> $Message" -ForegroundColor $Colors.Info
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor $Colors.Success
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor $Colors.Warning
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor $Colors.Error
}

# Navigate to infra directory
$infraDir = Split-Path -Parent $PSScriptRoot
Set-Location $infraDir

Write-Header "🚀 MeepleAI Development Environment Startup"

# Step 1: Port Conflict Check
if (-not $SkipPortCheck) {
    Write-Step "Checking for port conflicts..."

    $cleanupScript = Join-Path (Split-Path -Parent $infraDir) "tools\cleanup\cleanup-dev-ports.ps1"

    if (Test-Path $cleanupScript) {
        try {
            & $cleanupScript -DryRun
            $dryRunResult = $LASTEXITCODE

            if ($dryRunResult -eq 0) {
                Write-Success "No port conflicts detected"
            }
            else {
                Write-Warning "Port conflicts detected - cleaning up..."
                & $cleanupScript
                Write-Success "Port cleanup completed"
            }
        }
        catch {
            Write-Warning "Port cleanup failed: $_"
            Write-Warning "Continuing anyway - Docker may fail if ports are in use"
        }
    }
    else {
        Write-Warning "Cleanup script not found: $cleanupScript"
        Write-Warning "Skipping port check - install cleanup script for automatic port management"
    }
}
else {
    Write-Warning "Port check skipped (--SkipPortCheck flag)"
}

# Step 2: Start Infrastructure Services
Write-Header "🏗️  Starting Infrastructure Services"

Write-Step "Starting PostgreSQL, Redis..."

try {
    docker compose --profile $Profile up -d postgres redis 2>&1 | Out-Null
    Write-Success "Infrastructure services started"
}
catch {
    Write-Error "Failed to start infrastructure: $_"
    exit 1
}

# Step 3: Wait for Infrastructure Health
if ($WaitForHealthy) {
    Write-Step "Waiting for infrastructure to become healthy..."

    $maxWait = 60 # seconds
    $waited = 0
    $interval = 2

    while ($waited -lt $maxWait) {
        $status = docker compose ps --format json | ConvertFrom-Json
        $infraServices = $status | Where-Object { $_.Service -in @('postgres', 'redis') }
        $allHealthy = $infraServices | Where-Object { $_.Health -ne 'healthy' } | Measure-Object | Select-Object -ExpandProperty Count

        if ($allHealthy -eq 0) {
            Write-Success "All infrastructure services healthy"
            break
        }

        Start-Sleep -Seconds $interval
        $waited += $interval
        Write-Host "." -NoNewline
    }

    Write-Host ""

    if ($waited -ge $maxWait) {
        Write-Warning "Infrastructure services did not become healthy within ${maxWait}s"
        Write-Warning "Continuing anyway - application services may fail"
    }
}

# Step 4: Start Application Services
Write-Header "🚀 Starting Application Services"

Write-Step "Starting API backend..."

try {
    docker compose --profile $Profile up -d api 2>&1 | Out-Null
    Write-Success "API service started"
}
catch {
    Write-Error "Failed to start API: $_"
    exit 1
}

# Wait for API health
if ($WaitForHealthy) {
    Write-Step "Waiting for API to become healthy..."

    $maxWait = 30
    $waited = 0

    while ($waited -lt $maxWait) {
        $status = docker compose ps --format json | ConvertFrom-Json
        $apiService = $status | Where-Object { $_.Service -eq 'api' }

        if ($apiService.Health -eq 'healthy') {
            Write-Success "API service healthy"
            break
        }

        Start-Sleep -Seconds 2
        $waited += 2
        Write-Host "." -NoNewline
    }

    Write-Host ""
}

Write-Step "Starting Web frontend..."

try {
    docker compose --profile $Profile up -d web 2>&1 | Out-Null
    Write-Success "Web service started"
}
catch {
    Write-Error "Failed to start web: $_"
    exit 1
}

# Wait for Web health
if ($WaitForHealthy) {
    Write-Step "Waiting for Web to become healthy..."

    $maxWait = 30
    $waited = 0

    while ($waited -lt $maxWait) {
        $status = docker compose ps --format json | ConvertFrom-Json
        $webService = $status | Where-Object { $_.Service -eq 'web' }

        if ($webService.Health -eq 'healthy') {
            Write-Success "Web service healthy"
            break
        }

        Start-Sleep -Seconds 2
        $waited += 2
        Write-Host "." -NoNewline
    }

    Write-Host ""
}

# Step 5: Display Service URLs
Write-Header "🌐 Service URLs"

$services = @(
    @{ Name = "Web Frontend"; URL = "http://localhost:3000"; Status = "Ready" }
    @{ Name = "API Backend"; URL = "http://localhost:8080"; Status = "Ready" }
    @{ Name = "API Docs (Scalar)"; URL = "http://localhost:8080/scalar/v1"; Status = "Ready" }
    @{ Name = "Grafana"; URL = "http://localhost:3001"; Status = "Profile: dev, observability, full" }
    @{ Name = "Prometheus"; URL = "http://localhost:9090"; Status = "Profile: dev, observability, full" }
    @{ Name = "Mailpit"; URL = "http://localhost:8025"; Status = "Profile: dev, full" }
)

foreach ($svc in $services) {
    $nameWidth = 25
    $urlWidth = 40
    Write-Host ("  {0,-$nameWidth} {1,-$urlWidth} {2}" -f $svc.Name, $svc.URL, $svc.Status)
}

# Step 6: Quick Health Summary
Write-Header "📊 Container Status"

docker compose ps --format "table {{.Name}}\t{{.Status}}\t{{.Ports}}"

Write-Host ""
Write-Success "Development environment started successfully!"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor $Colors.Info
Write-Host "  1. Open Web UI: http://localhost:3000" -ForegroundColor White
Write-Host "  2. Check API docs: http://localhost:8080/scalar/v1" -ForegroundColor White
Write-Host "  3. Monitor logs: docker compose logs -f web api" -ForegroundColor White
Write-Host ""
Write-Host "To stop: docker compose down" -ForegroundColor $Colors.Warning
Write-Host "To stop & clean volumes: docker compose down -v" -ForegroundColor $Colors.Error
Write-Host ""
