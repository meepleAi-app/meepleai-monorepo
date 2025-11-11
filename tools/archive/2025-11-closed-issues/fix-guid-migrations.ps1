# PowerShell script to automatically fix common Guid/string conversion patterns
# DDD-PHASE2: Automated fixes for entity ID type changes

param(
    [switch]$DryRun,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$filesFixed = 0
$totalChanges = 0

Write-Host "Starting automated Guid/string conversion fixes..." -ForegroundColor Cyan

# Get all C# files in Services, Routing, Models
$targetFiles = Get-ChildItem -Path "apps/api/src/Api" -Include "*.cs" -Recurse |
    Where-Object {
        $_.FullName -match "(Services|Routing|Models)" -and
        $_.FullName -notmatch "(Migrations|bin|obj|BoundedContexts)"
    }

Write-Host "Found $($targetFiles.Count) files to process" -ForegroundColor Yellow

foreach ($file in $targetFiles) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    $fileChanges = 0

    # Pattern 1: UserRole enum to string comparisons
    $content = $content -replace 'UserRole\.Admin', '"admin"'
    $content = $content -replace 'UserRole\.Editor', '"editor"'
    $content = $content -replace 'UserRole\.User', '"user"'

    # Pattern 2: Guid entity creation - Id = Guid.NewGuid().ToString() → Id = Guid.NewGuid()
    $content = $content -replace 'Id\s*=\s*Guid\.NewGuid\(\)\.ToString\(\s*"N"\s*\)', 'Id = Guid.NewGuid()'
    $content = $content -replace 'Id\s*=\s*Guid\.NewGuid\(\)\.ToString\(\)', 'Id = Guid.NewGuid()'

    # Pattern 3: UserId assignments in entity creation
    $content = $content -replace '(\s+)UserId\s*=\s*userId\.ToString\(\)', '$1UserId = userId'
    $content = $content -replace '(\s+)GameId\s*=\s*gameId\.ToString\(\)', '$1GameId = gameId'
    $content = $content -replace '(\s+)RuleSpecId\s*=\s*ruleSpecId\.ToString\(\)', '$1RuleSpecId = ruleSpecId'

    # Pattern 4: DTO mappings - entity.Id (Guid) to string in DTO
    # Keep .ToString() in DTOs if needed, but remove from entity assignments

    # Pattern 5: Scopes array to comma-separated string
    $content = $content -replace 'Scopes\s*=\s*new\s*\[\]\s*\{\s*"([^"]+)"\s*\}', 'Scopes = "$1"'
    $content = $content -replace 'Scopes\s*=\s*new\s*string\[\]\s*\{\s*"([^"]+)"\s*\}', 'Scopes = "$1"'
    $content = $content -replace 'Scopes\s*=\s*Array\.Empty<string>\(\)', 'Scopes = ""'

    if ($content -ne $originalContent) {
        $fileChanges = ($content.ToCharArray() | Where-Object { $_ -ne $originalContent[$content.IndexOf($_)] }).Count

        if (-not $DryRun) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
        }

        $filesFixed++
        $totalChanges += $fileChanges

        if ($Verbose) {
            Write-Host "  Fixed: $($file.Name) ($fileChanges changes)" -ForegroundColor Green
        }
    }
}

Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "  Files processed: $($targetFiles.Count)" -ForegroundColor White
Write-Host "  Files fixed: $filesFixed" -ForegroundColor Green
Write-Host "  Total changes: $totalChanges" -ForegroundColor Green

if ($DryRun) {
    Write-Host "`n[DRY RUN] No files were modified. Run without -DryRun to apply changes." -ForegroundColor Yellow
}

Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "  1. Run: dotnet build" -ForegroundColor White
Write-Host "  2. Check remaining errors" -ForegroundColor White
Write-Host "  3. Fix edge cases manually" -ForegroundColor White
