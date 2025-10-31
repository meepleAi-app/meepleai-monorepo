param(
    [int]$BatchSize = 10
)

$ErrorActionPreference = "Stop"
$testDir = "tests\Api.Tests"
$filesWithAssert = @()

# Find all files with Assert statements
Get-ChildItem -Path $testDir -Recurse -Filter "*.cs" | ForEach-Object {
    if ((Select-String -Path $_.FullName -Pattern "Assert\." -Quiet)) {
        $filesWithAssert += $_.FullName
    }
}

Write-Host "Found $($filesWithAssert.Count) files to migrate"

$batchNum = 1
$totalConverted = 0

for ($i = 0; $i -lt $filesWithAssert.Count; $i += $BatchSize) {
    $batch = $filesWithAssert[$i..[Math]::Min($i + $BatchSize - 1, $filesWithAssert.Count - 1)]

    Write-Host "`nBatch $batchNum ($($batch.Count) files)..."

    foreach ($file in $batch) {
        $content = Get-Content $file -Raw
        $fileName = Split-Path $file -Leaf

        # Add using if not present
        if ($content -notmatch "using FluentAssertions;") {
            $content = $content -replace "(using Xunit;)", "`$1`r`nusing FluentAssertions;"
        }

        # SAFE conversions only - NO Assert.True/False (too risky)
        $content = $content -replace 'Assert\.NotNull\(([^)]+)\);', '$1.Should().NotBeNull();'
        $content = $content -replace 'Assert\.Null\(([^)]+)\);', '$1.Should().BeNull();'
        $content = $content -replace 'Assert\.Empty\(([^)]+)\);', '$1.Should().BeEmpty();'
        $content = $content -replace 'Assert\.NotEmpty\(([^)]+)\);', '$1.Should().NotBeEmpty();'
        $content = $content -replace 'Assert\.Single\(([^)]+)\);', '$1.Should().ContainSingle();'

        # Simple string contains (no wildcards, no collections)
        $content = $content -replace 'Assert\.Contains\("([^"]+)", ([a-zA-Z0-9_\.]+)\);', '$2.Should().Contain("$1");'

        # Status codes only
        $content = $content -replace 'Assert\.Equal\(HttpStatusCode\.(\w+), response\.StatusCode\);', 'response.StatusCode.Should().Be(HttpStatusCode.$1);'

        Set-Content -Path $file -Value $content -NoNewline
        $totalConverted++
    }

    # Verify compilation after each batch
    Write-Host "  Building..."
    $buildOutput = & dotnet build --no-restore 2>&1 | Out-String

    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ❌ Build failed! Reverting batch $batchNum..." -ForegroundColor Red
        & git restore $batch
        Write-Host "  Skipped batch $batchNum (has complex patterns)" -ForegroundColor Yellow
        $totalConverted -= $batch.Count
    }
    else {
        Write-Host "  ✅ Batch $batchNum OK ($($batch.Count) files)" -ForegroundColor Green
    }

    $batchNum++
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "✅ Migrated $totalConverted files successfully" -ForegroundColor Green
Write-Host "Remaining files need manual conversion for complex patterns" -ForegroundColor Yellow
