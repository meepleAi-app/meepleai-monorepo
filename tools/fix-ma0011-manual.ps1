# Fix MA0011 warnings: Add IFormatProvider to Parse/ToString/ToLower/ToUpper
# Usage: pwsh tools/fix-ma0011-manual.ps1 [-DryRun]
#
# Strategy: Parse build warnings, apply safe transformations with CultureInfo.InvariantCulture
# Safe for: Backend API (no user-facing localized text)

param(
    [switch]$DryRun = $false
)

$projectPath = "apps/api/src/Api/Api.csproj"

Write-Host "Collecting MA0011 warnings..." -ForegroundColor Cyan
$buildOutput = dotnet build "$projectPath" --no-incremental 2>&1
$warnings = $buildOutput | Select-String "warning MA0011"

if ($warnings.Count -eq 0) {
    Write-Host "No MA0011 warnings found!" -ForegroundColor Green
    exit 0
}

Write-Host "Found $($warnings.Count) MA0011 warnings" -ForegroundColor Yellow

# Group warnings by file
$fileWarnings = @{}
foreach ($warning in $warnings) {
    if ($warning -match '(.+\.cs)\((\d+),(\d+)\): warning MA0011: (.+) \(') {
        $filePath = $matches[1]
        $lineNum = [int]$matches[2]
        $colNum = [int]$matches[3]
        $message = $matches[4]

        if (-not $fileWarnings.ContainsKey($filePath)) {
            $fileWarnings[$filePath] = @()
        }
        $fileWarnings[$filePath] += @{
            Line = $lineNum
            Column = $colNum
            Message = $message
        }
    }
}

Write-Host "`nProcessing $($fileWarnings.Count) files..." -ForegroundColor Cyan

$filesModified = 0
$totalFixes = 0
$skipped = 0

# Convert to array to avoid collection modification issues
$filePaths = @($fileWarnings.Keys)

foreach ($filePath in $filePaths) {
    $lines = Get-Content $filePath
    $fileWarnings[$filePath] = $fileWarnings[$filePath] | Sort-Object -Property Line -Descending
    $fileChanges = 0

    foreach ($warn in $fileWarnings[$filePath]) {
        $idx = $warn.Line - 1
        $line = $lines[$idx]
        $originalLine = $line

        # Pattern 1: .ToString() → .ToString(CultureInfo.InvariantCulture)
        if ($line -match '(\w+)\.ToString\(\)' -and $line -notmatch 'CultureInfo') {
            $line = $line -replace '\.ToString\(\)', '.ToString(CultureInfo.InvariantCulture)'
            $fileChanges++
        }
        # Pattern 2: int.Parse(x) → int.Parse(x, CultureInfo.InvariantCulture)
        elseif ($line -match '(int|long|decimal|double|float)\.Parse\(([^)]+)\)' -and $line -notmatch 'CultureInfo') {
            $type = $matches[1]
            $arg = $matches[2]
            $line = $line -replace "$type\.Parse\($arg\)", "$type.Parse($arg, CultureInfo.InvariantCulture)"
            $fileChanges++
        }
        # Pattern 3: Type.TryParse(x, out y) → Type.TryParse(x, CultureInfo.InvariantCulture, out y)
        elseif ($line -match '(int|long|decimal|double|float|DateTime)\.TryParse\(([^,]+),\s*out\s+' -and $line -notmatch 'CultureInfo') {
            $type = $matches[1]
            $arg = $matches[2]
            # Insert CultureInfo after first argument
            $line = $line -replace "$type\.TryParse\($arg,\s*out\s+", "$type.TryParse($arg, CultureInfo.InvariantCulture, out "
            $fileChanges++
        }
        # Pattern 4: .ToLower() → .ToLower(CultureInfo.InvariantCulture)
        elseif ($line -match '\.ToLower\(\)' -and $line -notmatch 'CultureInfo') {
            $line = $line -replace '\.ToLower\(\)', '.ToLower(CultureInfo.InvariantCulture)'
            $fileChanges++
        }
        # Pattern 5: .ToUpper() → .ToUpper(CultureInfo.InvariantCulture)
        elseif ($line -match '\.ToUpper\(\)' -and $line -notmatch 'CultureInfo') {
            $line = $line -replace '\.ToUpper\(\)', '.ToUpper(CultureInfo.InvariantCulture)'
            $fileChanges++
        }
        else {
            Write-Host "  SKIP (complex): Line $($warn.Line) - $($warn.Message)" -ForegroundColor DarkYellow
            $skipped++
        }

        $lines[$idx] = $line
    }

    if ($fileChanges -gt 0) {
        $filesModified++
        $totalFixes += $fileChanges

        $fileName = Split-Path $filePath -Leaf
        Write-Host "  $fileName`: $fileChanges changes" -ForegroundColor Yellow

        if (-not $DryRun) {
            $lines | Set-Content -Path $filePath -NoNewline
        }
    }
}

Write-Host "`nSummary:" -ForegroundColor Green
Write-Host "   Files modified: $filesModified" -ForegroundColor White
Write-Host "   Total fixes: $totalFixes" -ForegroundColor White
Write-Host "   Skipped (complex): $skipped" -ForegroundColor DarkYellow

if ($DryRun) {
    Write-Host "`nDRY RUN - No files modified. Remove -DryRun to apply changes." -ForegroundColor Yellow
} else {
    Write-Host "`nChanges applied!" -ForegroundColor Green
    Write-Host "IMPORTANT: Add 'using System.Globalization;' to modified files!" -ForegroundColor Cyan
    Write-Host "Run 'dotnet build' to verify fixes." -ForegroundColor Cyan
}
