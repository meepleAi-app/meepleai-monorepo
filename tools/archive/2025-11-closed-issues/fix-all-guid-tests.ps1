#!/usr/bin/env pwsh
# Comprehensive script to fix ALL Guid-related test errors

$ErrorActionPreference = "Stop"
$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

Write-Host "Starting comprehensive Guid test fixes..." -ForegroundColor Cyan

# Find all test files
$testFiles = Get-ChildItem -Path $testDir -Filter "*.cs" -Recurse | Where-Object { $_.Name -like "*Tests.cs" -or $_.Name -like "*Fixture.cs" }

$totalFiles = $testFiles.Count
$currentFile = 0

foreach ($file in $testFiles) {
    $currentFile++
    Write-Host "[$currentFile/$totalFiles] Processing: $($file.Name)" -ForegroundColor Yellow

    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    $changes = 0

    # Pattern 1: String ID declarations - convert to Guid
    # var userId = "user-123" → var userId = Guid.NewGuid()
    $pattern1 = 'var\s+(\w+Id)\s*=\s*"[^"]*"(?=\s*;)'
    if ($content -match $pattern1) {
        $content = $content -replace $pattern1, 'var $1 = Guid.NewGuid()'
        $changes++
    }

    # Pattern 2: Entity ID assignments - convert to Guid
    # Id = "xyz" → Id = Guid.NewGuid()
    $pattern2 = '(?<=\s)Id\s*=\s*"[^"]*"'
    if ($content -match $pattern2) {
        $content = $content -replace $pattern2, 'Id = Guid.NewGuid()'
        $changes++
    }

    # Pattern 3: GameId, UserId, AgentId string assignments
    # GameId = "xyz" → GameId = Guid.NewGuid()
    $pattern3 = '(GameId|UserId|AgentId|ChatId|SpecId|KeyId|SessionId|TemplateId|VersionId|OAuthAccountId)\s*=\s*"[^"]*"'
    if ($content -match $pattern3) {
        $content = $content -replace $pattern3, '$1 = Guid.NewGuid()'
        $changes++
    }

    # Pattern 4: Fix UserRole string assignments
    # Role = "admin" → Role = UserRole.Admin
    $roleMap = @{
        '"admin"' = 'UserRole.Admin'
        '"user"' = 'UserRole.User'
        '"editor"' = 'UserRole.Editor'
    }

    foreach ($oldRole in $roleMap.Keys) {
        if ($content -match "Role\s*=\s*$oldRole") {
            $content = $content -replace "Role\s*=\s*$oldRole", "Role = $($roleMap[$oldRole])"
            $changes++
        }
    }

    # Pattern 5: Fix method calls expecting string - add .ToString()
    # Service method calls that need string
    $stringMethods = @(
        'CreateApiKeyAsync',
        'GetUserKeysAsync',
        'GetKeyByIdAsync',
        'RevokeKeyAsync',
        'RenewKeyAsync',
        'GetActiveKeysCountAsync',
        'ValidateApiKeyAsync',
        'CreateSessionAsync',
        'GetSessionByTokenAsync',
        'ValidateSessionAsync',
        'RevokeSessionAsync',
        'GetActiveSessionsAsync',
        'LogEventAsync',
        'LogApiKeyAuthEventAsync',
        'CreateChatAsync',
        'GetChatHistoryAsync',
        'AddMessageAsync',
        'DeleteChatAsync',
        'GetUserChatsByGameAsync'
    )

    foreach ($method in $stringMethods) {
        # Find calls like: method(userId, ...) and replace with method(userId.ToString(), ...)
        $pattern = "($method\s*\(\s*)(\w+Id)(\s*[,)])"
        if ($content -match $pattern) {
            $content = $content -replace $pattern, '$1$2.ToString()$3'
            $changes++
        }
    }

    # Pattern 6: Fix tuple deconstruction type inference
    # var (plaintextKey, _) = await CreateApiKeyAsync(...)
    # → (string plaintextKey, _) = await CreateApiKeyAsync(...)
    $pattern6 = 'var\s+\(\s*plaintextKey\s*,\s*_\s*\)\s*=\s*await\s+CreateApiKeyAsync'
    if ($content -match $pattern6) {
        $content = $content -replace $pattern6, '(string plaintextKey, _) = await CreateApiKeyAsync'
        $changes++
    }

    # Pattern 7: Fix tuple with entity
    # var (plaintextKey, apiKeyEntity) = await CreateApiKeyAsync(...)
    # → (string plaintextKey, var apiKeyEntity) = await CreateApiKeyAsync(...)
    $pattern7 = 'var\s+\(\s*plaintextKey\s*,\s*apiKeyEntity\s*\)\s*=\s*await\s+CreateApiKeyAsync'
    if ($content -match $pattern7) {
        $content = $content -replace $pattern7, '(string plaintextKey, var apiKeyEntity) = await CreateApiKeyAsync'
        $changes++
    }

    # Pattern 8: Nullable Guid assignments
    # CreatedByUserId = "xyz" → CreatedByUserId = Guid.NewGuid()
    $pattern8 = '(CreatedByUserId|UpdatedByUserId|DeletedByUserId)\s*=\s*"[^"]*"'
    if ($content -match $pattern8) {
        $content = $content -replace $pattern8, '$1 = Guid.NewGuid()'
        $changes++
    }

    # Pattern 9: Fix AiResponseCacheService calls (expects string)
    # These methods specifically need .ToString()
    $cachePattern = '(GetCachedResponseAsync|CacheResponseAsync)\s*\(\s*(\w+)\s*,\s*(\w+)\s*,'
    if ($content -match $cachePattern) {
        $content = $content -replace $cachePattern, '$1($2.ToString(), $3.ToString(),'
        $changes++
    }

    # Pattern 10: Fix InvalidateCacheAsync calls
    $invalidatePattern = 'InvalidateCacheAsync\s*\(\s*(\w+)\s*\)'
    if ($content -match $invalidatePattern) {
        $content = $content -replace $invalidatePattern, 'InvalidateCacheAsync($1.ToString())'
        $changes++
    }

    # Pattern 11: Fix audit log userId parameters
    $auditPattern = 'LogEventAsync\s*\(\s*([^,]+),\s*(\w+Id)\s*,\s*"'
    if ($content -match $auditPattern) {
        $content = $content -replace $auditPattern, 'LogEventAsync($1, $2.ToString(), "'
        $changes++
    }

    # Only write if changes were made
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "  ✓ Applied $changes fix patterns" -ForegroundColor Green
    } else {
        Write-Host "  - No changes needed" -ForegroundColor Gray
    }
}

Write-Host "`nCompleted processing $totalFiles files" -ForegroundColor Cyan
Write-Host "Running build to check for remaining errors..." -ForegroundColor Yellow

# Run build to check results
$buildOutput = & dotnet build "$testDir\Api.Tests.csproj" 2>&1
$errorCount = ($buildOutput | Select-String "error CS").Count

if ($errorCount -eq 0) {
    Write-Host "`n✓ SUCCESS: All compilation errors fixed!" -ForegroundColor Green
} else {
    Write-Host "`n⚠ WARNING: $errorCount errors remaining" -ForegroundColor Yellow
    Write-Host "Top remaining errors:" -ForegroundColor Cyan
    $buildOutput | Select-String "error CS" | Select-Object -First 20
}
