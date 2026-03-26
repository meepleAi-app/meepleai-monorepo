#!/usr/bin/env pwsh
# setup-secrets.ps1 — Generate placeholder .secret files from .example templates
# Usage: pwsh -File setup-secrets.ps1
# Does NOT overwrite existing .secret files.

$ErrorActionPreference = 'Stop'
$SecretsDir = $PSScriptRoot

Write-Host "`n=== MeepleAI Secrets Setup ===" -ForegroundColor Cyan
Write-Host "Generating placeholder secrets in: $SecretsDir`n"

$created = 0
$skipped = 0

# Generate from .secret.example templates
Get-ChildItem -Path $SecretsDir -Filter '*.secret.example' | ForEach-Object {
    $targetName = $_.Name -replace '\.example$', ''
    $targetPath = Join-Path $SecretsDir $targetName

    if (Test-Path $targetPath) {
        Write-Host "  SKIP  $targetName (already exists)" -ForegroundColor DarkGray
        $skipped++
    } else {
        Copy-Item $_.FullName $targetPath
        Write-Host "  CREATE  $targetName (from template)" -ForegroundColor Green
        $created++
    }
}

# Generate services-auth.secret if missing (no .example template — dynamic content)
$servicesAuthPath = Join-Path $SecretsDir 'services-auth.secret'
if (-not (Test-Path $servicesAuthPath)) {
    $token = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 64 | ForEach-Object { [char]$_ })
    @"
# Services authentication token (auto-generated)
SERVICES_AUTH_TOKEN=$token
"@ | Set-Content -Path $servicesAuthPath -Encoding UTF8
    Write-Host "  CREATE  services-auth.secret (generated)" -ForegroundColor Green
    $created++
} else {
    Write-Host "  SKIP  services-auth.secret (already exists)" -ForegroundColor DarkGray
    $skipped++
}

Write-Host "`n--- Summary ---"
Write-Host "Created: $created | Skipped: $skipped" -ForegroundColor Cyan
Write-Host ""

if ($created -gt 0) {
    Write-Host "Placeholder secrets created. To get real values from staging:" -ForegroundColor Yellow
    Write-Host "  cd infra && make secrets-sync" -ForegroundColor Yellow
}
