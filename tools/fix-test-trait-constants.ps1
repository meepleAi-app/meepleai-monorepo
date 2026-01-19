#Requires -Version 7.0
<#
.SYNOPSIS
    Replaces string literal Trait attributes with TestCategories constants.

.DESCRIPTION
    Fixes test files that use [Trait("Category", "Unit")] to use [Trait("Category", TestCategories.Unit)]
    instead, following project standards and TestCategories.cs usage guidance.

.PARAMETER DryRun
    Preview changes without modifying files.

.EXAMPLE
    .\fix-test-trait-constants.ps1
    Replaces all string literals with constants.

.EXAMPLE
    .\fix-test-trait-constants.ps1 -DryRun
    Preview changes without modifying files.
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Add-UsingStatement {
    param(
        [string]$FilePath,
        [string]$Content
    )

    $usingStatement = "using Api.Tests.Constants;"

    # Check if using statement already exists
    if ($Content -match "using Api\.Tests\.Constants;") {
        return $Content
    }

    # Find the last using statement
    if ($Content -match "(?m)^using .+;$") {
        # Insert after the last using statement
        $Content = $Content -replace "(using [^;]+;(?:\r?\n)+)(?!using)", "`$1$usingStatement`n"
    }
    else {
        # No using statements found, add after namespace
        $Content = $Content -replace "(namespace [^;]+;(?:\r?\n)+)", "`$1$usingStatement`n`n"
    }

    return $Content
}

function Replace-TraitLiterals {
    param(
        [string]$FilePath
    )

    Write-Host "Processing: $FilePath" -ForegroundColor Cyan

    $content = Get-Content -Path $FilePath -Raw

    # Check if file contains string literal Trait attributes
    $hasLiterals = $content -match '\[Trait\("Category",\s*"(Unit|Integration|E2E)"\)\]'

    if (-not $hasLiterals) {
        Write-Host "  ✓ Already uses TestCategories constants" -ForegroundColor Yellow
        return $false
    }

    $originalContent = $content

    # Replace string literals with constants
    $content = $content -replace '\[Trait\("Category",\s*"Unit"\)\]', '[Trait("Category", TestCategories.Unit)]'
    $content = $content -replace '\[Trait\("Category",\s*"Integration"\)\]', '[Trait("Category", TestCategories.Integration)]'
    $content = $content -replace '\[Trait\("Category",\s*"E2E"\)\]', '[Trait("Category", TestCategories.E2E)]'

    # Add using statement if needed
    $content = Add-UsingStatement -FilePath $FilePath -Content $content

    if ($content -ne $originalContent) {
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline
            Write-Host "  ✓ Replaced literals with TestCategories constants" -ForegroundColor Green
            Write-Host "  ✓ Added using Api.Tests.Constants" -ForegroundColor Green
        }
        else {
            Write-Host "  [DRY RUN] Would replace literals with TestCategories constants" -ForegroundColor Magenta
        }

        return $true
    }

    return $false
}

# Main execution
Write-Host "=== Test Trait Constants Fix ===" -ForegroundColor White
Write-Host ""

if ($DryRun) {
    Write-Host "DRY RUN MODE - No files will be modified" -ForegroundColor Yellow
    Write-Host ""
}

$testRoot = Join-Path $PSScriptRoot "..\apps\api\tests\Api.Tests"

if (-not (Test-Path $testRoot)) {
    Write-Error "Test directory not found: $testRoot"
    exit 1
}

# Find all test files
$testFiles = Get-ChildItem -Path $testRoot -Filter "*Tests.cs" -Recurse |
    Where-Object {
        $_.FullName -notlike "*\obj\*" -and
        $_.FullName -notlike "*\bin\*"
    }

Write-Host "Found $($testFiles.Count) test files" -ForegroundColor Cyan
Write-Host ""

$stats = @{
    Total    = 0
    Modified = 0
    Skipped  = 0
}

foreach ($file in $testFiles) {
    $stats.Total++

    try {
        $wasModified = Replace-TraitLiterals -FilePath $file.FullName

        if ($wasModified) {
            $stats.Modified++
        }
        else {
            $stats.Skipped++
        }
    }
    catch {
        Write-Host "  ✗ Error: $_" -ForegroundColor Red
    }

    Write-Host ""
}

# Summary
Write-Host "=== Summary ===" -ForegroundColor White
Write-Host "Total files processed: $($stats.Total)"
Write-Host "Modified: $($stats.Modified) files" -ForegroundColor Green
Write-Host "Skipped (already using constants): $($stats.Skipped) files" -ForegroundColor Yellow

if (-not $DryRun -and $stats.Modified -gt 0) {
    Write-Host ""
    Write-Host "✓ Test trait constants fixed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review changes: git diff" -ForegroundColor White
    Write-Host "  2. Build tests: dotnet build apps/api/tests/Api.Tests" -ForegroundColor White
    Write-Host "  3. Run tests: dotnet test apps/api/tests/Api.Tests" -ForegroundColor White
    Write-Host "  4. Commit: git add . && git commit --amend --no-edit" -ForegroundColor White
}
