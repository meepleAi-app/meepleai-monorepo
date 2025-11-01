# Fix FluentAssertions syntax errors across all test files
# Pattern 1: value, "reason".Should().BeTrue() → value.Should().BeTrue("reason")
# Pattern 2: value, "reason".Should().BeFalse() → value.Should().BeFalse("reason")
# Pattern 3: value > 0, "reason".Should().BeTrue() → value.Should().BeGreaterThan(0, "reason")
# Pattern 4: value >= 0, "reason".Should().BeTrue() → value.Should().BeGreaterThanOrEqualTo(0, "reason")
# Pattern 5: value, StringComparison.X.Should().Contain("y") → value.Should().Contain("y", StringComparison.X)
# Pattern 6: value, StringComparison.X.Should().NotContain("y") → value.Should().NotContain("y", StringComparison.X)

$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Get all .cs files recursively
$files = Get-ChildItem -Path $testDir -Filter "*.cs" -Recurse

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content

    # Pattern: value!, StringComparison.X.Should().Contain/NotContain("y")
    $content = $content -replace '(\w+)!, (StringComparison\.\w+)\.Should\(\)\.Contain\(("[\w\s]+")','$1.Should().Contain($3, $2'
    $content = $content -replace '(\w+)!, (StringComparison\.\w+)\.Should\(\)\.NotContain\(("[\w\s]+")','$1.Should().NotContain($3, $2'

    # Pattern: value, StringComparison.X.Should().Contain/NotContain("y")
    $content = $content -replace '(\w+), (StringComparison\.\w+)\.Should\(\)\.Contain\(("[\w\s]+")','$1.Should().Contain($3, $2'
    $content = $content -replace '(\w+), (StringComparison\.\w+)\.Should\(\)\.NotContain\(("[\w\s]+")','$1.Should().NotContain($3, $2'

    # Save if changed
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.FullName)"
    }
}

Write-Host "`nDone! Run 'dotnet build' to check for remaining errors."
