# infra/scripts/db-restore.ps1
# Restores a database dump to a target environment
# Usage: pwsh db-restore.ps1 -InputFile dump.sql [-TargetDb meepleai_staging]
param(
    [Parameter(Mandatory)]
    [string]$InputFile,
    [string]$TargetDb = "meepleai_staging",
    [switch]$DropExisting = $false
)

if (-not (Test-Path $InputFile)) {
    Write-Error "Input file not found: $InputFile"
    exit 1
}

$secretFile = Join-Path $PSScriptRoot ".." "secrets" "database.secret"
if (-not (Test-Path $secretFile)) {
    Write-Error "database.secret not found at $secretFile"
    exit 1
}

$envVars = Get-Content $secretFile | Where-Object { $_ -match '=' } | ForEach-Object {
    $parts = $_ -split '=', 2
    @{ Name = $parts[0].Trim(); Value = $parts[1].Trim() }
}

$dbHost = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_HOST' }).Value ?? 'localhost'
$dbPort = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_PORT' }).Value ?? '5432'
$dbUser = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_USER' }).Value ?? 'meepleai'
$env:PGPASSWORD = ($envVars | Where-Object { $_.Name -eq 'POSTGRES_PASSWORD' }).Value

Write-Host "Restoring $InputFile to $TargetDb on ${dbHost}:${dbPort}..."

if ($DropExisting) {
    Write-Host "WARNING: Dropping existing database $TargetDb in 5 seconds... (Ctrl+C to cancel)"
    Start-Sleep -Seconds 5
    & psql -h $dbHost -p $dbPort -U $dbUser -c "DROP DATABASE IF EXISTS $TargetDb;"
    & psql -h $dbHost -p $dbPort -U $dbUser -c "CREATE DATABASE $TargetDb;"
}

& psql -h $dbHost -p $dbPort -U $dbUser -d $TargetDb -f $InputFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "Restore completed successfully to $TargetDb"
} else {
    Write-Error "Restore failed with exit code $LASTEXITCODE"
    exit 1
}
