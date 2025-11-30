# Fix MA0004 warnings: Add .ConfigureAwait(false) to await calls
# Usage: pwsh tools/fix-ma0004.ps1 [-PathFilter "BoundedContexts"] [-DryRun]

param(
    [string]$PathFilter = "",
    [switch]$DryRun = $false
)

$basePath = "apps/api/src/Api"
if ($PathFilter) {
    $searchPath = Join-Path $basePath $PathFilter
} else {
    $searchPath = $basePath
}

Write-Host "Scanning: $searchPath" -ForegroundColor Cyan

$files = Get-ChildItem -Path $searchPath -Recurse -Filter "*.cs" -ErrorAction SilentlyContinue

$totalChanges = 0
$filesModified = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Pattern: await [expression]; (without ConfigureAwait already)
    # Match await statements that don't already have ConfigureAwait
    $pattern = '(await\s+[^;\r\n]+?)(\s*;)'
    $regex = [regex]$pattern

    $newContent = $regex.Replace($content, {
        param($match)
        $awaitExpr = $match.Groups[1].Value
        $semicolon = $match.Groups[2].Value

        # Skip if already has ConfigureAwait
        if ($awaitExpr -match 'ConfigureAwait') {
            return $match.Value
        }

        # Skip if it's a simple value await (not a task)
        if ($awaitExpr -match 'await\s+(true|false|null|\d+|"[^"]*")') {
            return $match.Value
        }

        # Add ConfigureAwait(false) before semicolon
        return $awaitExpr + ".ConfigureAwait(false)" + $semicolon
    })

    if ($newContent -ne $originalContent) {
        $changes = ($originalContent.Length - $newContent.Length) / -".ConfigureAwait(false)".Length
        $filesModified++
        $totalChanges += [Math]::Abs($changes)

        Write-Host "  $($file.Name): ~$([Math]::Round($changes)) changes" -ForegroundColor Yellow

        if (-not $DryRun) {
            Set-Content -Path $file.FullName -Value $newContent -NoNewline
        }
    }
}

Write-Host "`nSummary:" -ForegroundColor Green
Write-Host "   Path filter: $PathFilter" -ForegroundColor White
Write-Host "   Files modified: $filesModified" -ForegroundColor White
Write-Host "   Estimated changes: ~$totalChanges" -ForegroundColor White

if ($DryRun) {
    Write-Host "`nDRY RUN - No files modified. Remove -DryRun to apply changes." -ForegroundColor Yellow
} else {
    Write-Host "`nChanges applied successfully!" -ForegroundColor Green
}
