#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup environment variables for integration tests with external infrastructure.

.DESCRIPTION
    Issue #2541: Configure TEST_POSTGRES_CONNSTRING and TEST_REDIS_CONNSTRING
    for parallel integration test execution with Docker Compose infrastructure.

.PARAMETER AdminPassword
    PostgreSQL admin password (reads from infra/secrets/admin.secret if not provided)

.EXAMPLE
    .\setup-test-env.ps1
    Sets environment variables using password from secret file

.EXAMPLE
    .\setup-test-env.ps1 -AdminPassword "mypassword"
    Sets environment variables with explicit password
#>

param(
    [string]$AdminPassword
)

# Determine repository root
$repoRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$secretPath = Join-Path $repoRoot "infra\secrets\admin.secret"

# Read admin password from secret file if not provided
if (-not $AdminPassword) {
    if (Test-Path $secretPath) {
        $AdminPassword = Get-Content $secretPath -Raw | ForEach-Object { $_.Trim() }
        Write-Host "✓ Loaded admin password from $secretPath" -ForegroundColor Green
    }
    else {
        Write-Host "❌ Admin password not found at $secretPath" -ForegroundColor Red
        Write-Host "Run: cd infra/secrets && .\setup-secrets.ps1" -ForegroundColor Yellow
        exit 1
    }
}

# Configure environment variables
$env:TEST_POSTGRES_CONNSTRING = "Host=localhost;Port=5432;Database=test_shared;Username=admin;Password=$AdminPassword;Ssl Mode=Disable;Trust Server Certificate=true;Pooling=false;Timeout=10"
$env:TEST_REDIS_CONNSTRING = "localhost:6379"

# Verify Docker Compose infrastructure is running
Write-Host "`n🔍 Checking Docker Compose infrastructure..." -ForegroundColor Cyan

$infraPath = Join-Path $repoRoot "infra"
Push-Location $infraPath

try {
    $postgresStatus = docker compose ps postgres --format json 2>$null | ConvertFrom-Json
    $redisStatus = docker compose ps redis --format json 2>$null | ConvertFrom-Json

    if ($postgresStatus.State -ne "running") {
        Write-Host "⚠️  PostgreSQL container not running" -ForegroundColor Yellow
        Write-Host "Starting PostgreSQL..." -ForegroundColor Cyan
        docker compose up -d postgres
    }
    else {
        Write-Host "✓ PostgreSQL running" -ForegroundColor Green
    }

    if ($redisStatus.State -ne "running") {
        Write-Host "⚠️  Redis container not running" -ForegroundColor Yellow
        Write-Host "Starting Redis..." -ForegroundColor Cyan
        docker compose up -d redis
    }
    else {
        Write-Host "✓ Redis running" -ForegroundColor Green
    }
}
catch {
    Write-Host "❌ Failed to check Docker Compose status" -ForegroundColor Red
    Write-Host "Ensure Docker Desktop is running and try:" -ForegroundColor Yellow
    Write-Host "  cd infra && docker compose up -d postgres redis" -ForegroundColor Yellow
}
finally {
    Pop-Location
}

# Display configuration
Write-Host "`n✅ Environment configured for parallel integration tests`n" -ForegroundColor Green
Write-Host "TEST_POSTGRES_CONNSTRING: $env:TEST_POSTGRES_CONNSTRING" -ForegroundColor Cyan
Write-Host "TEST_REDIS_CONNSTRING: $env:TEST_REDIS_CONNSTRING" -ForegroundColor Cyan

Write-Host "`nRun tests with:" -ForegroundColor Yellow
Write-Host "  cd apps/api/tests/Api.Tests" -ForegroundColor White
Write-Host "  dotnet test --parallel --max-cpu-count 4" -ForegroundColor White

Write-Host "`nFor more details, see:" -ForegroundColor Yellow
Write-Host "  docs/05-testing/INTEGRATION_TEST_OPTIMIZATION.md" -ForegroundColor White
