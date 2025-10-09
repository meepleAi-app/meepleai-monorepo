#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Measure code coverage for MeepleAI monorepo projects

.DESCRIPTION
    This script runs tests with coverage collection for both the API and Web projects.
    It generates coverage reports in multiple formats and optionally creates HTML reports.

.PARAMETER Project
    Which project to measure coverage for. Valid values: 'api', 'web', 'all' (default: 'all')

.PARAMETER Format
    Coverage report format. Valid values: 'cobertura', 'lcov', 'json', 'opencover' (default: 'cobertura')

.PARAMETER GenerateHtml
    Generate HTML coverage report using reportgenerator tool

.PARAMETER SkipTests
    Skip running tests (only generate reports from existing coverage data)

.PARAMETER Filter
    Test filter pattern (e.g., 'FullyQualifiedName~QdrantServiceTests')

.EXAMPLE
    .\measure-coverage.ps1
    Runs coverage for all projects

.EXAMPLE
    .\measure-coverage.ps1 -Project api -GenerateHtml
    Runs API coverage and generates HTML report

.EXAMPLE
    .\measure-coverage.ps1 -Project api -Filter "FullyQualifiedName~ServiceTests"
    Runs only service tests for API coverage

.EXAMPLE
    .\measure-coverage.ps1 -Project web
    Runs only web project coverage
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('api', 'web', 'all')]
    [string]$Project = 'all',

    [Parameter()]
    [ValidateSet('cobertura', 'lcov', 'json', 'opencover')]
    [string]$Format = 'cobertura',

    [Parameter()]
    [switch]$GenerateHtml,

    [Parameter()]
    [switch]$SkipTests,

    [Parameter()]
    [string]$Filter
)

$ErrorActionPreference = 'Stop'
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmmss"
$rootDir = Split-Path $PSScriptRoot -Parent

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  MeepleAI Code Coverage Measurement" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "Timestamp: $timestamp" -ForegroundColor Gray
Write-Host "Project: $Project" -ForegroundColor Gray
Write-Host "Format: $Format" -ForegroundColor Gray
Write-Host ""

