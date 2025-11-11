#!/usr/bin/env pwsh
# MASSIVE GUID FIX - Process all 2096 errors systematically

param(
    [switch]$DryRun
)

$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"
$files = Get-ChildItem -Path $testDir -Filter "*.cs" -Recurse
$totalFixed = 0

Write-Host "Processing $($files.Count) test files..." -ForegroundColor Cyan

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $original = $content

    # FIX 1: String userId = "xxx" → Guid userId = Guid.NewGuid()
    $content = $content -replace 'var\s+(user|game|agent|chat|spec|key|session|template|version)Id\s*=\s*"[^"]*"', 'var $1Id = Guid.NewGuid()'

    # FIX 2: Entity Id = "xxx" → Id = Guid.NewGuid()
    $content = $content -replace '(?<=\s)Id\s*=\s*"[^"]*"', 'Id = Guid.NewGuid()'

    # FIX 3: Specific ID fields
    $content = $content -replace '(GameId|UserId|AgentId|ChatId|SpecId|KeyId|SessionId|TemplateId|VersionId|OAuthAccountId|CreatedByUserId|UpdatedByUserId|DeletedByUserId)\s*=\s*"[^"]*"', '$1 = Guid.NewGuid()'

    # FIX 4: Role = "admin" → Role = UserRole.Admin
    $content = $content -replace 'Role\s*=\s*"admin"', 'Role = UserRole.Admin'
    $content = $content -replace 'Role\s*=\s*"user"', 'Role = UserRole.User'
    $content = $content -replace 'Role\s*=\s*"editor"', 'Role = UserRole.Editor'

    # FIX 5: SeedUserAsync(dbContext, "user-id") → SeedUserAsync(dbContext, Guid.NewGuid())
    $content = $content -replace '(SeedUserAsync|SeedTestDataAsync|SeedDashboardDataAsync)\s*\([^,]+,\s*"[^"]*"', '$1($2, Guid.NewGuid()'

    # FIX 6: Method calls expecting string IDs - add .ToString()
    # CreateApiKeyAsync(userId, ...) → CreateApiKeyAsync(userId.ToString(), ...)
    $stringMethods = @(
        'CreateApiKeyAsync', 'GetUserKeysAsync', 'GetKeyByIdAsync', 'RevokeKeyAsync',
        'RenewKeyAsync', 'GetActiveKeysCountAsync', 'ValidateApiKeyAsync',
        'CreateSessionAsync', 'GetSessionByTokenAsync', 'ValidateSessionAsync',
        'RevokeSessionAsync', 'GetActiveSessionsAsync',
        'LogEventAsync', 'LogApiKeyAuthEventAsync',
        'CreateChatAsync', 'GetChatHistoryAsync', 'AddMessageAsync',
        'DeleteChatAsync', 'GetUserChatsByGameAsync', 'CreateConfigAsync',
        'GetCachedResponseAsync', 'CacheResponseAsync', 'InvalidateCacheAsync',
        'RegisterAndAuthenticateAsync', 'GetUserIdByEmailAsync'
    )

    foreach ($method in $stringMethods) {
        # Pattern: method(userId, → method(userId.ToString(),
        $content = $content -replace "($method\s*\(\s*)(\w+Id)(\s*[,)])", '$1$2.ToString()$3'
        # Pattern: method(xxx, userId, → method(xxx, userId.ToString(),
        $content = $content -replace "($method\s*\([^,]+,\s*)(\w+Id)(\s*[,)])", '$1$2.ToString()$3'
        # Pattern: method(xxx, xxx, userId → method(xxx, xxx, userId.ToString()
        $content = $content -replace "($method\s*\([^,]+,[^,]+,\s*)(\w+Id)(\s*[,)])", '$1$2.ToString()$3'
    }

    # FIX 7: Tuple deconstruction - var (plaintextKey, _) → (string plaintextKey, _)
    $content = $content -replace 'var\s+\(\s*plaintextKey\s*,\s*_\s*\)\s*=', '(string plaintextKey, _) ='
    $content = $content -replace 'var\s+\(\s*plaintextKey\s*,\s*apiKeyEntity\s*\)\s*=', '(string plaintextKey, var apiKeyEntity) ='

    # FIX 8: userId parameter in method signatures
    # private static async Task SeedUserAsync(MeepleAiDbContext dbContext, string userId)
    # → private static async Task SeedUserAsync(MeepleAiDbContext dbContext, Guid userId)
    $content = $content -replace '(\w+Async\s*\([^)]*,\s*)string\s+userId(\s*[,)])', '$1Guid userId$2'

    # FIX 9: Fix userId comparisons in LINQ
    # .Where(x => x.UserId == userId) when userId is now Guid
    # No change needed - these work with both types

    # FIX 10: Fix nullable Guid? assignments
    $content = $content -replace '(CreatedByUserId|UpdatedByUserId|DeletedByUserId)\s*=\s*"[^"]*"\s*(?=as Guid\?)', '$1 = Guid.NewGuid()'

    if ($content -ne $original) {
        if (-not $DryRun) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
        }
        $totalFixed++
        Write-Host "  ✓ Fixed: $($file.Name)" -ForegroundColor Green
    }
}

Write-Host "`nFixed $totalFixed files" -ForegroundColor Cyan

if (-not $DryRun) {
    Write-Host "`nBuilding to check results..." -ForegroundColor Yellow
    $buildOutput = & dotnet build "$testDir\Api.Tests.csproj" 2>&1 | Out-String
    $errorCount = ([regex]::Matches($buildOutput, "error CS")).Count

    if ($errorCount -eq 0) {
        Write-Host "`nRemaining errors: $errorCount" -ForegroundColor Green
    } else {
        Write-Host "`nRemaining errors: $errorCount" -ForegroundColor Yellow
        Write-Host "`nTop 20 remaining errors:" -ForegroundColor Cyan
        ($buildOutput -split [Environment]::NewLine) | Select-String "error CS" | Select-Object -First 20
    }
}
