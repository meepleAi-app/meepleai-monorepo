# migrate-volume-names.ps1 - Migrate Docker volume names to new standardized convention
#
# Purpose: Rename existing volumes from infra_* prefix to meepleai_* prefix
#          and standardize naming from hyphens to underscores
#
# Created: 2026-01-19
# Issue: Volume naming consistency across profiles
#
# IMPORTANT: This script migrates data from old volumes to new volumes
#            Run BEFORE starting services with new docker-compose.yml
#
# Usage:
#   .\infra\scripts\migrate-volume-names.ps1
#   OR
#   pwsh .\infra\scripts\migrate-volume-names.ps1
#
# Prerequisites:
#   - Docker Desktop running
#   - Services stopped: docker compose down
#   - PowerShell 5.1+ or PowerShell Core 7+

param(
    [switch]$DryRun = $false,
    [switch]$Force = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Docker Volume Migration Script" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# Volume mapping: old_name -> new_name
$volumeMappings = @{
    "infra_pgdata" = "meepleai_postgres_data"
    "infra_qdrantdata" = "meepleai_qdrant_data"
    "infra_ollamadata" = "meepleai_ollama_data"
    "infra_mailpit-data" = "meepleai_mailpit_data"
    "infra_prometheusdata" = "meepleai_prometheus_data"
    "infra_grafanadata" = "meepleai_grafana_data"
    "infra_alertmanagerdata" = "meepleai_alertmanager_data"
    "infra_unstructured-temp" = "meepleai_unstructured_temp"
    "infra_smoldocling-temp" = "meepleai_smoldocling_temp"
    "infra_smoldocling-models" = "meepleai_smoldocling_models"
    "infra_reranker-models" = "meepleai_reranker_models"
    "infra_pdf-uploads" = "meepleai_pdf_uploads"
}

function Test-VolumeExists {
    param([string]$VolumeName)

    $volumes = docker volume ls --format "{{.Name}}" 2>&1
    return $volumes -contains $VolumeName
}

function Get-VolumeSize {
    param([string]$VolumeName)

    try {
        $inspect = docker volume inspect $VolumeName 2>&1 | ConvertFrom-Json
        $mountpoint = $inspect.Mountpoint
        if ($mountpoint) {
            # Size estimation not directly available, return mountpoint
            return $mountpoint
        }
        return "Unknown"
    }
    catch {
        return "N/A"
    }
}

function Migrate-Volume {
    param(
        [string]$OldName,
        [string]$NewName,
        [bool]$DryRun
    )

    Write-Host "  Migrating: $OldName -> $NewName" -ForegroundColor Yellow

    if ($DryRun) {
        Write-Host "    [DRY RUN] Would create volume: $NewName" -ForegroundColor Gray
        Write-Host "    [DRY RUN] Would copy data from: $OldName" -ForegroundColor Gray
        return $true
    }

    # Check if old volume exists
    if (-not (Test-VolumeExists $OldName)) {
        Write-Host "    [SKIP] Old volume does not exist: $OldName" -ForegroundColor Gray
        return $true
    }

    # Check if new volume already exists
    if (Test-VolumeExists $NewName) {
        Write-Host "    [WARNING] New volume already exists: $NewName" -ForegroundColor Yellow
        if (-not $Force) {
            Write-Host "    [SKIP] Use -Force to overwrite existing volume" -ForegroundColor Gray
            return $false
        }
        Write-Host "    [FORCE] Removing existing new volume..." -ForegroundColor Red
        docker volume rm $NewName 2>&1 | Out-Null
    }

    # Create new volume
    Write-Host "    Creating new volume: $NewName" -ForegroundColor Green
    docker volume create $NewName 2>&1 | Out-Null

    # Copy data using temporary container
    Write-Host "    Copying data..." -ForegroundColor Green
    $copyResult = docker run --rm `
        -v "${OldName}:/source:ro" `
        -v "${NewName}:/destination" `
        alpine sh -c "cd /source && cp -av . /destination" 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "    [SUCCESS] Migration complete" -ForegroundColor Green
        return $true
    }
    else {
        Write-Host "    [ERROR] Migration failed: $copyResult" -ForegroundColor Red
        return $false
    }
}

# Main execution
Write-Host "Step 1: Checking Docker status..." -ForegroundColor Cyan
try {
    docker info 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Docker is not running. Please start Docker Desktop." -ForegroundColor Red
        exit 1
    }
    Write-Host "  [OK] Docker is running" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Cannot connect to Docker: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 2: Checking for running containers..." -ForegroundColor Cyan
$runningContainers = docker ps --format "{{.Names}}" 2>&1
if ($runningContainers) {
    Write-Host "[WARNING] Found running containers:" -ForegroundColor Yellow
    $runningContainers | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    Write-Host ""
    Write-Host "Please stop containers before migration:" -ForegroundColor Yellow
    Write-Host "  cd infra && docker compose down" -ForegroundColor White
    if (-not $Force) {
        Write-Host ""
        Write-Host "Use -Force to proceed anyway (not recommended)" -ForegroundColor Gray
        exit 1
    }
}
else {
    Write-Host "  [OK] No running containers" -ForegroundColor Green
}

Write-Host ""
Write-Host "Step 3: Volume migration plan..." -ForegroundColor Cyan
Write-Host ""

$migrationResults = @()

foreach ($mapping in $volumeMappings.GetEnumerator()) {
    $oldName = $mapping.Key
    $newName = $mapping.Value

    $result = Migrate-Volume -OldName $oldName -NewName $newName -DryRun $DryRun
    $migrationResults += @{
        Old = $oldName
        New = $newName
        Success = $result
    }
}

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Migration Summary" -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

$successCount = ($migrationResults | Where-Object { $_.Success }).Count
$totalCount = $migrationResults.Count

Write-Host "Total volumes: $totalCount" -ForegroundColor White
Write-Host "Migrated: $successCount" -ForegroundColor Green
Write-Host "Skipped/Failed: $($totalCount - $successCount)" -ForegroundColor Yellow

if ($DryRun) {
    Write-Host ""
    Write-Host "[DRY RUN MODE] No changes were made" -ForegroundColor Yellow
    Write-Host "Run without -DryRun to perform actual migration" -ForegroundColor Yellow
}
else {
    Write-Host ""
    Write-Host "[NEXT STEPS]" -ForegroundColor Cyan
    Write-Host "1. Verify migrations:" -ForegroundColor White
    Write-Host "   docker volume ls | findstr meepleai" -ForegroundColor Gray
    Write-Host ""
    Write-Host "2. Start services with new configuration:" -ForegroundColor White
    Write-Host "   cd infra && docker compose -f docker-compose.yml -f docker-compose.test.yml --profile dev up -d" -ForegroundColor Gray
    Write-Host ""
    Write-Host "3. Verify data integrity:" -ForegroundColor White
    Write-Host "   docker compose ps" -ForegroundColor Gray
    Write-Host "   docker logs meepleai-postgres" -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. (Optional) Remove old volumes after verification:" -ForegroundColor White
    Write-Host "   docker volume rm infra_pgdata infra_qdrantdata ..." -ForegroundColor Gray
}

Write-Host ""
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "Migration Complete" -ForegroundColor Green
Write-Host "==================================================================" -ForegroundColor Cyan
