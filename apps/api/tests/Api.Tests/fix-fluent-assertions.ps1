# Fix common FluentAssertions syntax errors across all test files
$testPath = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"
$files = Get-ChildItem -Path $testPath -Filter "*Tests.cs" -Recurse

$fixCount = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Pattern 1: condition, message.Should().BeTrue() -> condition.Should().BeTrue(message)
    $content = $content -replace '(\s+)(\S.+?),\s*\r?\n\s*(["`][^"`]+["`])\.Should\(\)\.BeTrue\(\);', '$1($2).Should().BeTrue($3);'

    # Pattern 2: condition, message.Should().BeFalse() -> condition.Should().BeFalse(message)
    $content = $content -replace '(\s+)(\S.+?),\s*\r?\n\s*(["`][^"`]+["`])\.Should\(\)\.BeFalse\(\);', '$1($2).Should().BeFalse($3);'

    # Pattern 3: Multi-line boolean conditions with .Should().BeTrue() at end
    $content = $content -replace '(\s+)([\S\s]+?)\s*,\s*\r?\n\s*\$"([^"]+)"\s*\.Should\(\)\.BeTrue\(\);', '$1($2).Should().BeTrue($$$"$3");'

    # Pattern 4: string.IsNullOrWhiteSpace patterns
    $content = $content -replace 'string\.IsNullOrWhiteSpace\(([^)]+)\),\s*\r?\n\s*(["`$][^;]+)\.Should\(\)\.BeFalse\(\);', 'string.IsNullOrWhiteSpace($1).Should().BeFalse($2);'

    # Pattern 5: response.IsSuccessStatusCode, message.Should().BeTrue()
    $content = $content -replace '(\s+)response\.IsSuccessStatusCode,\s*\r?\n\s*(["`$][^;]+)\.Should\(\)\.BeTrue\(\);', '$1response.IsSuccessStatusCode.Should().BeTrue($2);'

    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.Name)" -ForegroundColor Green
        $fixCount++
    }
}

Write-Host "`nTotal files fixed: $fixCount" -ForegroundColor Cyan
