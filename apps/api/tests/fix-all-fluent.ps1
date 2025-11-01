# Comprehensive FluentAssertions syntax fix script
$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"
$files = Get-ChildItem -Path $testDir -Filter "*.cs" -Recurse
$totalFixed = 0

foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    if (!$content) { continue }

    $original = $content

    # Fix all remaining comma-based patterns with multi-line regex
    $patterns = @(
        # Pattern: value!, StringComparison.X.Should().Contain("y") → value.Should().Contain("y", StringComparison.X)
        @('(\w+)!,\s*(StringComparison\.\w+)\.Should\(\)\.Contain\(("[^"]+")','$1.Should().Contain($3, $2'),
        @('(\w+)!,\s*(StringComparison\.\w+)\.Should\(\)\.NotContain\(("[^"]+")','$1.Should().NotContain($3, $2'),

        # Pattern: value, StringComparison.X.Should().Contain("y")
        @('(\w+),\s*(StringComparison\.\w+)\.Should\(\)\.Contain\(("[^"]+")','$1.Should().Contain($3, $2'),
        @('(\w+),\s*(StringComparison\.\w+)\.Should\(\)\.NotContain\(("[^"]+")','$1.Should().NotContain($3, $2'),

        # Pattern: value, "reason".Should().BeTrue/BeFalse()
        @('(\w+),\s*("[^"]+")\.Should\(\)\.BeTrue\(\)','($1).Should().BeTrue($2)'),
        @('(\w+),\s*("[^"]+")\.Should\(\)\.BeFalse\(\)','($1).Should().BeFalse($2)'),

        # Pattern: value > X, "reason".Should().BeTrue()
        @('(\w+\s*[><=!]+\s*\d+),\s*("[^"]+")\.Should\(\)\.BeTrue\(\)','($1).Should().BeTrue($2)'),
        @('(\w+\s*[><=!]+\s*\d+),\s*("[^"]+")\.Should\(\)\.BeFalse\(\)','($1).Should().BeFalse($2)'),

        # Pattern: complex boolean expression, "reason".Should().BeTrue()
        @('(\([^)]+\)),\s*\r?\n\s*("[^"]+")\.Should\(\)\.BeTrue\(\)','$1.Should().BeTrue($2)'),
        @('(\([^)]+\)),\s*\r?\n\s*("[^"]+")\.Should\(\)\.BeFalse\(\)','$1.Should().BeFalse($2)')
    )

    foreach ($pattern in $patterns) {
        $content = $content -replace $pattern[0], $pattern[1]
    }

    if ($content -ne $original) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $totalFixed++
        Write-Host "Fixed: $($file.Name)"
    }
}

Write-Host "`nTotal files fixed: $totalFixed"
Write-Host "Run 'dotnet build' to check for remaining errors."
