<#
.SYNOPSIS
Fix CA2000 violations by adding 'using' keyword to HttpRequestMessage declarations

.DESCRIPTION
This script fixes CODE-01 violations by transforming:
  var request = new HttpRequestMessage(...)
To:
  using var request = new HttpRequestMessage(...)

Handles multiple patterns:
- Single-line declarations
- Multi-line declarations with object initializers
- All test files in apps/api/tests/Api.Tests/

.NOTES
Author: Claude Code
Issue: CODE-02 / #798
Date: 2025-11-07
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$TestsPath = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests",

    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

Write-Host "===== CODE-02: Fix HttpRequestMessage IDisposable Violations =====" -ForegroundColor Cyan
Write-Host ""

# Get all .cs files in the tests directory
$testFiles = Get-ChildItem -Path $TestsPath -Filter "*.cs" -Recurse -File

Write-Host "Found $($testFiles.Count) test files to process" -ForegroundColor Yellow
Write-Host ""

$totalFixed = 0
$filesModified = 0

foreach ($file in $testFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $originalContent = $content

    # Pattern 1: var <name> = new HttpRequestMessage(...);
    # Single line: var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/...");
    $pattern1 = '(\s+)var\s+(\w+)\s*=\s*new\s+HttpRequestMessage\('
    $replacement1 = '$1using var $2 = new HttpRequestMessage('

    # Pattern 2: var <name> = new HttpRequestMessage(...)
    # with object initializer spanning multiple lines
    $pattern2 = '(\s+)var\s+(\w+)\s*=\s*new\s+HttpRequestMessage\([^;]*\)\s*\{'
    $replacement2 = '$1using var $2 = new HttpRequestMessage($3){' # Preserve object initializer

    # Apply replacements
    $fixedCount = 0

    # Count matches before replacement
    $matches1 = [regex]::Matches($content, $pattern1)
    $matches2 = [regex]::Matches($content, $pattern2)

    if ($matches1.Count -gt 0 -or $matches2.Count -gt 0) {
        # Apply Pattern 1 (single-line)
        $content = [regex]::Replace($content, $pattern1, $replacement1)

        # Apply Pattern 2 (multi-line with object initializer)
        # Note: Pattern 2 is now covered by Pattern 1 since we match the opening (

        $fixedCount = $matches1.Count + $matches2.Count
    }

    # Only write if content changed
    if ($content -ne $originalContent) {
        if (-not $WhatIf) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
            Write-Host "[FIXED] $($file.Name): $fixedCount instances" -ForegroundColor Green
        }
        else {
            Write-Host "[WOULD FIX] $($file.Name): $fixedCount instances" -ForegroundColor Yellow
        }

        $totalFixed += $fixedCount
        $filesModified++
    }
}

Write-Host ""
Write-Host "===== Summary =====" -ForegroundColor Cyan
Write-Host "Files modified: $filesModified" -ForegroundColor $(if ($filesModified -gt 0) { "Green" } else { "Yellow" })
Write-Host "Total fixes applied: $totalFixed" -ForegroundColor $(if ($totalFixed -gt 0) { "Green" } else { "Yellow" })

if ($WhatIf) {
    Write-Host ""
    Write-Host "NOTE: This was a dry-run (--WhatIf). No files were actually modified." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Run: cd apps/api && dotnet build" -ForegroundColor White
Write-Host "2. Verify: dotnet build 2>&1 | grep CA2000 (should be empty)" -ForegroundColor White
Write-Host "3. Test: dotnet test" -ForegroundColor White

exit 0