function Measure-ApiCoverage {
    Write-Host "[API] Starting coverage measurement..." -ForegroundColor Yellow

    $apiDir = Join-Path $rootDir "apps\api"
    $coverageDir = Join-Path $apiDir "tests\Api.Tests\coverage"

    # Create coverage directory if it doesn't exist
    if (-not (Test-Path $coverageDir)) {
        New-Item -ItemType Directory -Path $coverageDir -Force | Out-Null
    }

    $outputFile = Join-Path $coverageDir "coverage-$timestamp"

    if (-not $SkipTests) {
        Write-Host "[API] Running tests with coverage collection..." -ForegroundColor Gray

        Push-Location $apiDir
        try {
            $testArgs = @(
                "test",
                "-p:CollectCoverage=true",
                "-p:CoverletOutputFormat=$Format",
                "-p:CoverletOutput=$outputFile"
            )

            if ($Filter) {
                $testArgs += "--filter"
                $testArgs += $Filter
                Write-Host "[API] Using filter: $Filter" -ForegroundColor Gray
            }

            Write-Host "[API] Running: dotnet $($testArgs -join ' ')" -ForegroundColor DarkGray

            $output = & dotnet @testArgs 2>&1

            # Display output
            $output | ForEach-Object { Write-Host $_ }

            if ($LASTEXITCODE -ne 0) {
                Write-Host "[API] Tests failed or coverage collection failed!" -ForegroundColor Red
                Write-Host "[API] Note: Tests with Testcontainers may take 10+ minutes" -ForegroundColor Yellow
                return $false
            }

            Write-Host "[API] ✓ Tests completed successfully" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }

    # Find the most recent coverage file
    $extension = switch ($Format) {
        'cobertura' { '.cobertura.xml' }
        'lcov' { '.info' }
        'json' { '.json' }
        'opencover' { '.opencover.xml' }
    }

    $coverageFile = Get-ChildItem -Path $coverageDir -Filter "*$extension" |
                    Sort-Object LastWriteTime -Descending |
                    Select-Object -First 1

    if ($coverageFile) {
        Write-Host "[API] Coverage report: $($coverageFile.FullName)" -ForegroundColor Green

        if ($GenerateHtml) {
            Write-Host "[API] Generating HTML report..." -ForegroundColor Gray
            $htmlDir = Join-Path $coverageDir "html"

            # Check if reportgenerator is installed
            $hasReportGenerator = $null -ne (Get-Command reportgenerator -ErrorAction SilentlyContinue)

            if (-not $hasReportGenerator) {
                Write-Host "[API] Installing reportgenerator tool..." -ForegroundColor Yellow
                dotnet tool install -g dotnet-reportgenerator-globaltool
            }

            reportgenerator "-reports:$($coverageFile.FullName)" "-targetdir:$htmlDir" "-reporttypes:Html"

            if ($LASTEXITCODE -eq 0) {
                $indexHtml = Join-Path $htmlDir "index.html"
                Write-Host "[API] ✓ HTML report generated: $indexHtml" -ForegroundColor Green

                # Optionally open in browser
                if ($IsWindows -or $PSVersionTable.Platform -eq 'Win32NT') {
                    Write-Host "[API] Opening report in browser..." -ForegroundColor Gray
                    Start-Process $indexHtml
                }
            }
        }
    }
    else {
        Write-Host "[API] ⚠ No coverage file found!" -ForegroundColor Yellow
        return $false
    }

    return $true
}

function Measure-WebCoverage {
    Write-Host "[WEB] Starting coverage measurement..." -ForegroundColor Yellow

    $webDir = Join-Path $rootDir "apps\web"

    if (-not $SkipTests) {
        Write-Host "[WEB] Running tests with coverage collection..." -ForegroundColor Gray

        Push-Location $webDir
        try {
            # Check if pnpm is available
            $hasPnpm = $null -ne (Get-Command pnpm -ErrorAction SilentlyContinue)

            if (-not $hasPnpm) {
                Write-Host "[WEB] ⚠ pnpm not found! Please install pnpm." -ForegroundColor Red
                return $false
            }

            Write-Host "[WEB] Running: pnpm test:coverage" -ForegroundColor DarkGray

            pnpm test:coverage

            if ($LASTEXITCODE -ne 0) {
                Write-Host "[WEB] Tests failed or coverage collection failed!" -ForegroundColor Red
                return $false
            }

            Write-Host "[WEB] ✓ Tests completed successfully" -ForegroundColor Green
        }
        finally {
            Pop-Location
        }
    }

    $coverageDir = Join-Path $webDir "coverage"
    $lcovFile = Join-Path $coverageDir "lcov.info"
    $htmlIndex = Join-Path $coverageDir "lcov-report\index.html"

    if (Test-Path $lcovFile) {
        Write-Host "[WEB] Coverage report: $lcovFile" -ForegroundColor Green

        if (Test-Path $htmlIndex) {
            Write-Host "[WEB] HTML report: $htmlIndex" -ForegroundColor Green

            if ($GenerateHtml) {
                if ($IsWindows -or $PSVersionTable.Platform -eq 'Win32NT') {
                    Write-Host "[WEB] Opening report in browser..." -ForegroundColor Gray
                    Start-Process $htmlIndex
                }
            }
        }
    }
    else {
        Write-Host "[WEB] ⚠ No coverage file found!" -ForegroundColor Yellow
        return $false
    }

    return $true
}

# Main execution
$results = @{}

try {
    if ($Project -eq 'all' -or $Project -eq 'api') {
        $results['api'] = Measure-ApiCoverage
    }

    if ($Project -eq 'all' -or $Project -eq 'web') {
        $results['web'] = Measure-WebCoverage
    }

    Write-Host ""
    Write-Host "==================================================" -ForegroundColor Cyan
    Write-Host "  Coverage Measurement Summary" -ForegroundColor Cyan
    Write-Host "==================================================" -ForegroundColor Cyan

    foreach ($key in $results.Keys) {
        $status = if ($results[$key]) { "✓ SUCCESS" } else { "✗ FAILED" }
        $color = if ($results[$key]) { "Green" } else { "Red" }
        Write-Host "[$($key.ToUpper())] $status" -ForegroundColor $color
    }

    Write-Host ""
    Write-Host "Timestamp: $timestamp" -ForegroundColor Gray

    # Exit with error if any measurement failed
    $anyFailed = $results.Values -contains $false
    if ($anyFailed) {
        Write-Host ""
        Write-Host "⚠ Some coverage measurements failed. See output above for details." -ForegroundColor Yellow
        exit 1
    }
    else {
        Write-Host ""
        Write-Host "✓ All coverage measurements completed successfully!" -ForegroundColor Green
        exit 0
    }
}
catch {
    Write-Host ""
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
}
