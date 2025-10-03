#!/usr/bin/env pwsh
#
# test-coverage.ps1 - Run tests and generate coverage reports for all projects
#

param(
    [switch]$BackendOnly,
    [switch]$FrontendOnly,
    [switch]$OpenReport
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot

function Write-Header {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " $Message" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Test-Backend {
    Write-Header "Running Backend Tests with Coverage"

    Push-Location "$rootDir/apps/api"
    try {
        # Run tests with coverage
        Write-Host "Running .NET tests..." -ForegroundColor Yellow
        dotnet test `
            --collect:"XPlat Code Coverage" `
            --results-directory "./TestResults" `
            --logger "console;verbosity=normal"

        # Generate HTML report
        Write-Host "`nGenerating coverage report..." -ForegroundColor Yellow
        reportgenerator `
            -reports:"TestResults/**/coverage.cobertura.xml" `
            -targetdir:"TestResults/CoverageReport" `
            -reporttypes:"Html;TextSummary;Cobertura"

        # Display summary
        if (Test-Path "TestResults/CoverageReport/Summary.txt") {
            Write-Host "`n--- Backend Coverage Summary ---" -ForegroundColor Green
            Get-Content "TestResults/CoverageReport/Summary.txt"
        }

        if ($OpenReport -and (Test-Path "TestResults/CoverageReport/index.html")) {
            Write-Host "`nOpening coverage report..." -ForegroundColor Yellow
            Start-Process "TestResults/CoverageReport/index.html"
        }
    }
    finally {
        Pop-Location
    }
}

function Test-Frontend {
    Write-Header "Running Frontend Tests with Coverage"

    Push-Location "$rootDir/apps/web"
    try {
        Write-Host "Running Jest tests with coverage..." -ForegroundColor Yellow
        npm run test:coverage

        if ($OpenReport -and (Test-Path "coverage/lcov-report/index.html")) {
            Write-Host "`nOpening coverage report..." -ForegroundColor Yellow
            Start-Process "coverage/lcov-report/index.html"
        }
    }
    finally {
        Pop-Location
    }
}

# Main execution
if ($BackendOnly) {
    Test-Backend
}
elseif ($FrontendOnly) {
    Test-Frontend
}
else {
    Test-Backend
    Test-Frontend
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host " Coverage reports generated successfully!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

if (-not $BackendOnly) {
    Write-Host "Frontend coverage report: apps/web/coverage/lcov-report/index.html" -ForegroundColor Cyan
}
if (-not $FrontendOnly) {
    Write-Host "Backend coverage report: apps/api/TestResults/CoverageReport/index.html" -ForegroundColor Cyan
}
