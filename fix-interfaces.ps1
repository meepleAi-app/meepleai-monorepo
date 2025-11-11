# Script to fix interface signatures to match implementations
$ErrorActionPreference = "Stop"

Write-Host "Fixing interface signatures..." -ForegroundColor Yellow

# Fix IConfigurationService.cs
$file = "apps/api/src/Api/Services/IConfigurationService.cs"
$fullPath = Join-Path $PSScriptRoot $file
if (Test-Path $fullPath) {
    $content = Get-Content $fullPath -Raw

    # Fix CreateConfigurationAsync
    $content = $content -replace '(Task<SystemConfigurationDto> CreateConfigurationAsync\(CreateConfigurationRequest request, )string userId', '$1Guid userId'

    # Fix UpdateConfigurationAsync
    $content = $content -replace '(Task<SystemConfigurationDto\?> UpdateConfigurationAsync\(string id, UpdateConfigurationRequest request, )string userId', '$1Guid userId'

    # Fix ToggleConfigurationAsync
    $content = $content -replace '(Task<SystemConfigurationDto\?> ToggleConfigurationAsync\(string id, bool isActive, )string userId', '$1Guid userId'

    # Fix BulkUpdateConfigurationsAsync
    $content = $content -replace '(Task<IReadOnlyList<SystemConfigurationDto>> BulkUpdateConfigurationsAsync\(\s*BulkConfigurationUpdateRequest request,\s*)string userId', '$1Guid userId'

    # Fix ImportConfigurationsAsync
    $content = $content -replace '(Task<int> ImportConfigurationsAsync\(ConfigurationImportRequest request, )string userId', '$1Guid userId'

    # Fix RollbackConfigurationAsync
    $content = $content -replace '(Task<SystemConfigurationDto\?> RollbackConfigurationAsync\(string configurationId, int toVersion, )string userId', '$1Guid userId'

    Set-Content $fullPath -Value $content -NoNewline
    Write-Host "Fixed: $file" -ForegroundColor Green
}

# Fix IOAuthService.cs
$file = "apps/api/src/Api/Services/IOAuthService.cs"
$fullPath = Join-Path $PSScriptRoot $file
if (Test-Path $fullPath) {
    $content = Get-Content $fullPath -Raw

    # Fix UnlinkOAuthAccountAsync
    $content = $content -replace '(Task UnlinkOAuthAccountAsync\()string userId', '$1Guid userId'

    # Fix GetLinkedAccountsAsync
    $content = $content -replace '(Task<List<OAuthAccountDto>> GetLinkedAccountsAsync\()string userId', '$1Guid userId'

    # Fix RefreshTokenAsync
    $content = $content -replace '(Task<OAuthTokenResponse\?> RefreshTokenAsync\()string userId', '$1Guid userId'

    Set-Content $fullPath -Value $content -NoNewline
    Write-Host "Fixed: $file" -ForegroundColor Green
}

# Fix ITempSessionService.cs
$file = "apps/api/src/Api/Services/ITempSessionService.cs"
$fullPath = Join-Path $PSScriptRoot $file
if (Test-Path $fullPath) {
    $content = Get-Content $fullPath -Raw

    # Fix CreateTempSessionAsync
    $content = $content -replace '(Task<string> CreateTempSessionAsync\()string userId', '$1Guid userId'

    Set-Content $fullPath -Value $content -NoNewline
    Write-Host "Fixed: $file" -ForegroundColor Green
}

Write-Host "`nAll interfaces fixed. Running build..." -ForegroundColor Cyan
cd apps/api
dotnet build --no-restore 2>&1 | Select-String "error" | Select-Object -First 20
