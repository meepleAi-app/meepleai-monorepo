# Issue #2184 - Generic Exception Handling Analysis Script
# Trova tutti i catch (Exception) senza #pragma warning disable CA1031

param(
    [string]$Path = "apps\api\src\Api",
    [switch]$ExcludeTests
)

Write-Host "=== Issue #2184: Generic Catch Analysis ===" -ForegroundColor Cyan
Write-Host "Analyzing: $Path" -ForegroundColor Yellow
Write-Host ""

$files = Get-ChildItem -Path $Path -Filter "*.cs" -Recurse

if ($ExcludeTests) {
    $files = $files | Where-Object { $_.FullName -notlike "*\tests\*" -and $_.FullName -notlike "*Tests.cs" }
}

$results = @()
$totalCatch = 0
$catchWithoutPragma = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "")

    # Trova tutti i catch (Exception)
    $catchMatches = [regex]::Matches($content, 'catch\s*\(\s*Exception\s+\w+\s*\)')

    if ($catchMatches.Count -gt 0) {
        $totalCatch += $catchMatches.Count

        # Controlla se c'è #pragma warning disable CA1031 prima
        $hasPragma = $content -match '#pragma\s+warning\s+disable\s+CA1031'

        if (-not $hasPragma) {
            $catchWithoutPragma += $catchMatches.Count

            $lineNumbers = @()
            $lines = $content -split "`n"
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match 'catch\s*\(\s*Exception\s+\w+\s*\)') {
                    $lineNumbers += ($i + 1)
                }
            }

            $results += [PSCustomObject]@{
                File = $relativePath
                CatchCount = $catchMatches.Count
                Lines = ($lineNumbers -join ", ")
                HasS2139Pragma = $content -match '#pragma\s+warning\s+disable\s+S2139'
            }
        }
    }
}

Write-Host "📊 Summary:" -ForegroundColor Green
Write-Host "  Total catch (Exception): $totalCatch" -ForegroundColor White
Write-Host "  Without CA1031 pragma: $catchWithoutPragma" -ForegroundColor Yellow
Write-Host "  Compliant: $($totalCatch - $catchWithoutPragma)" -ForegroundColor Green
Write-Host "  Files with issues: $($results.Count)" -ForegroundColor Yellow
Write-Host ""

if ($results.Count -gt 0) {
    Write-Host "❌ Non-compliant files:" -ForegroundColor Red
    $results | Format-Table -AutoSize

    # Export to CSV
    $csvPath = "claudedocs\issue-2184-inventory.csv"
    $results | Export-Csv -Path $csvPath -NoTypeInformation
    Write-Host "Exported to: $csvPath" -ForegroundColor Cyan
} else {
    Write-Host "✅ All catch (Exception) blocks are compliant!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Completion: $([math]::Round(($totalCatch - $catchWithoutPragma) / $totalCatch * 100, 2))%" -ForegroundColor Cyan
