<#
.SYNOPSIS
Fix double 'using using var' created by previous script

.DESCRIPTION
Replaces 'using using var' with 'using var' across all test files
#>

param(
    [string]$TestsPath = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"
)

$ErrorActionPreference = "Stop"

$testFiles = Get-ChildItem -Path $TestsPath -Filter "*.cs" -Recurse -File
$totalFixed = 0

foreach ($file in $testFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content

    # Fix: using using var → using var
    $content = $content -replace '(\s+)using\s+using\s+var\s+', '$1using var '

    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $fixedCount = ([regex]::Matches($originalContent, 'using\s+using\s+var')).Count
        Write-Host "[FIXED] $($file.Name): $fixedCount instances" -ForegroundColor Green
        $totalFixed += $fixedCount
    }
}

Write-Host ""
Write-Host "Total double-using fixes applied: $totalFixed" -ForegroundColor Green
exit 0
