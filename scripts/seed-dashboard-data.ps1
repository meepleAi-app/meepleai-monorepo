#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Seed dashboard test data - Issue #4576

.DESCRIPTION
    Popola il database PostgreSQL con dati di test per la Gaming Hub Dashboard:
    - 3 utenti (Marco, Sara, Luca)
    - 20 giochi nel catalogo condiviso
    - Play records di esempio

.PARAMETER ConnectionString
    PostgreSQL connection string (default: localhost:5432)

.PARAMETER UseDocker
    Use Docker container instead of local PostgreSQL

.EXAMPLE
    .\seed-dashboard-data.ps1
    .\seed-dashboard-data.ps1 -UseDocker
    .\seed-dashboard-data.ps1 -ConnectionString "Host=localhost;Port=5432;Database=meepleai;..."
#>

param(
    [string]$ConnectionString = "Host=localhost;Port=5432;Database=meepleai;Username=postgres;Password=postgres",
    [switch]$UseDocker
)

$ErrorActionPreference = "Stop"

Write-Host "🎲 Gaming Hub Dashboard - Data Seeder" -ForegroundColor Cyan
Write-Host ""

$scriptPath = Join-Path $PSScriptRoot "seed-dashboard-data.sql"

if (-not (Test-Path $scriptPath)) {
    Write-Host "❌ SQL script not found: $scriptPath" -ForegroundColor Red
    exit 1
}

if ($UseDocker) {
    Write-Host "🐳 Using Docker container..." -ForegroundColor Yellow

    # Check if container exists
    $container = docker ps --filter "name=meepleai-postgres" --format "{{.Names}}"

    if (-not $container) {
        Write-Host "❌ Docker container 'meepleai-postgres' not found" -ForegroundColor Red
        Write-Host "Start it with: docker compose up -d postgres" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "📡 Executing seed script in container..." -ForegroundColor Yellow
    Get-Content $scriptPath | docker exec -i meepleai-postgres psql -U postgres -d meepleai

} else {
    Write-Host "📡 Using local PostgreSQL..." -ForegroundColor Yellow

    # Parse connection string
    $parts = $ConnectionString -split ';'
    $host = ($parts | Where-Object { $_ -match '^Host=' }) -replace 'Host=', ''
    $port = ($parts | Where-Object { $_ -match '^Port=' }) -replace 'Port=', ''
    $database = ($parts | Where-Object { $_ -match '^Database=' }) -replace 'Database=', ''
    $username = ($parts | Where-Object { $_ -match '^Username=' }) -replace 'Username=', ''
    $password = ($parts | Where-Object { $_ -match '^Password=' }) -replace 'Password=', ''

    if (-not $host) { $host = 'localhost' }
    if (-not $port) { $port = '5432' }
    if (-not $database) { $database = 'meepleai' }
    if (-not $username) { $username = 'postgres' }

    # Set PGPASSWORD for password-less auth
    $env:PGPASSWORD = $password

    Write-Host "📡 Executing seed script..." -ForegroundColor Yellow
    Get-Content $scriptPath | psql -h $host -p $port -U $username -d $database

    # Clear password
    $env:PGPASSWORD = $null
}

Write-Host ""
Write-Host "✅ Done!" -ForegroundColor Green
