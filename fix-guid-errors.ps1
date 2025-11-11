# Script to fix all Guid vs string compilation errors
$ErrorActionPreference = "Stop"

$files = @(
    "apps/api/src/Api/Services/OAuthService.cs",
    "apps/api/src/Api/Services/PromptManagementService.cs",
    "apps/api/src/Api/Services/ConfigurationService.cs",
    "apps/api/src/Api/Services/TempSessionService.cs",
    "apps/api/src/Api/Services/PasswordResetService.cs",
    "apps/api/src/Api/Services/N8nConfigService.cs",
    "apps/api/src/Api/Services/PdfStorageService.cs"
)

foreach ($file in $files) {
    $fullPath = Join-Path $PSScriptRoot $file
    Write-Host "Fixing: $fullPath" -ForegroundColor Yellow

    if (Test-Path $fullPath) {
        $content = Get-Content $fullPath -Raw

        # Fix GetLinkedAccountsAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<List<OAuthAccountDto>> GetLinkedAccountsAsync\()string userId', '$1Guid userId'

        # Fix UnlinkOAuthAccountAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task UnlinkOAuthAccountAsync\()string userId', '$1Guid userId'

        # Fix RefreshTokenAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<OAuthTokenResponse\?> RefreshTokenAsync\()string userId', '$1Guid userId'

        # Fix CreateOAuthAccountAsync - change string userId to Guid userId
        $content = $content -replace '(private async Task CreateOAuthAccountAsync\(\s*string userId)', 'private async Task CreateOAuthAccountAsync(Guid userId'

        # Fix entity ID creation - Guid.NewGuid().ToString() to Guid.NewGuid()
        $content = $content -replace 'Id = Guid\.NewGuid\(\)\.ToString\(\)', 'Id = Guid.NewGuid()'

        # Fix UserId assignment in entities - userId.ToString() to userId
        $content = $content -replace 'UserId = userId\.ToString\(\)', 'UserId = userId'

        # Fix CreatePromptTemplateAsync - change string createdByUserId to Guid createdByUserId
        $content = $content -replace '(CreatePromptTemplateRequest request,\s*string createdByUserId)', 'CreatePromptTemplateRequest request, Guid createdByUserId'

        # Fix CreatePromptVersionAsync - change string createdByUserId to Guid createdByUserId
        $content = $content -replace '(string templateId,\s*CreatePromptVersionRequest request,\s*string createdByUserId)', 'string templateId, CreatePromptVersionRequest request, Guid createdByUserId'

        # Fix ActivateVersionAsync - change string activatedByUserId to Guid activatedByUserId
        $content = $content -replace '(string templateId,\s*string versionId,\s*string activatedByUserId)', 'string templateId, string versionId, Guid activatedByUserId'

        # Fix CreateConfigurationAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<SystemConfigurationDto> CreateConfigurationAsync\(CreateConfigurationRequest request, )string userId', '$1Guid userId'

        # Fix UpdateConfigurationAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<SystemConfigurationDto\?> UpdateConfigurationAsync\(\s*string id,\s*UpdateConfigurationRequest request,\s*string userId)', 'public async Task<SystemConfigurationDto?> UpdateConfigurationAsync(string id, UpdateConfigurationRequest request, Guid userId'

        # Fix ToggleConfigurationAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<SystemConfigurationDto\?> ToggleConfigurationAsync\(string id, bool isActive, )string userId', '$1Guid userId'

        # Fix BulkUpdateConfigurationsAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<IReadOnlyList<SystemConfigurationDto>> BulkUpdateConfigurationsAsync\(\s*BulkConfigurationUpdateRequest request,\s*string userId)', 'public async Task<IReadOnlyList<SystemConfigurationDto>> BulkUpdateConfigurationsAsync(BulkConfigurationUpdateRequest request, Guid userId'

        # Fix ImportConfigurationsAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<int> ImportConfigurationsAsync\(ConfigurationImportRequest request, )string userId', '$1Guid userId'

        # Fix RollbackConfigurationAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<SystemConfigurationDto\?> RollbackConfigurationAsync\(\s*string configurationId,\s*int toVersion,\s*string userId)', 'public async Task<SystemConfigurationDto?> RollbackConfigurationAsync(string configurationId, int toVersion, Guid userId'

        # Fix CreateTempSessionAsync - change string userId to Guid userId
        $content = $content -replace '(public async Task<string> CreateTempSessionAsync\()string userId', '$1Guid userId'

        # Fix RequestPasswordResetAsync - UserId assignment
        $content = $content -replace '(\s+UserId = )user\.Id', '$1user.Id'

        # Fix PdfStorageService - UploadPdfAsync gameId and userId
        $content = $content -replace '(public async Task<PdfUploadResult> UploadPdfAsync\(\s*string gameId,\s*string userId)', 'public async Task<PdfUploadResult> UploadPdfAsync(string gameId, Guid userId'

        Set-Content $fullPath -Value $content -NoNewline
        Write-Host "Fixed: $file" -ForegroundColor Green
    }
}

Write-Host "`nAll files processed. Running build to verify..." -ForegroundColor Cyan
cd apps/api
dotnet build --no-restore | Select-String "error"
