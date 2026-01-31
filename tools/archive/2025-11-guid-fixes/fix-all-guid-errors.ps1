# Fix all GUID-related compilation errors
# This script fixes type mismatches between Guid and string

$ErrorActionPreference = "Stop"

Write-Host "Fixing all GUID compilation errors..." -ForegroundColor Cyan

$apiPath = "D:\Repositories\meepleai-monorepo\apps\api\src\Api"

# Pattern 1: Add missing userGuid variable declaration
Write-Host "`nFixing missing userGuid declarations..." -ForegroundColor Yellow
$files = @(
    "$apiPath\Services\FeatureFlagService.cs"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Fix DisableFeatureAsync - add userGuid before if statement
        $pattern = '(\s+// Check if configuration exists\s+var existing = await _configService\.GetConfigurationByKeyAsync\(key\);)\s+(if \(existing != null\))'
        $replacement = '$1' + "`n`n        var userGuid = userId != null && Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;`n`n        " + '$2'
        $content = $content -replace $pattern, $replacement

        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "  Fixed: $($file.Split('\')[-1])" -ForegroundColor Green
    }
}

# Pattern 2: Fix Guid == string comparisons (change to entity.UserId == userId)
Write-Host "`nFixing Guid == string comparisons..." -ForegroundColor Yellow
$comparisonFiles = @(
    "$apiPath\Routing\PdfEndpoints.cs",
    "$apiPath\Routing\AiEndpoints.cs",
    "$apiPath\Services\PdfStorageService.cs",
    "$apiPath\Services\SetupGuideService.cs",
    "$apiPath\Services\PdfIndexingService.cs",
    "$apiPath\Services\RuleSpecService.cs",
    "$apiPath\Services\RuleSpecCommentService.cs",
    "$apiPath\Services\RuleCommentService.cs",
    "$apiPath\Services\PromptManagementService.cs",
    "$apiPath\Services\PromptTemplateService.cs",
    "$apiPath\Services\PromptEvaluationService.cs"
)

foreach ($file in $comparisonFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Pattern: entity.UserId == currentUserId where currentUserId is string
        # Change to: entity.UserId.ToString() == currentUserId
        $content = $content -replace '(\w+\.UserId)\s*==\s*(\w+UserId)(?!\.)', '$1.ToString() == $2'
        $content = $content -replace '(\w+\.UserId)\s*!=\s*(\w+UserId)(?!\.)', '$1.ToString() != $2'

        # Pattern: entity.GameId == gameId where gameId is string
        $content = $content -replace '(\w+\.GameId)\s*==\s*(\w+Id)(?!\.)', '$1.ToString() == $2'

        # Pattern: entity.Id == stringId
        $content = $content -replace '(\w+\.Id)\s*==\s*([a-z]\w+Id)(?!\.)(?!\()', '$1.ToString() == $2'

        # Pattern: template.Id == templateId where templateId is string
        $content = $content -replace '(template\.Id)\s*==\s*(templateId)(?!\.)', '$1.ToString() == $2'

        # Pattern: version.Id == versionId where versionId is string
        $content = $content -replace '(version\.Id)\s*==\s*(versionId)(?!\.)', '$1.ToString() == $2'

        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "  Fixed comparisons: $($file.Split('\')[-1])" -ForegroundColor Green
    }
}

# Pattern 3: Fix string to Guid assignments (parse with Guid.Parse)
Write-Host "`nFixing string to Guid assignments..." -ForegroundColor Yellow
$assignmentFiles = @(
    "$apiPath\Services\PdfStorageService.cs",
    "$apiPath\Services\PdfIndexingService.cs",
    "$apiPath\Services\RuleSpecService.cs",
    "$apiPath\Services\RuleSpecCommentService.cs",
    "$apiPath\Services\RuleCommentService.cs",
    "$apiPath\Services\PasswordResetService.cs",
    "$apiPath\Services\PromptManagementService.cs",
    "$apiPath\Services\PromptTemplateService.cs",
    "$apiPath\Services\PromptEvaluationService.cs"
)

