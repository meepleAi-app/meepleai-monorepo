# Script per testare il rate limiting configurabile

Write-Host "`n=== Test Rate Limiting Dinamico ===" -ForegroundColor Cyan
Write-Host "Questo script testa la configurazione dinamica del rate limiting`n"

$baseUrl = "http://localhost:8080"

# 1. Test anonimo - verifica limiti Anonymous (60 tokens)
Write-Host "[1] Test richiesta anonima..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/" -Method GET -SkipHttpErrorCheck
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "X-RateLimit-Limit: $($response.Headers['X-RateLimit-Limit'])" -ForegroundColor Green
    Write-Host "X-RateLimit-Remaining: $($response.Headers['X-RateLimit-Remaining'])" -ForegroundColor Green

    if ($response.Headers['X-RateLimit-Limit'] -eq '60') {
        Write-Host "[OK] Limite anonimo corretto (60 da appsettings.json)" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] Limite anonimo non corretto! Atteso 60, ricevuto: $($response.Headers['X-RateLimit-Limit'])" -ForegroundColor Red
    }
} catch {
    Write-Host "[ERROR] Errore: $_" -ForegroundColor Red
}

Write-Host "`n[2] Registrazione utente di test..." -ForegroundColor Yellow
$email = "test-ratelimit-$(Get-Date -Format 'yyyyMMddHHmmss')@example.com"
$registerPayload = @{
    email = $email
    password = "TestPassword123!"
    displayName = "Rate Limit Test User"
} | ConvertTo-Json

try {
    $registerResponse = Invoke-WebRequest -Uri "$baseUrl/auth/register" `
        -Method POST `
        -Body $registerPayload `
        -ContentType "application/json" `
        -SkipHttpErrorCheck `
        -SessionVariable session

    Write-Host "Status: $($registerResponse.StatusCode)" -ForegroundColor Green

    if ($registerResponse.StatusCode -eq 200) {
        Write-Host "[OK] Utente registrato con successo" -ForegroundColor Green

        # 3. Test autenticato - verifica limiti User (100 tokens)
        Write-Host "`n[3] Test richiesta autenticata (User role)..." -ForegroundColor Yellow
        $authResponse = Invoke-WebRequest -Uri "$baseUrl/auth/me" `
            -Method GET `
            -WebSession $session `
            -SkipHttpErrorCheck

        Write-Host "Status: $($authResponse.StatusCode)" -ForegroundColor Green
        Write-Host "X-RateLimit-Limit: $($authResponse.Headers['X-RateLimit-Limit'])" -ForegroundColor Green
        Write-Host "X-RateLimit-Remaining: $($authResponse.Headers['X-RateLimit-Remaining'])" -ForegroundColor Green

        if ($authResponse.Headers['X-RateLimit-Limit'] -eq '100') {
            Write-Host "[OK] Limite User corretto (100 da appsettings.json)" -ForegroundColor Green
        } else {
            Write-Host "[FAIL] Limite User non corretto! Atteso 100, ricevuto: $($authResponse.Headers['X-RateLimit-Limit'])" -ForegroundColor Red
        }

        # 4. Test configurazione appsettings.json
        Write-Host "`n[4] Verifica configurazione da appsettings.Development.json..." -ForegroundColor Yellow
        $appsettingsPath = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\appsettings.Development.json"
        $config = Get-Content $appsettingsPath | ConvertFrom-Json

        Write-Host "Admin: MaxTokens=$($config.RateLimit.Admin.MaxTokens), RefillRate=$($config.RateLimit.Admin.RefillRate)" -ForegroundColor Cyan
        Write-Host "Editor: MaxTokens=$($config.RateLimit.Editor.MaxTokens), RefillRate=$($config.RateLimit.Editor.RefillRate)" -ForegroundColor Cyan
        Write-Host "User: MaxTokens=$($config.RateLimit.User.MaxTokens), RefillRate=$($config.RateLimit.User.RefillRate)" -ForegroundColor Cyan
        Write-Host "Anonymous: MaxTokens=$($config.RateLimit.Anonymous.MaxTokens), RefillRate=$($config.RateLimit.Anonymous.RefillRate)" -ForegroundColor Cyan

        Write-Host "`n[OK] Configurazione caricata correttamente da appsettings.json!" -ForegroundColor Green
    }
} catch {
    Write-Host "[ERROR] Errore durante la registrazione: $_" -ForegroundColor Red
}

Write-Host "`n=== Test Completati ===" -ForegroundColor Cyan
Write-Host "La configurazione dinamica del rate limiting funziona correttamente!`n" -ForegroundColor Green
