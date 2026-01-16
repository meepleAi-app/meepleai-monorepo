# Generate Docker Compose secret .txt files from .secret files
# Extracts values and creates plain text files expected by Docker Compose

Write-Host "Generating Docker Compose secret files..." -ForegroundColor Cyan
Write-Host ""

$secretsDir = $PSScriptRoot
Set-Location $secretsDir

# Mapping: .secret file -> variable name -> .txt file
$mappings = @(
    @{Source='database.secret'; Variable='POSTGRES_PASSWORD'; Target='postgres-password.txt'},
    @{Source='redis.secret'; Variable='REDIS_PASSWORD'; Target='redis-password.txt'},
    @{Source='jwt.secret'; Variable='JWT_SECRET_KEY'; Target='jwt-secret.txt'},
    @{Source='openrouter.secret'; Variable='OPENROUTER_API_KEY'; Target='openrouter-api-key.txt'},
    @{Source='bgg.secret'; Variable='BGG_PASSWORD'; Target='bgg-api-token.txt'},
    @{Source='email.secret'; Variable='SMTP_PASSWORD'; Target='gmail-app-password.txt'},
    @{Source='monitoring.secret'; Variable='GRAFANA_ADMIN_PASSWORD'; Target='grafana-admin-password.txt'},
    @{Source='admin.secret'; Variable='ADMIN_PASSWORD'; Target='initial-admin-password.txt'},
    @{Source='oauth.secret'; Variable='GOOGLE_CLIENT_ID'; Target='google-oauth-client-id.txt'},
    @{Source='oauth.secret'; Variable='GOOGLE_CLIENT_SECRET'; Target='google-oauth-client-secret.txt'},
    @{Source='oauth.secret'; Variable='GITHUB_CLIENT_ID'; Target='github-oauth-client-id.txt'},
    @{Source='oauth.secret'; Variable='GITHUB_CLIENT_SECRET'; Target='github-oauth-client-secret.txt'},
    @{Source='oauth.secret'; Variable='DISCORD_CLIENT_ID'; Target='discord-oauth-client-id.txt'},
    @{Source='oauth.secret'; Variable='DISCORD_CLIENT_SECRET'; Target='discord-oauth-client-secret.txt'}
)

$created = 0
$skipped = 0
$errors = 0

foreach ($mapping in $mappings) {
    $sourceFile = $mapping.Source
    $variable = $mapping.Variable
    $targetFile = $mapping.Target

    if (!(Test-Path $sourceFile)) {
        Write-Host "  [SKIP] $targetFile (source $sourceFile not found)" -ForegroundColor Yellow
        $skipped++
        continue
    }

    $content = Get-Content $sourceFile -Raw
    $match = [regex]::Match($content, "$variable=([^\r\n]+)")

    if ($match.Success) {
        $value = $match.Groups[1].Value.Trim()
        Set-Content -Path $targetFile -Value $value -NoNewline

        $maskedValue = if ($value.Length -gt 12) { $value.Substring(0,8) + "***" } else { "***" }
        Write-Host "  [OK] $targetFile <- $variable=$maskedValue" -ForegroundColor Green
        $created++
    } else {
        Write-Host "  [ERROR] ${targetFile}: Variable $variable not found in $sourceFile" -ForegroundColor Red
        $errors++
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Created: $created files" -ForegroundColor Green
Write-Host "  Skipped: $skipped files" -ForegroundColor Yellow
Write-Host "  Errors:  $errors files" -ForegroundColor Red
Write-Host ""

if ($created -gt 0) {
    Write-Host "[SUCCESS] Docker secret files generated!" -ForegroundColor Green
    Write-Host "          Services can now use Docker secrets for sensitive data." -ForegroundColor Green
} else {
    Write-Host "[WARNING] No Docker secret files were created." -ForegroundColor Yellow
}

Write-Host ""