foreach ($file in $assignmentFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Pattern: GameId = gameId where gameId is string
        $content = $content -replace '(\s+GameId\s*=\s*)([a-z]\w+Id)(?!\.)', '$1Guid.Parse($2)'

        # Pattern: UserId = userId where userId is string
        $content = $content -replace '(\s+UserId\s*=\s*)([a-z]\w+Id)(?!\.)', '$1Guid.Parse($2)'

        # Pattern: RuleSpecId = ruleSpecId
        $content = $content -replace '(\s+RuleSpecId\s*=\s*)([a-z]\w+Id)(?!\.)', '$1Guid.Parse($2)'

        # Pattern: ParentCommentId = parentCommentId (nullable)
        $content = $content -replace '(\s+ParentCommentId\s*=\s*)([a-z]\w+Id)(?!\.)', '$1string.IsNullOrEmpty($2) ? null : Guid.Parse($2)'

        # Pattern: TemplateId = templateId
        $content = $content -replace '(\s+TemplateId\s*=\s*)([a-z]\w+Id)(?!\.)', '$1Guid.Parse($2)'

        # Pattern: VersionId = versionId
        $content = $content -replace '(\s+VersionId\s*=\s*)([a-z]\w+Id)(?!\.)', '$1Guid.Parse($2)'

        # Pattern: PromptTemplateId = promptTemplateId
        $content = $content -replace '(\s+PromptTemplateId\s*=\s*)([a-z]\w+Id)(?!\.)', '$1Guid.Parse($2)'

        # Pattern: PromptVersionId = promptVersionId
        $content = $content -replace '(\s+PromptVersionId\s*=\s*)([a-z]\w+Id)(?!\.)', '$1Guid.Parse($2)'

        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "  Fixed assignments: $($file.Split('\')[-1])" -ForegroundColor Green
    }
}

# Pattern 4: Fix Guid to string in DTOs (add .ToString())
Write-Host "`nFixing Guid to string in DTOs..." -ForegroundColor Yellow
$dtoFiles = @(
    "$apiPath\Services\TempSessionService.cs",
    "$apiPath\Services\PasswordResetService.cs",
    "$apiPath\Services\PdfStorageService.cs",
    "$apiPath\Services\ConfigurationService.cs",
    "$apiPath\Services\PromptManagementService.cs"
)

foreach ($file in $dtoFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Pattern: UserId = entity.UserId in DTO construction
        $content = $content -replace '(\s+UserId:\s*)(\w+\.UserId)(?!\.ToString)', '$1$2.ToString()'

        # Pattern: GameId = entity.GameId in DTO construction
        $content = $content -replace '(\s+GameId:\s*)(\w+\.GameId)(?!\.ToString)', '$1$2.ToString()'

        # Pattern: Id = entity.Id in DTO construction
        $content = $content -replace '(\s+Id:\s*)(\w+\.Id)(?!\.ToString)(?!\s*\+)', '$1$2.ToString()'

        # Pattern: TemplateId = entity.TemplateId
        $content = $content -replace '(\s+TemplateId:\s*)(\w+\.TemplateId)(?!\.ToString)', '$1$2.ToString()'

        # Pattern: VersionId = entity.VersionId
        $content = $content -replace '(\s+VersionId:\s*)(\w+\.VersionId)(?!\.ToString)', '$1$2.ToString()'

        # Pattern: ParentCommentId = entity.ParentCommentId (nullable)
        $content = $content -replace '(\s+ParentCommentId:\s*)(\w+\.ParentCommentId)(?!\.ToString)', '$1$2?.ToString()'

        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "  Fixed DTO mappings: $($file.Split('\')[-1])" -ForegroundColor Green
    }
}

# Pattern 5: Fix method parameter types (string to Guid in service methods)
Write-Host "`nFixing method parameter types..." -ForegroundColor Yellow
# This requires more careful analysis - we'll handle specific cases

