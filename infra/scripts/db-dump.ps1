# infra/scripts/db-dump.ps1
# Exports production database, excluding sensitive tables
# Usage: pwsh db-dump.ps1 [-OutputFile dump.sql] [-ExcludeSensitive]
param(
    [string]$OutputFile = "meepleai-dump-$(Get-Date -Format 'yyyy-MM-dd-HHmmss').sql",
    [switch]$ExcludeSensitive = $true
)

$secretFile = Join-Path $PSScriptRoot ".." "secrets" "database.secret"
if (-not (Test-Path $secretFile)) {
    Write-Error "database.secret not found at $secretFile"
    exit 1
}

# Read connection string from secret
$envVars = Get-Content $secretFile | Where-Object { $_ -match '=' } | ForEach-Object {
    $parts = $_ -split '=', 2
    @{ Name = $parts[0].Trim(); Value = $parts[1].Trim() }
}

$dbHost = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_HOST' }).Value ?? 'localhost'
$dbPort = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_PORT' }).Value ?? '5432'
$dbName = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_DB' }).Value ?? 'meepleai'
$dbUser = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_USER' }).Value ?? 'meepleai'

# Tables to exclude (contain sensitive data)
$excludeTables = @(
    "Users",
    "Sessions",
    "RefreshTokens",
    "ApiKeys",
    "AuditLogs"
)

$excludeArgs = if ($ExcludeSensitive) {
    $excludeTables | ForEach-Object { "--exclude-table-data=$_" }
} else { @() }

Write-Host "Dumping database $dbName from ${dbHost}:${dbPort}..."
Write-Host "Output: $OutputFile"
if ($ExcludeSensitive) { Write-Host "Excluding sensitive data from: $($excludeTables -join ', ')" }

$env:PGPASSWORD = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_PASSWORD' }).Value

& pg_dump -h $dbHost -p $dbPort -U $dbUser -d $dbName `
    --format=plain --no-owner --no-privileges `
    @excludeArgs `
    --file=$OutputFile

if ($LASTEXITCODE -eq 0) {
    $size = (Get-Item $OutputFile).Length / 1MB
    Write-Host "Dump completed: $OutputFile ($([math]::Round($size, 2)) MB)"
} else {
    Write-Error "pg_dump failed with exit code $LASTEXITCODE"
    exit 1
}
