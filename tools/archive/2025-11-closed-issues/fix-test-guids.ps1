# Fix test files: Convert string IDs to Guid for DDD-PHASE2
param([switch]$DryRun)

$files = Get-ChildItem -Path "apps/api/tests" -Include "*.cs" -Recurse | Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\" }
$totalFixed = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $original = $content

    # Pattern 1: Id = "guid-string" → Id = Guid.NewGuid()
    $content = $content -replace 'Id\s*=\s*"[0-9a-fA-F-]+"', 'Id = Guid.NewGuid()'

    # Pattern 2: UserId = "guid" → UserId = Guid.NewGuid()
    $content = $content -replace '(UserId|GameId|RuleSpecId|ChatId|PdfDocumentId|TemplateId|VersionId|AgentId|ApiKeyId)\s*=\s*"[0-9a-fA-F-]+"', '$1 = Guid.NewGuid()'

    # Pattern 3: CreatedByUserId etc = "guid" → = Guid.NewGuid()
    $content = $content -replace '(CreatedByUserId|UpdatedByUserId|RevokedBy|DeletedByUserId)\s*=\s*"[0-9a-fA-F-]+"', '$1 = Guid.NewGuid()'

    # Pattern 4: UserRole.Admin → "admin"
    $content = $content -replace 'UserRole\.Admin', '"admin"'
    $content = $content -replace 'UserRole\.Editor', '"editor"'
    $content = $content -replace 'UserRole\.User', '"user"'

    # Pattern 5: Scopes = new[] { "read" } → Scopes = "read"
    $content = $content -replace 'Scopes\s*=\s*new\s*string?\[\]\s*\{\s*"([^"]+)"\s*\}', 'Scopes = "$1"'
    $content = $content -replace 'Scopes\s*=\s*new\s*\[\]\s*\{\s*"([^"]+)"\s*\}', 'Scopes = "$1"'
    $content = $content -replace 'Scopes\s*=\s*Array\.Empty<string>\(\)', 'Scopes = ""'

    if ($content -ne $original) {
        if (-not $DryRun) {
            Set-Content $file.FullName -Value $content -NoNewline
        }
        $totalFixed++
        Write-Host "Fixed: $($file.Name)" -ForegroundColor Green
    }
}

Write-Host "`nTotal files fixed: $totalFixed" -ForegroundColor Cyan
if ($DryRun) { Write-Host "[DRY RUN] No changes applied" -ForegroundColor Yellow }
