param([int]$BatchSize = 30)

$testDir = "tests\Api.Tests"
$files = Get-ChildItem -Path $testDir -Recurse -Filter "*.cs" | Where-Object {
    (Select-String -Path $_.FullName -Pattern "Assert\." -Quiet)
}

Write-Host "Found $($files.Count) files with Assert"

$converted = 0
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw

    # Add using ONLY if missing
    if ($content -notmatch "using FluentAssertions;") {
        $content = $content -replace "(using Xunit;)", "`$1`r`nusing FluentAssertions;"
    }

    # ULTRA-SAFE: Only 4 patterns that NEVER break
    $original = $content
    $content = $content -replace 'Assert\.NotNull\(([a-zA-Z0-9_\.]+)\);', '$1.Should().NotBeNull();'
    $content = $content -replace 'Assert\.Null\(([a-zA-Z0-9_\.]+)\);', '$1.Should().BeNull();'
    $content = $content -replace 'Assert\.Empty\(([a-zA-Z0-9_\.]+)\);', '$1.Should().BeEmpty();'
    $content = $content -replace 'Assert\.NotEmpty\(([a-zA-Z0-9_\.]+)\);', '$1.Should().NotBeEmpty();'

    if ($content -ne $original) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $converted++
    }
}

# Verify build
Write-Host "`nVerifying build..."
$result = & dotnet build --no-restore 2>&1 | Select-String -Pattern "error" | Select-Object -First 5

if ($result) {
    Write-Host "❌ Errors found:" -ForegroundColor Red
    $result
    exit 1
} else {
    Write-Host "✅ Build successful! $converted files migrated" -ForegroundColor Green
}
