# Comprehensive Test Suite Optimization - Issue #1820
# Implements all 3 optimizations: Categories, Parallel Execution, Shared Containers
#
# Baseline: 17m 13s
# Target: <7m (60-70% improvement)

param(
    [switch]$DryRun = $false,
    [switch]$Verbose = $false,
    [switch]$SkipCategories = $false,
    [switch]$SkipCollections = $false,
    [switch]$SkipRedis = $false
)

$ErrorActionPreference = "Stop"
$testRoot = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

$stats = @{
    TotalFiles = 0
    CategoriesAdded = 0
    CollectionsRemoved = 0
    RedisFixed = 0
    Errors = 0
}

Write-Host "`n🚀 Test Suite Optimization Script - Issue #1820" -ForegroundColor Cyan
Write-Host "Baseline: 17m 13s → Target: <7m (60% improvement)`n" -ForegroundColor Yellow

#region Step 1: Add Test Categories
if (-not $SkipCategories) {
    Write-Host "📂 Step 1: Adding test categories to all test files..." -ForegroundColor Cyan

    $testFiles = Get-ChildItem -Path $testRoot -Filter "*Tests.cs" -Recurse |
        Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

    $stats.TotalFiles = $testFiles.Count
    Write-Host "   Found $($testFiles.Count) test files" -ForegroundColor White

    foreach ($file in $testFiles) {
        try {
            $content = Get-Content -Path $file.FullName -Raw
            $modified = $false

            # Determine category
            $category = "Unit"
            if ($file.Name -match "Integration|E2E|CrossContext") { $category = "Integration" }
            if ($file.Name -match "Security|Penetration|Attack") { $category = "Security" }
            if ($file.Name -match "Performance|Benchmark") { $category = "Performance" }

            # Check if already has category
            if ($content -notmatch '\[Trait\("Category", TestCategories\.' -and $content -notmatch '\[Trait\("Category", "') {
                # Add using statement
                if ($content -notmatch 'using Api\.Tests\.Constants;') {
                    $content = $content -replace '(using Xunit;)', "`$1`nusing Api.Tests.Constants;"
                    $modified = $true
                }

                # Add trait before class declaration
                $content = $content -replace '(public (?:sealed )?class \w+.*?Tests)', "[Trait(`"Category`", TestCategories.$category)]`n`$1"
                $modified = $true
            }

            if ($modified -and -not $DryRun) {
                Set-Content -Path $file.FullName -Value $content -NoNewline
                $stats.CategoriesAdded++
                if ($Verbose) { Write-Host "   ✅ $($file.Name) -> [$category]" -ForegroundColor Green }
            }
        }
        catch {
            $stats.Errors++
            Write-Host "   ❌ Error: $($file.Name) - $_" -ForegroundColor Red
        }
    }

    Write-Host "   Categories added: $($stats.CategoriesAdded)" -ForegroundColor Green
}
#endregion

#region Step 2: Remove [Collection] Attributes
if (-not $SkipCollections) {
    Write-Host "`n🔓 Step 2: Removing [Collection] attributes to enable parallelization..." -ForegroundColor Cyan

    $collectionFiles = Get-ChildItem -Path $testRoot -Filter "*.cs" -Recurse |
        Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" } |
        Where-Object { (Get-Content $_.FullName -Raw) -match '\[Collection\(' }

    Write-Host "   Found $($collectionFiles.Count) files with [Collection] attributes" -ForegroundColor White

    foreach ($file in $collectionFiles) {
        try {
            $content = Get-Content -Path $file.FullName -Raw

            # Remove [Collection("...")] attribute lines
            $originalContent = $content
            $content = $content -replace '\[Collection\([^]]+\)\]\s*\r?\n', ''

            if ($content -ne $originalContent -and -not $DryRun) {
                Set-Content -Path $file.FullName -Value $content -NoNewline
                $stats.CollectionsRemoved++
                if ($Verbose) { Write-Host "   ✅ $($file.Name) - Collection removed" -ForegroundColor Green }
            }
        }
        catch {
            $stats.Errors++
            Write-Host "   ❌ Error: $($file.Name) - $_" -ForegroundColor Red
        }
    }

    Write-Host "   Collections removed: $($stats.CollectionsRemoved)" -ForegroundColor Green
}
#endregion

#region Step 3: Add Unique Redis Key Prefixes
if (-not $SkipRedis) {
    Write-Host "`n🔑 Step 3: Adding unique Redis key prefixes for parallel execution..." -ForegroundColor Cyan

    $redisFiles = @(
        "$testRoot\Integration\PdfUploadQuotaEnforcementIntegrationTests.cs",
        "$testRoot\Integration\Authentication\TwoFactorSecurityPenetrationTests.cs",
        "$testRoot\Integration\Authentication\TotpReplayAttackPreventionTests.cs",
        "$testRoot\Infrastructure\RedisOAuthStateStoreTests.cs",
        "$testRoot\Infrastructure\RedisBackgroundTaskOrchestratorTests.cs"
    )

    foreach ($filePath in $redisFiles) {
        if (Test-Path $filePath) {
            try {
                $content = Get-Content -Path $filePath -Raw

                # Add unique key prefix field if not exists
                if ($content -notmatch 'private readonly string _redisKeyPrefix') {
                    # Find class opening brace and add field
                    $content = $content -replace '(public (?:sealed )?class \w+[^{]*\{)', "`$1`n    private readonly string _redisKeyPrefix = `$`"test:{Guid.NewGuid()}:`";"

                    # Update Redis key usage to include prefix
                    $content = $content -replace '("pdf:upload:quota:)', "`$`"{_redisKeyPrefix}`$1"
                    $content = $content -replace '("oauth:state:)', "`$`"{_redisKeyPrefix}`$1"
                    $content = $content -replace '("totp:used:)', "`$`"{_redisKeyPrefix}`$1"

                    if (-not $DryRun) {
                        Set-Content -Path $filePath -Value $content -NoNewline
                        $stats.RedisFixed++
                        if ($Verbose) { Write-Host "   ✅ $(Split-Path $filePath -Leaf) - Redis keys prefixed" -ForegroundColor Green }
                    }
                }
            }
            catch {
                $stats.Errors++
                Write-Host "   ❌ Error: $(Split-Path $filePath -Leaf) - $_" -ForegroundColor Red
            }
        }
    }

    Write-Host "   Redis tests fixed: $($stats.RedisFixed)" -ForegroundColor Green
}
#endregion

#region Step 4: Remove PdfPipelineCollectionDefinition
Write-Host "`n🗑️  Step 4: Removing PdfPipelineCollectionDefinition (blocking parallelization)..." -ForegroundColor Cyan

$pdfCollectionFile = "$testRoot\Infrastructure\PdfPipelineCollectionDefinition.cs"
if (Test-Path $pdfCollectionFile) {
    if (-not $DryRun) {
        Remove-Item $pdfCollectionFile -Force
        Write-Host "   ✅ PdfPipelineCollectionDefinition.cs removed" -ForegroundColor Green
    } else {
        Write-Host "   [DRY RUN] Would remove: PdfPipelineCollectionDefinition.cs" -ForegroundColor Yellow
    }
}
#endregion

#region Summary
Write-Host "`n📊 Optimization Summary:" -ForegroundColor Cyan
Write-Host "   Total test files: $($stats.TotalFiles)" -ForegroundColor White
Write-Host "   Categories added: $($stats.CategoriesAdded)" -ForegroundColor Green
Write-Host "   Collections removed: $($stats.CollectionsRemoved)" -ForegroundColor Green
Write-Host "   Redis tests fixed: $($stats.RedisFixed)" -ForegroundColor Green
Write-Host "   Errors: $($stats.Errors)" -ForegroundColor $(if ($stats.Errors -gt 0) { "Red" } else { "Green" })

if ($DryRun) {
    Write-Host "`n⚠️  DRY RUN - No files were modified" -ForegroundColor Yellow
    Write-Host "   Run without -DryRun to apply changes" -ForegroundColor Yellow
} else {
    Write-Host "`n✅ Optimization complete!" -ForegroundColor Green
    Write-Host "   Next steps:" -ForegroundColor Cyan
    Write-Host "   1. Build: cd apps/api && dotnet build" -ForegroundColor White
    Write-Host "   2. Test: dotnet test --filter 'Category=Unit'" -ForegroundColor White
    Write-Host "   3. Measure: time dotnet test" -ForegroundColor White
}
#endregion