# Fix PdfEndpoints.cs - parse documentId parameter
$file = "$apiPath\Routing\PdfEndpoints.cs"
if (Test-Path $file) {
    $content = Get-Content $file -Raw

    # Find the line with the error and add parsing
    $content = $content -replace '(await pdfIndexingService\.IndexPdfAsync\()([a-z]\w+Id)(,)', '$1Guid.Parse($2)$3'

    Set-Content -Path $file -Value $content -NoNewline
    Write-Host "  Fixed: PdfEndpoints.cs" -ForegroundColor Green
}

# Pattern 6: Fix List<string> to List<Guid> conversions
Write-Host "`nFixing List<string> to List<Guid> conversions..." -ForegroundColor Yellow
$listFiles = @(
    "$apiPath\Services\RuleCommentService.cs",
    "$apiPath\Services\RuleSpecCommentService.cs"
)

foreach ($file in $listFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Pattern: MentionedUserIds = request.MentionedUserIds where request has List<string>
        $content = $content -replace '(\s+MentionedUserIds\s*=\s*)(request\.MentionedUserIds)(?!\.)', '$1$2.Select(Guid.Parse).ToList()'

        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "  Fixed list conversions: $($file.Split('\')[-1])" -ForegroundColor Green
    }
}

# Pattern 7: Fix AuditService.LogAsync calls with Guid parameters
Write-Host "`nFixing AuditService.LogAsync calls..." -ForegroundColor Yellow
$auditFiles = @(
    "$apiPath\Services\RuleSpecCommentService.cs",
    "$apiPath\Services\PromptManagementService.cs"
)

foreach ($file in $auditFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw

        # Pattern: LogAsync with Guid parameters that need .ToString()
        $content = $content -replace '(_auditService\.LogAsync\([^,]+,\s*)(\w+\.Id)(\s*,)', '$1$2.ToString()$3'
        $content = $content -replace '(_auditService\.LogAsync\([^,]+,\s*[^,]+,\s*[^,]+,\s*)(\w+\.Id)(\s*,)', '$1$2.ToString()$3'
        $content = $content -replace '(_auditService\.LogAsync\([^,]+,\s*[^,]+,\s*[^,]+,\s*[^,]+,\s*)(\w+\?\.\w+)(\s*,)', '$1$2?.ToString()$3'
        $content = $content -replace '(_auditService\.LogAsync\([^,]+,\s*[^,]+,\s*[^,]+,\s*[^,]+,\s*)(\w+\.Id)(\s*\))', '$1$2.ToString()$3'

        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "  Fixed audit calls: $($file.Split('\')[-1])" -ForegroundColor Green
    }
}

# Pattern 8: Fix ConfigurationService UpdateConfigurationAsync call
$file = "$apiPath\Services\ConfigurationService.cs"
if (Test-Path $file) {
    $content = Get-Content $file -Raw

    # Fix the call to AuditService with userId parameter
    $content = $content -replace '(_auditService\.LogAsync\([^,]+,\s*)(userId)(\s*,)', '$1$2.ToString()$3'

    Set-Content -Path $file -Value $content -NoNewline
    Write-Host "  Fixed: ConfigurationService.cs" -ForegroundColor Green
}

Write-Host "`n=== Build check ===" -ForegroundColor Cyan
Set-Location "D:\Repositories\meepleai-monorepo\apps\api"
$buildOutput = dotnet build 2>&1
$errorCount = ($buildOutput | Select-String "error CS").Count

if ($errorCount -eq 0) {
    Write-Host "`nSUCCESS! All compilation errors fixed!" -ForegroundColor Green
} else {
    Write-Host "`nRemaining errors: $errorCount" -ForegroundColor Yellow
    Write-Host "Showing first 20 errors:" -ForegroundColor Yellow
    $buildOutput | Select-String "error CS" | Select-Object -First 20 | ForEach-Object {
        Write-Host $_ -ForegroundColor Red
    }
}
