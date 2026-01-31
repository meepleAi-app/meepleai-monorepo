# GUID Test Fixer - Comprehensive

$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"
$files = Get-ChildItem -Path $testDir -Filter *.cs -Recurse
$fixed = 0

Write-Host "Processing $($files.Count) files..."

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $original = $content

    # Fix 1: var userId = "xxx" -> var userId = Guid.NewGuid()
    $content = $content -creplace 'var\s+(user|game|agent|chat|spec|key|session|template|version)Id\s*=\s*"[^"]*"', 'var $1Id = Guid.NewGuid()'

    # Fix 2: Id = "xxx" -> Id = Guid.NewGuid()
    $content = $content -creplace '(?<=\s)Id\s*=\s*"[^"]*"', 'Id = Guid.NewGuid()'

    # Fix 3: Specific ID properties
    $content = $content -creplace '(GameId|UserId|AgentId|ChatId|SpecId|KeyId|SessionId|TemplateId|VersionId|CreatedByUserId|UpdatedByUserId)\s*=\s*"[^"]*"', '$1 = Guid.NewGuid()'

    # Fix 4: Role enum
    $content = $content -creplace 'Role\s*=\s*"admin"', 'Role = UserRole.Admin'
    $content = $content -creplace 'Role\s*=\s*"user"', 'Role = UserRole.User'
    $content = $content -creplace 'Role\s*=\s*"editor"', 'Role = UserRole.Editor'

    # Fix 5: Method parameter types
    $content = $content -creplace 'SeedUserAsync\s*\([^,]+,\s*string\s+userId', 'SeedUserAsync($1, Guid userId'

    # Fix 6: Add .ToString() to method calls
    $methods = 'CreateApiKeyAsync|GetUserKeysAsync|RevokeKeyAsync|CreateSessionAsync|ValidateSessionAsync|RevokeSessionAsync|GetActiveSessionsAsync|LogEventAsync|CreateChatAsync|GetChatHistoryAsync|AddMessageAsync|DeleteChatAsync|GetUserChatsByGameAsync|CreateConfigAsync|GetCachedResponseAsync|CacheResponseAsync|InvalidateCacheAsync|RegisterAndAuthenticateAsync|GetUserIdByEmailAsync'

    $content = $content -creplace "($methods)\s*\(\s*(\w+Id)(\s*[,)])", '$1($2.ToString()$3'
    $content = $content -creplace "($methods)\s*\(([^,]+),\s*(\w+Id)(\s*[,)])", '$1($2, $3.ToString()$4'
    $content = $content -creplace "($methods)\s*\(([^,]+),([^,]+),\s*(\w+Id)(\s*[,)])", '$1($2,$3, $4.ToString()$5'

    # Fix 7: Tuple deconstruction
    $content = $content -creplace 'var\s+\(\s*plaintextKey\s*,\s*_\s*\)\s*=', '(string plaintextKey, _) ='
    $content = $content -creplace 'var\s+\(\s*plaintextKey\s*,\s*apiKeyEntity\s*\)\s*=', '(string plaintextKey, var apiKeyEntity) ='

    if ($content -ne $original) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $fixed++
        Write-Host "Fixed: $($file.Name)"
    }
}

Write-Host "`n$fixed files fixed"
Write-Host "Building..."

cd "D:\Repositories\meepleai-monorepo\apps\api"
$result = dotnet build tests/Api.Tests/Api.Tests.csproj 2>&1
$errors = $result | Select-String "error CS"
Write-Host "$($errors.Count) errors remaining"

if ($errors.Count -gt 0 -and $errors.Count -lt 50) {
    $errors | ForEach-Object { Write-Host $_ }
}
