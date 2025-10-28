# AI-11: Fix test files to add score parameter to Snippet constructors

$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests"
$files = Get-ChildItem -Path $testDir -Filter "*.cs" -Recurse

$count = 0
$totalReplacements = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Replace 4-parameter Snippet calls with 5-parameter (add score)
    # This regex handles both "new Snippet(...)" and target-typed "new(...)"
    # Pattern: new(...4 params...) -> new(...4 params..., 0.85f)
    $content = $content -replace '(?s)new\s*\(\s*"([^"]+)",\s*"([^"]+)",\s*(\d+),\s*(\d+)\s*\)', 'new("$1", "$2", $3, $4, 0.85f)'
    $content = $content -replace '(?s)new Snippet\s*\(\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*([^),]+)\s*\)', 'new Snippet($1, $2, $3, $4, 0.85f)'

    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $matches = [regex]::Matches($originalContent, '(?s)new Snippet\s*\(\s*[^)]+\s*\)')
        Write-Host "Updated $($file.Name): $($matches.Count) replacements"
        $count++
        $totalReplacements += $matches.Count
    }
}

Write-Host "`nTotal: Updated $count files with $totalReplacements replacements"
