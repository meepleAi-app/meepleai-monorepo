# MeepleAI - Generate .env.development from .secret files
# Issue #2570: Auto-sync local development environment
#
# Usage: .\generate-env-from-secrets.ps1 [-OutputPath <path>] [-Force]
#
# This script reads all .secret files and generates a consolidated
# .env.development file for local development (outside Docker).

[CmdletBinding()]
param(
    [string]$OutputPath = "$PSScriptRoot\..\..\..\.env.development",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "   Generate .env.development" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

# Resolve paths
$secretsDir = $PSScriptRoot
$outputFile = Resolve-Path $OutputPath -ErrorAction SilentlyContinue
if (-not $outputFile) {
    $outputFile = $OutputPath
}

Write-Host "[INFO] Secrets directory: $secretsDir" -ForegroundColor Gray
Write-Host "[INFO] Output file: $outputFile" -ForegroundColor Gray
Write-Host ""

# Check if output exists
if ((Test-Path $outputFile) -and -not $Force) {
    Write-Host "[WARN] Output file already exists: $outputFile" -ForegroundColor Yellow
    Write-Host "       Use -Force to overwrite" -ForegroundColor Yellow
    Write-Host ""
    $confirm = Read-Host "Overwrite? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "[SKIP] Aborted by user" -ForegroundColor Yellow
        exit 0
    }
}

# Collect all secrets
$allSecrets = @{}
$secretFiles = Get-ChildItem -Path $secretsDir -Filter "*.secret" -ErrorAction SilentlyContinue

if ($secretFiles.Count -eq 0) {
    Write-Host "[ERROR] No .secret files found in $secretsDir" -ForegroundColor Red
    Write-Host "        Run setup-secrets.ps1 first to create secret files" -ForegroundColor Yellow
    exit 1
}

Write-Host "[STEP 1] Reading .secret files..." -ForegroundColor Yellow
Write-Host ""

foreach ($file in $secretFiles) {
    $content = Get-Content $file.FullName -ErrorAction SilentlyContinue
    $count = 0

    foreach ($line in $content) {
        $trimmed = $line.Trim()

        # Skip empty lines and comments
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        # Parse KEY=VALUE
        $idx = $trimmed.IndexOf('=')
        if ($idx -gt 0) {
            $key = $trimmed.Substring(0, $idx).Trim()
            $value = $trimmed.Substring($idx + 1).Trim()

            # Don't overwrite if already set (first file wins)
            if (-not $allSecrets.ContainsKey($key)) {
                $allSecrets[$key] = $value
                $count++
            }
        }
    }

    Write-Host "  [OK] $($file.Name): $count variables" -ForegroundColor Green
}

Write-Host ""
Write-Host "[STEP 2] Generating .env.development..." -ForegroundColor Yellow
Write-Host ""

# Build output content with sections
$output = @()
$output += "# MeepleAI Local Development Environment"
$output += "# AUTO-GENERATED from infra/secrets/ directory"
$output += "# Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$output += "# DO NOT COMMIT THIS FILE - it is gitignored"
$output += "# Regenerate with: cd infra/secrets && .\generate-env-from-secrets.ps1"
$output += ""

# Database Configuration
$output += "# Database Connection (localhost for local dev)"
$output += "POSTGRES_HOST=localhost"
$output += "POSTGRES_PORT=5432"
if ($allSecrets.ContainsKey('POSTGRES_DB')) { $output += "POSTGRES_DB=$($allSecrets['POSTGRES_DB'])" }
if ($allSecrets.ContainsKey('POSTGRES_USER')) { $output += "POSTGRES_USER=$($allSecrets['POSTGRES_USER'])" }
if ($allSecrets.ContainsKey('POSTGRES_PASSWORD')) { $output += "POSTGRES_PASSWORD=$($allSecrets['POSTGRES_PASSWORD'])" }
$output += ""

# Redis Configuration
$output += "# Redis Configuration"
if ($allSecrets.ContainsKey('REDIS_PASSWORD')) {
    $output += "REDIS_PASSWORD=$($allSecrets['REDIS_PASSWORD'])"
    $output += "REDIS_URL=localhost:6379,password=$($allSecrets['REDIS_PASSWORD']),abortConnect=false"
}
$output += ""

# Vector Database
$output += "# Vector Database"
$output += "QDRANT_URL=http://localhost:6333"
if ($allSecrets.ContainsKey('QDRANT_API_KEY')) { $output += "QDRANT_API_KEY=$($allSecrets['QDRANT_API_KEY'])" }
$output += ""

# LLM Services
$output += "# LLM Service"
$output += "OLLAMA_URL=http://localhost:11434"
$output += ""

