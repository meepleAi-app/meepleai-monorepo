# Runbook Validation Test Script (PowerShell)
# Issue #2004: Enable testing of high-error-rate.md and error-spike.md runbooks
#
# Prerequisites:
# - API running on localhost:8080
# - Admin user credentials (default: admin@meepleai.dev / admin123)
# - Prometheus on localhost:9090
#
# Usage:
#   .\scripts\testing\test-runbooks.ps1 high-error-rate
#   .\scripts\testing\test-runbooks.ps1 error-spike
#   .\scripts\testing\test-runbooks.ps1 all

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('high-error-rate', 'error-spike', 'all')]
    [string]$TestType,

    [string]$ApiBase = "http://localhost:8080",
    [string]$PrometheusUrl = "http://localhost:9090",
    [string]$AdminEmail = "admin@meepleai.dev",
    [string]$AdminPassword = "admin123"
)

$ErrorActionPreference = 'Stop'
$CookieFile = Join-Path $env:TEMP "meepleai-test-session.txt"

# =============================================================================
# Helper Functions
# =============================================================================

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

# Check prerequisites
function Test-Prerequisites {
    Write-Info "Checking prerequisites..."

    # Check API
    try {
        Invoke-RestMethod -Uri "$ApiBase/health" -Method Get -TimeoutSec 5 | Out-Null
    }
    catch {
        Write-ErrorMsg "API is not running on $ApiBase"
        exit 1
    }

    # Check Prometheus
    try {
        Invoke-RestMethod -Uri "$PrometheusUrl/-/ready" -Method Get -TimeoutSec 5 | Out-Null
    }
    catch {
        Write-Warning "Prometheus is not running on $PrometheusUrl (alert validation will be skipped)"
    }

    Write-Success "Prerequisites check passed"
}

# Login as admin
function Invoke-AdminLogin {
    Write-Info "Logging in as admin..."

    # Remove old cookie file
    if (Test-Path $CookieFile) {
        Remove-Item $CookieFile -Force
    }

    $body = @{
        email    = $AdminEmail
        password = $AdminPassword
    } | ConvertTo-Json

    try {
        $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $response = Invoke-RestMethod -Uri "$ApiBase/api/v1/auth/login" `
            -Method Post `
            -Body $body `
            -ContentType "application/json" `
            -WebSession $session

        # Save session for reuse
        $script:WebSession = $session
        Write-Success "Logged in as admin"
    }
    catch {
        Write-ErrorMsg "Login failed: $_"
        exit 1
    }
}

# Test high-error-rate runbook
function Test-HighErrorRate {
    Write-Info "Testing high-error-rate.md runbook..."
    Write-Info "Objective: Trigger HighErrorRate alert (> 1 error/sec)"

    $totalErrors = 200
    $durationSeconds = 120
    $delay = $durationSeconds / $totalErrors

    Write-Info "Generating $totalErrors errors over $durationSeconds seconds..."
    Write-Info "Delay between requests: ${delay}s (1.67 errors/sec)"

    $successCount = 0
    $errorCount = 0

    for ($i = 1; $i -le $totalErrors; $i++) {
        try {
            $body = @{ errorType = "500" } | ConvertTo-Json

            Invoke-RestMethod -Uri "$ApiBase/api/v1/test/error" `
                -Method Post `
                -Body $body `
                -ContentType "application/json" `
                -WebSession $script:WebSession `
                -TimeoutSec 30 `
                -ErrorAction SilentlyContinue

            $successCount++
        }
        catch {
            $errorCount++
            if ($i % 20 -eq 0) {
                Write-Warning "Request $i failed"
            }
        }

        if ($i % 20 -eq 0) {
            Write-Info "Progress: $i/$totalErrors errors generated"
        }

        Start-Sleep -Seconds $delay
    }

    Write-Success "Error generation complete: $successCount simulated, $errorCount failed"

    Write-Info "Waiting 130 seconds for HighErrorRate alert to fire..."
    Start-Sleep -Seconds 130

    Test-PrometheusAlert "HighErrorRate"
}

