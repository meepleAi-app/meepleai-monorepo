# Safe Test Runner - Ensures clean environment before running tests
# Usage: .\tools\run-tests-safe.ps1 [-Filter "TestName"] [-Verbose] [-NoBuild]

param(
    [string]$Filter = "",
    [switch]$Verbose = $false,
    [switch]$NoBuild = $false,
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = "Continue"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

Write-Host "=== Safe Test Runner ===" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor Gray
Write-Host "Filter: $(if ($Filter) { $Filter } else { '(all tests)' })" -ForegroundColor Gray
Write-Host "Timeout: $TimeoutSeconds seconds" -ForegroundColor Gray
Write-Host ""

# Step 1: Clean up any hanging processes
Write-Host "[1/5] Cleaning up hanging test processes..." -ForegroundColor Yellow
& "$scriptDir\cleanup-test-processes.ps1" -Verbose:$Verbose
Start-Sleep -Seconds 2

# Step 2: Shutdown build servers
Write-Host "`n[2/5] Shutting down build servers..." -ForegroundColor Yellow
Push-Location "$repoRoot\apps\api"
try {
    dotnet build-server shutdown 2>&1 | Out-Null
    Write-Host "Build servers stopped" -ForegroundColor Green
} catch {
    Write-Host "Warning: Could not stop build servers: $_" -ForegroundColor Yellow
}
Pop-Location
Start-Sleep -Seconds 2

# Step 3: Clear test output directory
Write-Host "`n[3/5] Clearing old test outputs..." -ForegroundColor Yellow
$testOutputDir = "$repoRoot\apps\api\tests\Api.Tests\bin\Debug\net9.0"
if (Test-Path $testOutputDir) {
    try {
        Remove-Item "$testOutputDir\*.trx" -Force -ErrorAction SilentlyContinue
        Remove-Item "$testOutputDir\TestResults" -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Test outputs cleared" -ForegroundColor Green
    } catch {
        Write-Host "Warning: Could not clear some test outputs: $_" -ForegroundColor Yellow
    }
}

# Step 4: Build (if not skipped)
if (-not $NoBuild) {
    Write-Host "`n[4/5] Building test project..." -ForegroundColor Yellow
    Push-Location "$repoRoot\apps\api"
    $buildOutput = dotnet build --no-incremental 2>&1
    $buildExitCode = $LASTEXITCODE
    Pop-Location

    if ($buildExitCode -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        Write-Host $buildOutput
        exit $buildExitCode
    }
    Write-Host "Build successful" -ForegroundColor Green
} else {
    Write-Host "`n[4/5] Skipping build (--NoBuild specified)" -ForegroundColor Gray
}

# Step 5: Run tests
Write-Host "`n[5/5] Running tests..." -ForegroundColor Yellow
Push-Location "$repoRoot\apps\api"

$testArgs = @("test", "--verbosity", "normal")
if ($NoBuild) {
    $testArgs += "--no-build"
}
if ($Filter) {
    $testArgs += "--filter"
    $testArgs += $Filter
}

Write-Host "Command: dotnet $($testArgs -join ' ')" -ForegroundColor Gray
Write-Host ""

# Run with timeout
$job = Start-Job -ScriptBlock {
    param($repoPath, $testArgs)
    Set-Location $repoPath
    & dotnet $testArgs
} -ArgumentList "$repoRoot\apps\api", $testArgs

$completed = Wait-Job $job -Timeout $TimeoutSeconds

if ($completed) {
    $output = Receive-Job $job
    if ($job.State -eq "Completed") {
        $exitCode = 0
    } else {
        $exitCode = 1
    }
    Write-Host $output
} else {
    Write-Host "`nTest execution timed out after $TimeoutSeconds seconds!" -ForegroundColor Red
    Stop-Job $job
    Remove-Job $job -Force
    $exitCode = 124  # Standard timeout exit code
}

Remove-Job $job -Force -ErrorAction SilentlyContinue
Pop-Location

# Summary
Write-Host "`n=== Test Run Complete ===" -ForegroundColor Cyan
if ($exitCode -eq 0) {
    Write-Host "Result: SUCCESS" -ForegroundColor Green
} elseif ($exitCode -eq 124) {
    Write-Host "Result: TIMEOUT" -ForegroundColor Red
} else {
    Write-Host "Result: FAILURES" -ForegroundColor Yellow
}

exit $exitCode
