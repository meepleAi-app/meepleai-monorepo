# Fix syntax errors introduced by previous script
$testPath = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Get all test files
$files = Get-ChildItem -Path $testPath -Filter "*.cs" -Recurse | Where-Object { $_.FullName -notmatch "\\obj\\" }

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Fix backtick syntax errors
    $content = $content -replace '`n', "`r`n"

    # Fix broken lines
    $content = $content -replace '\.Subject;`r`n\s+', '.Subject;' + "`r`n        "
    $content = $content -replace 'var str = ([^;]+);`r`n\s+str\.', 'var str = $1;' + "`r`n        str."

    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Fixed syntax in: $($file.Name)"
    }
}

Write-Host "`nDone! Now checking build..."