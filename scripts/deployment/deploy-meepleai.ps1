# =============================================================================
# MeepleAI Production Deployment Script (PowerShell)
# =============================================================================
#
# Deploys MeepleAI to production at meepleai.io
#
# Usage:
#   .\scripts\deployment\deploy-meepleai.ps1 [command]
#
# Commands:
#   up        Start all services (default)
#   down      Stop all services
#   restart   Restart all services
#   logs      Show logs
#   status    Show service status
#   backup    Backup databases
#   update    Pull latest images and restart
#
# =============================================================================

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('up', 'down', 'restart', 'logs', 'status', 'backup', 'update', 'help')]
    [string]$Command = 'up',

    [Parameter(Position = 1)]
    [string]$ServiceName
)

$ErrorActionPreference = 'Stop'

# Paths
$ScriptDir = Split-Path -Parent $PSCommandPath
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$InfraDir = Join-Path $ProjectRoot 'infra'

# Compose files
$ComposeFiles = @(
    '-f', 'docker-compose.yml',
    '-f', 'compose.traefik.yml',
    '-f', 'compose.prod.yml',
    '-f', 'compose.meepleai.yml'
)

Set-Location $InfraDir

# =============================================================================
# Functions
# =============================================================================

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Test-Prerequisites {
    Write-Info "Checking prerequisites..."

    # Check Docker
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Write-ErrorMsg "Docker is not installed"
        exit 1
    }

    # Check Docker Compose
    try {
        docker compose version | Out-Null
    }
    catch {
        Write-ErrorMsg "Docker Compose is not installed"
        exit 1
    }

    # Check secrets exist
    if (-not (Test-Path "secrets/prod/postgres-password.txt")) {
        Write-ErrorMsg "Production secrets not found. Run: cd infra/secrets/prod && ./setup-prod-secrets.sh"
        exit 1
    }

    # Check Traefik config exists
    if (-not (Test-Path "traefik/traefik.prod.yml")) {
        Write-ErrorMsg "Traefik production config not found: traefik/traefik.prod.yml"
        exit 1
    }

    Write-Success "Prerequisites check passed"
}

function New-RequiredDirectories {
    Write-Info "Creating required directories..."

    $dirs = @('traefik/letsencrypt', 'traefik/logs')
    foreach ($dir in $dirs) {
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
    }

    # Set permissions (Windows: no-op, Linux: chmod 600)
    if ($IsLinux -or $IsMacOS) {
        chmod 600 traefik/letsencrypt 2>$null
    }

    Write-Success "Directories created"
}

function Start-Services {
    Write-Info "Starting MeepleAI services..."

    docker compose @ComposeFiles --profile full up -d

    Write-Success "Services started"
    Write-Host ""
    Write-Info "Waiting for services to be healthy..."
    Start-Sleep -Seconds 10

    Show-Status
}

function Stop-Services {
    Write-Info "Stopping MeepleAI services..."
    docker compose @ComposeFiles --profile full down
    Write-Success "Services stopped"
}

function Restart-Services {
    Write-Info "Restarting MeepleAI services..."
    docker compose @ComposeFiles --profile full restart
    Write-Success "Services restarted"
}

function Show-Logs {
    if ($ServiceName) {
        docker compose @ComposeFiles logs -f $ServiceName
    }
    else {
        docker compose @ComposeFiles logs -f
    }
}

function Show-Status {
    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "       MeepleAI Service Status          " -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host ""

    docker compose @ComposeFiles ps --format "table {{.Name}}`t{{.Status}}`t{{.Ports}}"

    Write-Host ""
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host "           Endpoints                    " -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  🌐 Website:    https://www.meepleai.io"
    Write-Host "  🔌 API:        https://api.meepleai.io"
    Write-Host "  📊 Grafana:    https://grafana.meepleai.io"
    Write-Host "  🚦 Traefik:    https://traefik.meepleai.io"
    Write-Host ""
}

function Backup-Databases {
    Write-Info "Creating database backup..."

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupDir = Join-Path $ProjectRoot "backups/$timestamp"
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

    # PostgreSQL backup
    Write-Info "Backing up PostgreSQL..."
    docker compose @ComposeFiles exec -T postgres pg_dumpall -U meeple |
        Out-File -FilePath "$backupDir/postgres_backup.sql" -Encoding UTF8

    # Qdrant backup note
    Write-Info "Backing up Qdrant..."
    Write-Warning "Qdrant CLI backup requires manual configuration"

    Write-Success "Backup completed: $backupDir"
}

function Update-Services {
    Write-Info "Pulling latest images..."
    docker compose @ComposeFiles pull

    Write-Info "Recreating containers with new images..."
    docker compose @ComposeFiles --profile full up -d --force-recreate

    Write-Success "Update completed"
    Show-Status
}

function Show-Help {
    Write-Host ""
    Write-Host "MeepleAI Production Deployment" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: .\deploy-meepleai.ps1 [command]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Commands:"
    Write-Host "  up        Start all services (default)"
    Write-Host "  down      Stop all services"
    Write-Host "  restart   Restart all services"
    Write-Host "  logs      Show logs (optional: service name)"
    Write-Host "  status    Show service status"
    Write-Host "  backup    Backup databases"
    Write-Host "  update    Pull latest images and restart"
    Write-Host "  help      Show this help message"
    Write-Host ""
}

# =============================================================================
# Main
# =============================================================================

try {
    switch ($Command) {
        'up' {
            Test-Prerequisites
            New-RequiredDirectories
            Start-Services
        }
        'down' {
            Stop-Services
        }
        'restart' {
            Restart-Services
        }
        'logs' {
            Show-Logs
        }
        'status' {
            Show-Status
        }
        'backup' {
            Backup-Databases
        }
        'update' {
            Test-Prerequisites
            Update-Services
        }
        'help' {
            Show-Help
        }
        default {
            Write-ErrorMsg "Unknown command: $Command"
            Show-Help
            exit 1
        }
    }
}
catch {
    Write-ErrorMsg "An error occurred: $_"
    exit 1
}
