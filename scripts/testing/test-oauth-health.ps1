# Test OAuth Configuration via Health Check API (PowerShell)
# Validates that all OAuth providers are correctly configured

[CmdletBinding()]
param(
    [string]$ApiUrl = "http://localhost:8080",
    [switch]$Verbose
)

$ErrorActionPreference = 'Stop'

Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "   OAuth Health Check Test" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

# Test /health/config endpoint
Write-Host "Testing: GET $ApiUrl/health/config" -ForegroundColor Blue
Write-Host ""

try {
    $response = Invoke-RestMethod -Uri "$ApiUrl/health/config" -Method Get -ErrorAction Stop
}
catch {
    Write-Host "❌ ERROR: No response from health check endpoint" -ForegroundColor Red
    Write-Host "   Is the API running? Try: docker compose up -d api" -ForegroundColor Yellow
    exit 1
}

# Parse response
$status = $response.status
$oauthConfigured = @()
$oauthPlaceholders = @()
$oauthMisconfigured = @()

if ($response.checks -and $response.checks.Count -gt 0) {
    $check = $response.checks[0]
    if ($check.data.oauth_configured_providers) {
        $oauthConfigured = $check.data.oauth_configured_providers
    }
    if ($check.data.oauth_placeholder_providers) {
        $oauthPlaceholders = $check.data.oauth_placeholder_providers
    }
    if ($check.data.oauth_misconfigured_providers) {
        $oauthMisconfigured = $check.data.oauth_misconfigured_providers
    }
}

# Display results
Write-Host "=== Health Check Status ===" -ForegroundColor Cyan
if ($status -eq "Healthy") {
    Write-Host "Overall Status: ✅ Healthy" -ForegroundColor Green
}
elseif ($status -eq "Degraded") {
    Write-Host "Overall Status: ⚠️  Degraded" -ForegroundColor Yellow
}
else {
    Write-Host "Overall Status: ❌ Unhealthy" -ForegroundColor Red
}
Write-Host ""

Write-Host "=== OAuth Providers ===" -ForegroundColor Cyan
if ($oauthConfigured.Count -gt 0) {
    Write-Host "✅ Configured: $($oauthConfigured -join ', ')" -ForegroundColor Green
}
else {
    Write-Host "❌ No providers configured" -ForegroundColor Red
}

if ($oauthPlaceholders.Count -gt 0) {
    Write-Host "⚠️  Placeholders: $($oauthPlaceholders -join ', ')" -ForegroundColor Yellow
}

if ($oauthMisconfigured.Count -gt 0) {
    Write-Host "❌ Misconfigured: $($oauthMisconfigured -join ', ')" -ForegroundColor Red
}
Write-Host ""

# Display masked client IDs
Write-Host "=== OAuth Client IDs (Masked) ===" -ForegroundColor Cyan
if ($response.checks -and $response.checks.Count -gt 0) {
    $check = $response.checks[0]

    $googleId = $check.data.oauth_google_client_id
    $discordId = $check.data.oauth_discord_client_id
    $githubId = $check.data.oauth_github_client_id

    if ($googleId) {
        Write-Host "Google:  $googleId" -ForegroundColor Green
    }

    if ($discordId) {
        Write-Host "Discord: $discordId" -ForegroundColor Green
    }

    if ($githubId) {
        Write-Host "GitHub:  $githubId" -ForegroundColor Green
    }
}
Write-Host ""

# Display warnings if any
if ($response.checks -and $response.checks.Count -gt 0) {
    $warnings = $response.checks[0].data.warnings
    if ($warnings -and $warnings.Count -gt 0) {
        Write-Host "=== Warnings ===" -ForegroundColor Cyan
        foreach ($warning in $warnings) {
            Write-Host "⚠️  $warning" -ForegroundColor Yellow
        }
        Write-Host ""
    }
}

# Full JSON response (optional)
if ($Verbose) {
    Write-Host "=== Full Response ===" -ForegroundColor Cyan
    $response | ConvertTo-Json -Depth 10
    Write-Host ""
}

# Summary
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "   Summary" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan

if ($oauthConfigured.Count -eq 3 -and $oauthPlaceholders.Count -eq 0) {
    Write-Host "✅ All OAuth providers (3/3) are properly configured" -ForegroundColor Green
    exit 0
}
elseif ($oauthConfigured.Count -gt 0) {
    Write-Host "⚠️  Partial configuration: $($oauthConfigured.Count)/3 providers configured" -ForegroundColor Yellow
    exit 0
}
else {
    Write-Host "❌ No OAuth providers configured" -ForegroundColor Red
    exit 1
}