# API Keys
$output += "# AI API Keys"
if ($allSecrets.ContainsKey('OPENROUTER_API_KEY')) { $output += "OPENROUTER_API_KEY=$($allSecrets['OPENROUTER_API_KEY'])" }
if ($allSecrets.ContainsKey('OPENROUTER_DEFAULT_MODEL')) { $output += "OPENROUTER_DEFAULT_MODEL=$($allSecrets['OPENROUTER_DEFAULT_MODEL'])" }
$output += ""

# JWT
$output += "# JWT Configuration"
if ($allSecrets.ContainsKey('JWT_SECRET_KEY')) { $output += "JWT_SECRET_KEY=$($allSecrets['JWT_SECRET_KEY'])" }
if ($allSecrets.ContainsKey('JWT_ISSUER')) { $output += "JWT_ISSUER=$($allSecrets['JWT_ISSUER'])" }
if ($allSecrets.ContainsKey('JWT_AUDIENCE')) { $output += "JWT_AUDIENCE=$($allSecrets['JWT_AUDIENCE'])" }
$output += ""

# OAuth Providers
$output += "# Google OAuth"
if ($allSecrets.ContainsKey('GOOGLE_OAUTH_CLIENT_ID')) { $output += "GOOGLE_OAUTH_CLIENT_ID=$($allSecrets['GOOGLE_OAUTH_CLIENT_ID'])" }
if ($allSecrets.ContainsKey('GOOGLE_OAUTH_CLIENT_SECRET')) { $output += "GOOGLE_OAUTH_CLIENT_SECRET=$($allSecrets['GOOGLE_OAUTH_CLIENT_SECRET'])" }
$output += ""
$output += "# Discord OAuth"
if ($allSecrets.ContainsKey('DISCORD_OAUTH_CLIENT_ID')) { $output += "DISCORD_OAUTH_CLIENT_ID=$($allSecrets['DISCORD_OAUTH_CLIENT_ID'])" }
if ($allSecrets.ContainsKey('DISCORD_OAUTH_CLIENT_SECRET')) { $output += "DISCORD_OAUTH_CLIENT_SECRET=$($allSecrets['DISCORD_OAUTH_CLIENT_SECRET'])" }
$output += ""
$output += "# GitHub OAuth"
if ($allSecrets.ContainsKey('GITHUB_OAUTH_CLIENT_ID')) { $output += "GITHUB_OAUTH_CLIENT_ID=$($allSecrets['GITHUB_OAUTH_CLIENT_ID'])" }
if ($allSecrets.ContainsKey('GITHUB_OAUTH_CLIENT_SECRET')) { $output += "GITHUB_OAUTH_CLIENT_SECRET=$($allSecrets['GITHUB_OAUTH_CLIENT_SECRET'])" }
$output += ""

# Admin User
$output += "# Initial Admin User"
if ($allSecrets.ContainsKey('ADMIN_EMAIL')) { $output += "INITIAL_ADMIN_EMAIL=$($allSecrets['ADMIN_EMAIL'])" }
if ($allSecrets.ContainsKey('ADMIN_PASSWORD')) { $output += "INITIAL_ADMIN_PASSWORD=$($allSecrets['ADMIN_PASSWORD'])" }
if ($allSecrets.ContainsKey('ADMIN_DISPLAY_NAME')) { $output += "INITIAL_ADMIN_DISPLAY_NAME=$($allSecrets['ADMIN_DISPLAY_NAME'])" }
$output += ""

# BoardGameGeek
$output += "# BoardGameGeek API"
if ($allSecrets.ContainsKey('BGG_USERNAME')) { $output += "BGG_USERNAME=$($allSecrets['BGG_USERNAME'])" }
if ($allSecrets.ContainsKey('BGG_PASSWORD')) { $output += "BGG_PASSWORD=$($allSecrets['BGG_PASSWORD'])" }
$output += ""

# Embedding Service
$output += "# Embedding Service"
if ($allSecrets.ContainsKey('EMBEDDING_SERVICE_API_KEY')) { $output += "EMBEDDING_SERVICE_API_KEY=$($allSecrets['EMBEDDING_SERVICE_API_KEY'])" }
$output += ""

# Write output file
$outputContent = $output -join "`n"
Set-Content -Path $outputFile -Value $outputContent -Encoding UTF8 -NoNewline

Write-Host "[OK] Generated: $outputFile" -ForegroundColor Green
Write-Host "     Total variables: $($allSecrets.Count)" -ForegroundColor Gray
Write-Host ""

Write-Host "=======================================" -ForegroundColor Green
Write-Host "   Done!" -ForegroundColor Green
Write-Host "=======================================" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Review generated file for accuracy" -ForegroundColor White
Write-Host "  2. Start local services: docker compose up -d postgres redis qdrant" -ForegroundColor White
Write-Host "  3. Run API locally: cd apps/api/src/Api && dotnet run" -ForegroundColor White
Write-Host ""
