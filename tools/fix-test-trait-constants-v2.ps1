#Requires -Version 7.0
<#
.SYNOPSIS
    Replaces string literal Trait attributes with TestCategories constants.

.DESCRIPTION
    Fixes test files that use [Trait("Category", "Unit")] to use [Trait("Category", TestCategories.Unit)]
    instead, following project standards and TestCategories.cs usage guidance.

    Handles both traditional namespace blocks and file-scoped namespaces correctly.

.PARAMETER DryRun
    Preview changes without modifying files.

.EXAMPLE
    .\fix-test-trait-constants-v2.ps1
    Replaces all string literals with constants.

.EXAMPLE
    .\fix-test-trait-constants-v2.ps1 -DryRun
    Preview changes without modifying files.
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

function Add-UsingStatement {
    param(
        [string]$Content
    )

    $usingStatement = "using Api.Tests.Constants;"

    # Check if using statement already exists
    if ($Content -match "using Api\.Tests\.Constants;") {
        return $Content, $false
    }

    # Find position to insert using statement - after last using statement
    $lines = $Content -split "`r?`n"
    $lastUsingIndex = -1

    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^using\s+[^;]+;") {
            $lastUsingIndex = $i
        }
        elseif ($lines[$i] -match "^namespace\s+") {
            # Stop at namespace declaration
            break
        }
    }

    if ($lastUsingIndex -ge 0) {
        # Insert after last using statement
        $lines = $lines[0..$lastUsingIndex] + $usingStatement + $lines[($lastUsingIndex + 1)..($lines.Count - 1)]
        return ($lines -join "`n"), $true
    }
    else {
        # No using statements found - insert before namespace
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match "^namespace\s+") {
                $lines = $lines[0..($i - 1)] + $usingStatement + "" + $lines[$i..($lines.Count - 1)]
                return ($lines -join "`n"), $true
            }
        }
    }

    # Fallback: add at beginning after any file header comments
    $insertIndex = 0
    foreach ($line in $lines) {
        if ($line -match "^//|^/\*|^\s*$") {
            $insertIndex++
        }
        else {
            break
        }
    }

    $lines = $lines[0..($insertIndex - 1)] + $usingStatement + "" + $lines[$insertIndex..($lines.Count - 1)]
    return ($lines -join "`n"), $true
}

function Replace-TraitLiterals {
    param(
        [string]$FilePath
    )

    Write-Host "Processing: $FilePath" -ForegroundColor Cyan

    $content = Get-Content -Path $FilePath -Raw

    # Check if file contains string literal Trait attributes
    $hasLiterals = $content -match '\[Trait\("Category",\s*"(Unit|Integration|E2E|Performance|Security|Slow)"\)\]'

    if (-not $hasLiterals) {
        Write-Host "  ✓ Already uses TestCategories constants" -ForegroundColor Yellow
        return $false
    }

    $originalContent = $content

    # Replace string literals with constants
    $content = $content -replace '\[Trait\("Category",\s*"Unit"\)\]', '[Trait("Category", TestCategories.Unit)]'
    $content = $content -replace '\[Trait\("Category",\s*"Integration"\)\]', '[Trait("Category", TestCategories.Integration)]'
    $content = $content -replace '\[Trait\("Category",\s*"E2E"\)\]', '[Trait("Category", TestCategories.E2E)]'
    $content = $content -replace '\[Trait\("Category",\s*"Performance"\)\]', '[Trait("Category", TestCategories.Performance)]'
    $content = $content -replace '\[Trait\("Category",\s*"Security"\)\]', '[Trait("Category", TestCategories.Security)]'
    $content = $content -replace '\[Trait\("Category",\s*"Slow"\)\]', '[Trait("Category", TestCategories.Slow)]'

    # Add using statement if needed
    $content, $usingAdded = Add-UsingStatement -Content $content

    if ($content -ne $originalContent) {
        if (-not $DryRun) {
            Set-Content -Path $FilePath -Value $content -NoNewline
            Write-Host "  ✓ Replaced literals with TestCategories constants" -ForegroundColor Green
            if ($usingAdded) {
                Write-Host "  ✓ Added using Api.Tests.Constants" -ForegroundColor Green
            }
        }
        else {
            Write-Host "  [DRY RUN] Would replace literals with TestCategories constants" -ForegroundColor Magenta
            if ($usingAdded) {
                Write-Host "  [DRY RUN] Would add using Api.Tests.Constants" -ForegroundColor Magenta
            }
        }

        return $true
    }

    return $false
}

# Main execution
Write-Host "=== Test Trait Constants Fix (v2) ===" -ForegroundColor White
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
    Failed   = 0
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
        $stats.Failed++
    }

    Write-Host ""
}

# Summary
Write-Host "=== Summary ===" -ForegroundColor White
Write-Host "Total files processed: $($stats.Total)"
Write-Host "Modified: $($stats.Modified) files" -ForegroundColor Green
Write-Host "Skipped (already using constants): $($stats.Skipped) files" -ForegroundColor Yellow
if ($stats.Failed -gt 0) {
    Write-Host "Failed: $($stats.Failed) files" -ForegroundColor Red
}

if (-not $DryRun -and $stats.Modified -gt 0) {
    Write-Host ""
    Write-Host "✓ Test trait constants fixed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review changes: git diff" -ForegroundColor White
    Write-Host "  2. Build tests: cd apps/api/tests/Api.Tests && dotnet build" -ForegroundColor White
    Write-Host "  3. Run tests: dotnet test" -ForegroundColor White
    Write-Host "  4. Commit: git add . && git commit --amend --no-edit" -ForegroundColor White
}
