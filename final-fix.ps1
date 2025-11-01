# Final comprehensive fix for FluentAssertions errors
$testPath = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Get all test files
$files = Get-ChildItem -Path $testPath -Filter "*.cs" -Recurse | Where-Object { $_.FullName -notmatch "\\obj\\" }

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Fix backtick issues
    $content = $content -replace '\`n\s+', ' '

    # Fix BeEquivalentTo on numeric types
    $content = $content -replace '\.Should\(\)\.BeEquivalentTo\((\d+)\)', '.Should().Be($1)'

    # Fix BeCloseTo on int (change to Be with approximation)
    $content = $content -replace '(\.Should\(\))\.BeCloseTo\(([^,]+),\s*([^)]+)\)', '$1.BeApproximately($2, $3)'

    # Fix BeCloseTo on double
    $content = $content -replace '(completionTokens\.Should\(\))\.BeCloseTo\(', '$1.BeApproximately('

    # Fix standalone Should expressions
    $content = $content -replace '^(\s+)(\w+\.Should\(\)[^;]+)$', '$1$2;'

    # Fix the var str pattern for GetString
    $content = $content -replace 'var str = (.+)\?\? "";var str\.', 'var str = $1 ?? ""; str.'

    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.Name)"
    }
}

Write-Host "`nDone! Final check..."