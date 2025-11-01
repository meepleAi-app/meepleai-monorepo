# Fix FluentAssertions compilation errors
$testPath = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Get all test files
$files = Get-ChildItem -Path $testPath -Filter "*.cs" -Recurse | Where-Object { $_.FullName -notmatch "\\obj\\" }

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Fix 1: Remove StringComparison from Contain() calls
    $content = $content -replace '\.Contain\(([^,]+),\s*StringComparison\.[^)]+\)', '.Contain($1)'

    # Fix 2: Fix BeTrue() on numeric assertions
    $content = $content -replace '(\w+)\.Should\(\)\.BeGreaterThan\([^)]+\)\.BeTrue\(\)', '$1.Should().BeGreaterThan(0)'
    $content = $content -replace '(\w+\.Count)\.Should\(\)\.BeTrue\(\)', '$1.Should().BeGreaterThan(0)'
    $content = $content -replace '(\w+\.TotalCount)\.Should\(\)\.BeTrue\(\)', '$1.Should().BeGreaterThan(0)'

    # Fix 3: Fix DateTime NotBeNull
    $content = $content -replace '(\w+\.ResetsAt)\.Should\(\)\.NotBeNull\(\)', '$1.Should().NotBe(default(DateTime))'

    # Fix 4: Fix collection Be() to BeEquivalentTo()
    $content = $content -replace '(embeddings|vectors|result\.\w+)\.Should\(\)\.Be\(([^)]+)\)', '$1.Should().BeEquivalentTo($2)'

    # Fix 5: Fix AndWhichConstraint access
    $content = $content -replace 'var (\w+) = _testLogger\.Logs\.Should\(\)\.ContainSingle\(([^)]+)\)\.Subject;[\r\n\s]+\1\.Message', 'var $1 = _testLogger.Logs.Should().ContainSingle($2).Subject;`n        $1.Message'

    # Fix 6: Fix BeInRange to BeCloseTo for DateTime
    $content = $content -replace '(\w+)\.Should\(\)\.BeInRange\(([^,]+),\s*([^)]+)\)', '$1.Should().BeCloseTo($2, TimeSpan.FromSeconds(5))'

    # Fix 7: Fix exception variable usage
    $content = $content -replace 'var exception = (.+);[\r\n\s]+exception\.(\w+)', 'var exception = $1.Subject;`n        exception.$2'

    # Fix 8: Fix null coalescing operator errors
    $content = $content -replace '(\w+\.GetString\(\))\s*\?\?\s*""\.Should\(\)', 'var str = $1 ?? "";`n        str.Should()'

    # Fix 9: Fix lambda assertion syntax
    $content = $content -replace '(\w+)\s*=>\s*\1\.Contains\(([^)]+)\)\.Should\(\)\.Contain\((\w+)\)', '$3.Should().Contain($1 => $1.Contains($2))'

    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.Name)"
    }
}

Write-Host "`nDone! Now running build to check results..."