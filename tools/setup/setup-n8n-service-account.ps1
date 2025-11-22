# N8N-01: Setup n8n Service Account
# This script creates a service account for n8n webhook authentication
# and stores the session token for use in workflows

param(
    [Parameter(Mandatory=$false)]
    [string]$ApiBaseUrl = "http://localhost:8080",

    [Parameter(Mandatory=$false)]
    [string]$ServiceEmail = "n8n-service@meepleai.dev",

    [Parameter(Mandatory=$false)]
    [string]$ServicePassword,

    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "infra/env/n8n-service-session.env"
)

$ErrorActionPreference = "Stop"

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "N8N-01: n8n Service Account Setup" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""

# Generate strong password if not provided
if (-not $ServicePassword) {
    Write-Host "Generating strong password for service account..." -ForegroundColor Yellow
    $ServicePassword = -join ((48..57) + (65..90) + (97..122) + (33, 35, 36, 37, 38, 42, 43, 45, 61, 63, 64) | Get-Random -Count 32 | ForEach-Object {[char]$_})
    Write-Host "Password generated (save this securely): $ServicePassword" -ForegroundColor Green
}

# Step 1: Check if service account already exists
Write-Host "Step 1: Checking if service account exists..." -ForegroundColor Cyan

try {
    # Try to login first to see if account exists
    $loginPayload = @{
        email = $ServiceEmail
        password = $ServicePassword
    } | ConvertTo-Json

    $loginResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $loginPayload `
        -SessionVariable session `
        -ErrorAction SilentlyContinue

    if ($loginResponse.StatusCode -eq 200) {
        Write-Host "✓ Service account already exists and credentials are valid" -ForegroundColor Green
        $accountExists = $true
    }
} catch {
    Write-Host "✓ Service account does not exist yet" -ForegroundColor Yellow
    $accountExists = $false
}

# Step 2: Create service account if it doesn't exist
if (-not $accountExists) {
    Write-Host ""
    Write-Host "Step 2: Creating service account..." -ForegroundColor Cyan

    $registerPayload = @{
        email = $ServiceEmail
        password = $ServicePassword
        displayName = "n8n Service Account"
        role = "User"
    } | ConvertTo-Json

    try {
        $registerResponse = Invoke-WebRequest `
            -Uri "$ApiBaseUrl/auth/register" `
            -Method POST `
            -ContentType "application/json" `
            -Body $registerPayload `
            -SessionVariable session

        if ($registerResponse.StatusCode -eq 200) {
            Write-Host "✓ Service account created successfully" -ForegroundColor Green
        }
    } catch {
        Write-Host "✗ Failed to create service account: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host ""
    Write-Host "Step 2: Skipped (account exists)" -ForegroundColor Yellow
}

# Step 3: Authenticate and get session token
Write-Host ""
Write-Host "Step 3: Authenticating and retrieving session token..." -ForegroundColor Cyan

$loginPayload = @{
    email = $ServiceEmail
    password = $ServicePassword
} | ConvertTo-Json

try {
    $authResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $loginPayload `
        -SessionVariable session

    if ($authResponse.StatusCode -eq 200) {
        # Extract session cookie
        $sessionCookie = $session.Cookies.GetCookies("$ApiBaseUrl") | Where-Object { $_.Name -eq "session" }

        if ($sessionCookie) {
            $sessionToken = $sessionCookie.Value
            Write-Host "✓ Session token retrieved" -ForegroundColor Green
            Write-Host "  Token (first 20 chars): $($sessionToken.Substring(0, [Math]::Min(20, $sessionToken.Length)))..." -ForegroundColor Gray
        } else {
            Write-Host "✗ No session cookie found in response" -ForegroundColor Red
            exit 1
        }
    }
} catch {
    Write-Host "✗ Authentication failed: $_" -ForegroundColor Red
    exit 1
}

# Step 4: Verify session is valid
Write-Host ""
Write-Host "Step 4: Verifying session..." -ForegroundColor Cyan

try {
    $headers = @{
        "Cookie" = "session=$sessionToken"
    }

    $meResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/auth/me" `
        -Method GET `
        -Headers $headers

    if ($meResponse.StatusCode -eq 200) {
        $userData = $meResponse.Content | ConvertFrom-Json
        Write-Host "✓ Session valid" -ForegroundColor Green
        Write-Host "  User: $($userData.displayName) ($($userData.email))" -ForegroundColor Gray
        Write-Host "  Role: $($userData.role)" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Session validation failed: $_" -ForegroundColor Red
    exit 1
}

# Step 5: Save to environment file
Write-Host ""
Write-Host "Step 5: Saving configuration..." -ForegroundColor Cyan

$envContent = @"
# N8N-01: Service Account Session Token
# Generated on: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
# Service Account: $ServiceEmail

# Session token for n8n workflows
N8N_SERVICE_SESSION=$sessionToken

# Service account credentials (store securely!)
N8N_SERVICE_EMAIL=$ServiceEmail
N8N_SERVICE_PASSWORD=$ServicePassword
"@

try {
    # Create directory if it doesn't exist
    $outputDir = Split-Path -Parent $OutputFile
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }

    # Write to file
    $envContent | Out-File -FilePath $OutputFile -Encoding UTF8 -Force

    Write-Host "✓ Configuration saved to: $OutputFile" -ForegroundColor Green
    Write-Host ""
    Write-Host "⚠️  IMPORTANT: Add this file to .gitignore!" -ForegroundColor Yellow
    Write-Host "⚠️  This file contains sensitive credentials!" -ForegroundColor Yellow
} catch {
    Write-Host "✗ Failed to save configuration: $_" -ForegroundColor Red
    exit 1
}

# Step 6: Summary
Write-Host ""
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Import workflow: infra/n8n/workflows/agent-explain-orchestrator.json" -ForegroundColor White
Write-Host "2. Set environment variable in n8n: N8N_SERVICE_SESSION=$sessionToken" -ForegroundColor White
Write-Host "3. Test webhook: POST http://n8n:5678/webhook/explain" -ForegroundColor White
Write-Host ""
Write-Host "Configuration file: $OutputFile" -ForegroundColor Gray
Write-Host ""
