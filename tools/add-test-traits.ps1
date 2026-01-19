#Requires -Version 7.0
<#
.SYNOPSIS
    Adds [Trait("Category", "...")] attributes to test files based on directory structure.

.DESCRIPTION
    Automatically categorizes tests as Unit, Integration, or E2E based on their location
    and adds the appropriate [Trait] attribute to test classes.

    Issue #2533: E2E Test Prerequisites Documentation

.PARAMETER DryRun
    Preview changes without modifying files.

.EXAMPLE
    .\add-test-traits.ps1
    Adds traits to all test files.

.EXAMPLE
    .\add-test-traits.ps1 -DryRun
    Preview changes without modifying files.
#>

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Define test categories based on directory structure
$categoryMappings = @{
    "Unit\"                = "Unit"
    "Integration\"         = "Integration"
    "BoundedContexts\"     = "Unit"  # Handler tests with mocks
    "Handlers\"            = "Unit"  # CQRS handlers
    "Validators\"          = "Unit"  # FluentValidation tests
    "Domain\"              = "Unit"  # Domain logic tests
}

function Add-TraitAttribute {
    param(
        [string]$FilePath,
        [string]$Category
    )

    Write-Host "Processing: $FilePath" -ForegroundColor Cyan

    $content = Get-Content -Path $FilePath -Raw

    # Check if already has Trait attribute
    if ($content -match '\[Trait\("Category",') {
        Write-Host "  ✓ Already has Trait attribute, skipping" -ForegroundColor Yellow
        return $false
    }

    # Find class declaration patterns
    $classPatterns = @(
        '(?m)^(\s*)(public\s+(sealed\s+)?class\s+\w+)'           # public class
        '(?m)^(\s*)(internal\s+(sealed\s+)?class\s+\w+)'         # internal class
        '(?m)^(\s*)(public\s+sealed\s+class\s+\w+)'             # public sealed class
    )

    $modified = $false

    foreach ($pattern in $classPatterns) {
        if ($content -match $pattern) {
            $indent = $Matches[1]
            $traitAttribute = "${indent}[Trait(`"Category`", `"$Category`")]`n"

            # Insert [Trait] before class declaration
            $newContent = $content -replace $pattern, "$traitAttribute`$&"

            if (-not $DryRun) {
                Set-Content -Path $FilePath -Value $newContent -NoNewline
                Write-Host "  ✓ Added [Trait(`"Category`", `"$Category`")]" -ForegroundColor Green
            }
            else {
                Write-Host "  [DRY RUN] Would add [Trait(`"Category`", `"$Category`")]" -ForegroundColor Magenta
            }

            $modified = $true
            break
        }
    }

    if (-not $modified) {
        Write-Host "  ✗ Could not find class declaration" -ForegroundColor Red
    }

    return $modified
}

function Get-TestCategory {
    param([string]$FilePath)

    foreach ($mapping in $categoryMappings.GetEnumerator()) {
        if ($FilePath -like "*$($mapping.Key)*") {
            return $mapping.Value
        }
    }

    # Default to Unit for tests in root or unrecognized directories
    return "Unit"
}

# Main execution
Write-Host "=== Test Trait Attribute Addition ===" -ForegroundColor White
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
    Total     = 0
    Modified  = 0
    Skipped   = 0
    Failed    = 0
    ByCategory = @{
        Unit        = 0
        Integration = 0
        E2E         = 0
    }
}

foreach ($file in $testFiles) {
    $stats.Total++

    $relativePath = $file.FullName.Replace($testRoot, "").TrimStart("\")
    $category = Get-TestCategory -FilePath $relativePath

    try {
        $wasModified = Add-TraitAttribute -FilePath $file.FullName -Category $category

        if ($wasModified) {
            $stats.Modified++
            $stats.ByCategory[$category]++
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
Write-Host "Skipped (already has trait): $($stats.Skipped) files" -ForegroundColor Yellow
Write-Host "Failed: $($stats.Failed) files" -ForegroundColor Red
Write-Host ""
Write-Host "By Category:" -ForegroundColor Cyan
Write-Host "  Unit: $($stats.ByCategory.Unit)" -ForegroundColor Cyan
Write-Host "  Integration: $($stats.ByCategory.Integration)" -ForegroundColor Cyan
Write-Host "  E2E: $($stats.ByCategory.E2E)" -ForegroundColor Cyan

if (-not $DryRun -and $stats.Modified -gt 0) {
    Write-Host ""
    Write-Host "✓ Trait attributes added successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Review changes: git diff" -ForegroundColor White
    Write-Host "  2. Run tests: dotnet test --filter `"Category=Unit`"" -ForegroundColor White
    Write-Host "  3. Commit changes: git add . && git commit -m 'feat(tests): Add test category traits'" -ForegroundColor White
}
