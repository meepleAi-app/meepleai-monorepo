<#
.SYNOPSIS
    Remove #region and #endregion directives from C# files

.DESCRIPTION
    This script removes all #region and #endregion preprocessor directives from C# files.
    These directives are purely visual aids for code editors and do not affect compilation.
    Removing them improves code readability by reducing visual clutter.

.PARAMETER Path
    The root path to search for C# files. Defaults to "apps/api"

.PARAMETER DryRun
    If specified, shows what would be changed without actually modifying files

.EXAMPLE
    .\remove-regions.ps1 -DryRun
    Preview changes without modifying files

.EXAMPLE
    .\remove-regions.ps1 -Path "apps/api"
    Remove regions from all C# files in apps/api

.NOTES
    Related to Issue #1507 - Remove excessive #region directives from backend tests
#>

param(
    [Parameter(Mandatory=$false)]
    [switch]$DryRun,

    [Parameter(Mandatory=$false)]
    [string]$Path = "apps/api"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Remove C# #region Directives ===" -ForegroundColor Cyan
Write-Host "Path: $Path" -ForegroundColor Gray
Write-Host "Mode: $(if ($DryRun) { 'DRY RUN (preview only)' } else { 'APPLY CHANGES' })" -ForegroundColor $(if ($DryRun) { 'Yellow' } else { 'Green' })
Write-Host ""

# Find all C# files, excluding obj and bin directories
$files = Get-ChildItem -Path $Path -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }

Write-Host "Found $($files.Count) C# files to process" -ForegroundColor Gray
Write-Host ""

$totalFilesModified = 0
$totalLinesRemoved = 0
$filesWithRegions = @()

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $originalContent = $content
    $originalLines = $content.Split("`n").Count

    # Remove #region lines (with any trailing comments/names)
    $content = $content -replace '(?m)^\s*#\s*region\b.*$[\r\n]*', ''

    # Remove #endregion lines (with any trailing comments)
    $content = $content -replace '(?m)^\s*#\s*endregion\b.*$[\r\n]*', ''

    # Only process if changes were made
    if ($content -ne $originalContent) {
        $newLines = $content.Split("`n").Count
        $linesRemoved = $originalLines - $newLines
        $totalFilesModified++
        $totalLinesRemoved += $linesRemoved
        $filesWithRegions += [PSCustomObject]@{
            File = $file.FullName.Replace((Get-Location).Path + "\", "")
            LinesRemoved = $linesRemoved
        }

        if ($DryRun) {
            Write-Host "[DRY RUN] Would modify: $($file.Name) (-$linesRemoved lines)" -ForegroundColor Yellow
        } else {
            # Write back to file with UTF8 encoding (no BOM) to match project standards
            [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]($false))
            Write-Host "[MODIFIED] $($file.Name) (-$linesRemoved lines)" -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Files processed: $($files.Count)" -ForegroundColor Gray
Write-Host "Files modified: $totalFilesModified" -ForegroundColor $(if ($DryRun) { 'Yellow' } else { 'Green' })
Write-Host "Total lines removed: $totalLinesRemoved" -ForegroundColor $(if ($DryRun) { 'Yellow' } else { 'Green' })

if ($totalFilesModified -gt 0 -and -not $DryRun) {
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Review changes: git diff" -ForegroundColor Gray
    Write-Host "2. Run tests: cd apps/api && dotnet test" -ForegroundColor Gray
    Write-Host "3. Commit: git add . && git commit -m 'refactor: remove #region directives from backend [Issue #1507]'" -ForegroundColor Gray
}

if ($DryRun) {
    Write-Host ""
    Write-Host "Run without -DryRun to apply changes" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Top 10 files with most regions removed:" -ForegroundColor Cyan
$filesWithRegions | Sort-Object -Property LinesRemoved -Descending | Select-Object -First 10 | Format-Table -AutoSize
