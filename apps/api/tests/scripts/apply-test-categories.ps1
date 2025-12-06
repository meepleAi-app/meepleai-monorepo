# Apply Test Categories to All Test Files - Issue #1820
# This script automatically categorizes all test files based on their characteristics

param(
    [switch]$DryRun = $false,
    [switch]$Verbose = $false
)

$ErrorActionPreference = "Stop"

# Category classification rules
$categoryRules = @{
    # Integration tests - use Testcontainers, databases, external services
    Integration = @(
        "*IntegrationTests.cs",
        "*E2ETests.cs",
        "*CrossContextTests.cs"
    )

    # Unit tests - pure logic, no external dependencies
    Unit = @(
        "*Tests.cs"  # Default for most tests
    )

    # Security tests - penetration tests, attack simulations
    Security = @(
        "*SecurityTests.cs",
        "*PenetrationTests.cs",
        "*AttackTests.cs"
    )

    # Performance tests - benchmarks, load tests
    Performance = @(
        "*PerformanceTests.cs",
        "*BenchmarkTests.cs"
    )

    # Slow tests - explicitly marked or E2E
    Slow = @(
        "*E2ETests.cs",
        "*LargeFileTests.cs"
    )
}

# Find all test files
$testFiles = Get-ChildItem -Path "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests" -Filter "*Tests.cs" -Recurse | Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

Write-Host "Found $($testFiles.Count) test files to process" -ForegroundColor Cyan

$stats = @{
    Total = 0
    Modified = 0
    Skipped = 0
    Errors = 0
}

foreach ($file in $testFiles) {
    $stats.Total++

    try {
        $content = Get-Content -Path $file.FullName -Raw
        $originalContent = $content

        # Determine category based on filename and content
        $category = "Unit"  # Default

        if ($file.Name -like "*IntegrationTests.cs" -or $file.Name -like "*E2ETests.cs" -or $file.Name -like "*CrossContextTests.cs") {
            $category = "Integration"
        }
        if ($file.Name -like "*SecurityTests.cs" -or $file.Name -like "*PenetrationTests.cs" -or $content -match "Security|Attack|Penetration") {
            $category = "Security"
        }
        if ($file.Name -like "*PerformanceTests.cs" -or $content -match "Performance|Benchmark") {
            $category = "Performance"
        }
        if ($file.Name -like "*E2ETests.cs" -or $content -match "Slow|LargeFile") {
            $additionalCategory = "Slow"
        }

        # Check if already has Category trait
        $hasCategory = $content -match '\[Trait\("Category"'

        if (-not $hasCategory) {
            # Find the class declaration to add trait before it
            if ($content -match '(?ms)(/// <summary>.*?/// </summary>\s*)((?:\[.*?\]\s*)*)(public (?:sealed )?class \w+)') {
                $summary = $matches[1]
                $existingAttributes = $matches[2]
                $classDeclaration = $matches[3]

                # Build new attributes
                $newAttributes = $existingAttributes
                if (-not ($existingAttributes -match '\[Trait\("Category"')) {
                    $newAttributes += "[Trait(""Category"", TestCategories.$category)]`n"
                }

                # Add TestCategories using statement if not present
                if ($content -notmatch 'using Api\.Tests\.Constants;') {
                    $content = $content -replace '(using .*?;)\s*\n\s*namespace', "`$1`nusing Api.Tests.Constants;`n`nnamespace"
                }

                # Replace with new structure
                $replacement = "$summary$newAttributes$classDeclaration"
                $content = $content -replace '(?ms)(/// <summary>.*?/// </summary>\s*)((?:\[.*?\]\s*)*)(public (?:sealed )?class \w+)', $replacement

                if ($content -ne $originalContent) {
                    if (-not $DryRun) {
                        Set-Content -Path $file.FullName -Value $content -NoNewline
                    }
                    $stats.Modified++

                    if ($Verbose -or $DryRun) {
                        Write-Host "  ✅ $($file.Name) -> [$category]" -ForegroundColor Green
                    }
                } else {
                    $stats.Skipped++
                }
            } else {
                Write-Host "  ⚠️  Could not parse class declaration in $($file.Name)" -ForegroundColor Yellow
                $stats.Skipped++
            }
        } else {
            $stats.Skipped++
            if ($Verbose) {
                Write-Host "  ⏭️  $($file.Name) - Already categorized" -ForegroundColor Gray
            }
        }
    }
    catch {
        $stats.Errors++
        Write-Host "  ❌ Error processing $($file.Name): $_" -ForegroundColor Red
    }
}

Write-Host "`n📊 Summary:" -ForegroundColor Cyan
Write-Host "  Total files: $($stats.Total)" -ForegroundColor White
Write-Host "  Modified: $($stats.Modified)" -ForegroundColor Green
Write-Host "  Skipped: $($stats.Skipped)" -ForegroundColor Yellow
Write-Host "  Errors: $($stats.Errors)" -ForegroundColor Red

if ($DryRun) {
    Write-Host "`n⚠️  DRY RUN - No files were actually modified" -ForegroundColor Yellow
    Write-Host "   Run without -DryRun to apply changes" -ForegroundColor Yellow
}
