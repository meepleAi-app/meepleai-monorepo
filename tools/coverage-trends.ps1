# Coverage Trend Tracking Script (PowerShell)
# Tracks test coverage over time for MeepleAI monorepo

[CmdletBinding()]
param(
    [switch]$NoRun,
    [switch]$Help
)

# Configuration
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmmss"
$coverageDir = "coverage-history"
$logFile = Join-Path $coverageDir "trends.log"

# Ensure coverage directory exists
if (-not (Test-Path $coverageDir)) {
    New-Item -ItemType Directory -Path $coverageDir | Out-Null
}

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Extract-Metrics {
    param(
        [string]$JsonFile,
        [string]$Project
    )

    if (Test-Path $JsonFile) {
        Write-ColorOutput "📊 $Project Coverage Snapshot" -Color Green

        try {
            $coverage = Get-Content $JsonFile -Raw | ConvertFrom-Json

            if ($coverage.total) {
                $statements = $coverage.total.statements.pct
                $branches = $coverage.total.branches.pct
                $functions = $coverage.total.functions.pct
                $lines = $coverage.total.lines.pct

                Write-Host "  Statements: $statements%"
                Write-Host "  Branches:   $branches%"
                Write-Host "  Functions:  $functions%"
                Write-Host "  Lines:      $lines%"

                # Log to trends file
                $logEntry = "[$timestamp] $Project - Statements: $statements% | Branches: $branches% | Functions: $functions% | Lines: $lines%"
                Add-Content -Path $logFile -Value $logEntry
            } else {
                Write-ColorOutput "  Coverage data format not recognized" -Color Yellow
                $coverage | ConvertTo-Json | Add-Content -Path $logFile
            }

            # Copy full report to history
            $historyFile = Join-Path $coverageDir "coverage-$Project-$timestamp.json"
            Copy-Item $JsonFile $historyFile
            Write-ColorOutput "✓ Snapshot saved: $historyFile`n" -Color Green
        }
        catch {
            Write-ColorOutput "⚠ Error parsing coverage file: $_" -Color Yellow
        }
    }
    else {
        Write-ColorOutput "⚠ Coverage file not found: $JsonFile`n" -Color Yellow
    }
}

if ($Help) {
    Write-Host ""
    Write-Host "Coverage Trend Tracking Script"
    Write-Host ""
    Write-Host "Usage: .\coverage-trends.ps1 [OPTIONS]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -NoRun      Use existing coverage data (skip test execution)"
    Write-Host "  -Help       Show this help message"
    Write-Host ""
    Write-Host "Examples:"
    Write-Host "  .\coverage-trends.ps1                  # Run tests and capture coverage"
    Write-Host "  .\coverage-trends.ps1 -NoRun           # Use existing coverage files"
    Write-Host ""
    exit 0
}

Write-ColorOutput "=== MeepleAI Coverage Trend Tracker ===" -Color Cyan
Write-ColorOutput "Timestamp: $timestamp`n" -Color Cyan

# Frontend Coverage
Write-ColorOutput "--- Frontend (Jest) ---" -Color Cyan
Push-Location apps\web

if (-not $NoRun) {
    Write-Host "Running frontend tests with coverage..."
    $rawOutputFile = Join-Path "..\..\" $coverageDir "coverage-web-$timestamp-raw.json"

    pnpm test:coverage --silent --json --outputFile=$rawOutputFile 2>&1 | Select-Object -Last 20
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "⚠ Frontend tests failed (exit code: $LASTEXITCODE) - coverage may be incomplete" -Color Yellow
        exit $LASTEXITCODE
    }
}

# Check for Jest coverage summary
$frontendCoverage = "coverage\coverage-summary.json"
if (Test-Path $frontendCoverage) {
    Extract-Metrics -JsonFile $frontendCoverage -Project "Frontend"
}
else {
    Write-ColorOutput "⚠ Frontend coverage summary not found`n" -Color Yellow
}

Pop-Location

# Backend Coverage
Write-ColorOutput "--- Backend (.NET) ---" -Color Cyan
Push-Location apps\api

if (-not $NoRun) {
    Write-Host "Running backend tests with coverage..."
    $backendCoverageFile = Join-Path "..\..\" $coverageDir "coverage-api-$timestamp.json"

    dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=json /p:CoverletOutput=$backendCoverageFile --verbosity quiet 2>&1 | Select-Object -Last 20
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "⚠ Backend tests failed (exit code: $LASTEXITCODE) - coverage may be incomplete" -Color Yellow
        exit $LASTEXITCODE
    }
}

# Extract backend coverage if available
$backendCoverageFile = Join-Path "..\..\" $coverageDir "coverage-api-$timestamp.json"
if (Test-Path $backendCoverageFile) {
    Extract-Metrics -JsonFile $backendCoverageFile -Project "Backend"
}
else {
    Write-ColorOutput "⚠ Backend coverage file not generated`n" -Color Yellow
}

Pop-Location

# Summary
Write-ColorOutput "`n=== Summary ===" -Color Cyan
Write-Host "Coverage snapshots saved to: $coverageDir\"
Write-Host "Trend log: $logFile"
Write-Host ""
Write-ColorOutput "✓ Coverage trend tracking complete!" -Color Green

# Show last 5 entries from log
if (Test-Path $logFile) {
    Write-ColorOutput "`nRecent Coverage History:" -Color Cyan
    Get-Content $logFile | Select-Object -Last 5
}