# Test error-spike runbook
function Test-ErrorSpike {
    Write-Info "Testing error-spike.md runbook..."
    Write-Info "Objective: Trigger ErrorSpike alert (3x baseline increase)"

    $totalErrors = 200
    Write-Info "Generating $totalErrors errors in rapid burst..."

    $jobs = @()
    for ($i = 1; $i -le $totalErrors; $i++) {
        $job = Start-Job -ScriptBlock {
            param($apiBase, $session)

            $body = @{ errorType = "exception" } | ConvertTo-Json

            try {
                Invoke-RestMethod -Uri "$apiBase/api/v1/test/error" `
                    -Method Post `
                    -Body $body `
                    -ContentType "application/json" `
                    -WebSession $session `
                    -TimeoutSec 30 `
                    -ErrorAction SilentlyContinue | Out-Null
            }
            catch {
                # Suppress errors
            }
        } -ArgumentList $ApiBase, $script:WebSession

        $jobs += $job

        if ($i % 50 -eq 0) {
            Write-Info "Progress: $i/$totalErrors errors generated"
        }
    }

    Write-Info "Waiting for background requests to complete (timeout: 180s)..."

    $jobs | Wait-Job -Timeout 180 | Out-Null
    $completedCount = ($jobs | Where-Object { $_.State -eq 'Completed' }).Count
    $jobs | Remove-Job -Force

    Write-Success "Error spike generation complete: $completedCount/$totalErrors completed"

    Write-Info "Waiting 310 seconds for ErrorSpike alert to fire..."
    Start-Sleep -Seconds 310

    Test-PrometheusAlert "ErrorSpike"
}

# Check Prometheus alert
function Test-PrometheusAlert {
    param([string]$AlertName)

    Write-Info "Checking Prometheus alert: $AlertName..."

    try {
        $response = Invoke-RestMethod -Uri "$PrometheusUrl/api/v1/alerts" -Method Get -TimeoutSec 5
        $alerts = $response.data.alerts | Where-Object { $_.labels.alertname -eq $AlertName }

        if ($alerts) {
            $state = $alerts[0].state

            if ($state -eq "firing") {
                Write-Success "Alert $AlertName is FIRING"
            }
            elseif ($state -eq "pending") {
                Write-Warning "Alert $AlertName is PENDING (may fire soon)"
            }
            else {
                Write-Warning "Alert $AlertName state: $state"
            }
        }
        else {
            Write-Warning "Alert $AlertName not found in Prometheus"
            Write-Info "Check Prometheus manually: $PrometheusUrl/alerts"
        }
    }
    catch {
        Write-Warning "Prometheus not available, skipping alert validation"
    }
}

# Cleanup
function Invoke-Cleanup {
    Write-Info "Cleaning up..."

    if (Test-Path $CookieFile) {
        Remove-Item $CookieFile -Force
    }

    Write-Success "Cleanup complete"
}

# =============================================================================
# Main
# =============================================================================

try {
    Test-Prerequisites
    Invoke-AdminLogin

    switch ($TestType) {
        'high-error-rate' {
            Test-HighErrorRate
        }
        'error-spike' {
            Test-ErrorSpike
        }
        'all' {
            Test-HighErrorRate
            Write-Host ""
            Test-ErrorSpike
        }
    }

    Invoke-Cleanup

    Write-Success "Runbook test complete!"
    Write-Info "Next steps:"
    Write-Info "1. Check Prometheus alerts: $PrometheusUrl/alerts"
    Write-Info "2. Check Grafana dashboards: http://localhost:3001"
    Write-Info "3. Verify API logs: docker compose logs api --tail 50"
}
catch {
    Write-ErrorMsg "Test failed: $_"
    Invoke-Cleanup
    exit 1
}
