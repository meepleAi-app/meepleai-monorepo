# Fix remaining GUID errors after first pass
$ErrorActionPreference = "Stop"

Write-Host "Fixing remaining GUID errors..." -ForegroundColor Cyan

$apiPath = "D:\Repositories\meepleai-monorepo\apps\api\src\Api"

# Fix 1: PasswordResetService - Id should be Guid, not string
Write-Host "`nFixing PasswordResetService..." -ForegroundColor Yellow
$file = "$apiPath\Services\PasswordResetService.cs"
$content = Get-Content $file -Raw

# Line 96: Id = Guid.NewGuid().ToString("N") should be Id = Guid.NewGuid()
$content = $content -replace 'Id = Guid\.NewGuid\(\)\.ToString\("N"\)', 'Id = Guid.NewGuid()'

# Line 259: UserId = token.UserId (Guid to string) should be UserId = token.UserId.ToString()
$content = $content -replace 'new ResetPasswordResponse\(\s*UserId:\s*token\.UserId', 'new ResetPasswordResponse(UserId: token.UserId.ToString()'

Set-Content -Path $file -Value $content -NoNewline
Write-Host "  Fixed: PasswordResetService.cs" -ForegroundColor Green

# Fix 2: PdfIndexingService - More comparison fixes
Write-Host "`nFixing PdfIndexingService..." -ForegroundColor Yellow
$file = "$apiPath\Services\PdfIndexingService.cs"
$content = Get-Content $file -Raw

# Line 77: pdf.UserId == currentUserId
$content = $content -replace '(pdf\.UserId)\s*==\s*(currentUserId)', '$1.ToString() == $2'

# Line 103 & 105: PdfDocumentId and UserId assignments from string
$content = $content -replace '(\s+PdfDocumentId\s*=\s*)([a-z]\w+Id)(?!\.)(\s*,)', '$1Guid.Parse($2)$3'

# Line 175: _qdrantService.SearchAsync takes documentId.ToString()
$content = $content -replace '(_qdrantService\.SearchAsync\()(\w+Id)(,)', '$1$2.ToString()$3'

Set-Content -Path $file -Value $content -NoNewline
Write-Host "  Fixed: PdfIndexingService.cs" -ForegroundColor Green

# Fix 3: PdfStorageService - More DTO mappings and method calls
Write-Host "`nFixing PdfStorageService..." -ForegroundColor Yellow
$file = "$apiPath\Services\PdfStorageService.cs"
$content = Get-Content $file -Raw

# Line 133, 172, 176, 217, 221: GameId assignments and DTO mappings
$content = $content -replace '(\s+GameId\s*=\s*)([a-z]\w+Id)(?!\.Parse)(\s*,)', '$1Guid.Parse($2)$3'

# Line 245: pdf.GameId == gameId
$content = $content -replace '(pdf\.GameId)\s*==\s*(gameId)', '$1.ToString() == $2'

# Line 300: _ragService.SearchAsync takes gameId.ToString()
$content = $content -replace '(_ragService\.SearchAsync\([^,]+,\s*)(\w+Id)(,)', '$1$2.ToString()$3'

# Line 311 & 313: _llmService calls with Guid parameters
$content = $content -replace '(_llmService\.\w+\()(\w+Id)(,)', '$1$2.ToString()$3'
$content = $content -replace '(_llmService\.\w+\([^,]+,\s*[^,]+,\s*)(\w+Id)(,)', '$1$2.ToString()$3'
$content = $content -replace '(_llmService\.\w+\([^,]+,\s*[^,]+,\s*[^,]+,\s*)(\w+Id)(\))', '$1$2?.ToString()$3'

Set-Content -Path $file -Value $content -NoNewline
Write-Host "  Fixed: PdfStorageService.cs" -ForegroundColor Green

# Fix 4: PdfEndpoints - Parameter parsing and comparisons
Write-Host "`nFixing PdfEndpoints..." -ForegroundColor Yellow
$file = "$apiPath\Routing\PdfEndpoints.cs"
$content = Get-Content $file -Raw

# Line 84: Already fixed by first script, but make sure it's correct
$content = $content -replace '(await pdfIndexingService\.IndexPdfAsync\([^,]+,\s*)([a-z]\w+Id)(\))', '$1Guid.Parse($2)$3'

# Lines 275, 347, 399: Owner checks - userId.ToString() comparisons
$content = $content -replace '(\s+if\s*\()(userId)\s*==\s*(document\.UserId)', '$1$2 == $3.ToString()'
$content = $content -replace '(\s+if\s*\()(userId)\s*!=\s*(document\.UserId)', '$1$2 != $3.ToString()'
$content = $content -replace '(\s+if\s*\()(currentUserId)\s*==\s*(doc\.UserId)', '$1$2 == $3.ToString()'
$content = $content -replace '(\s+if\s*\()(currentUserId)\s*!=\s*(doc\.UserId)', '$1$2 != $3.ToString()'

Set-Content -Path $file -Value $content -NoNewline
Write-Host "  Fixed: PdfEndpoints.cs" -ForegroundColor Green

# Fix 5: AiEndpoints - Comparison fix
Write-Host "`nFixing AiEndpoints..." -ForegroundColor Yellow
$file = "$apiPath\Routing\AiEndpoints.cs"
$content = Get-Content $file -Raw

# Line 92: userId == game.UserId
$content = $content -replace '(if\s*\()([a-z]\w+Id)\s*==\s*(game\.UserId)(\))', '$1$2 == $3.ToString()$4'

Set-Content -Path $file -Value $content -NoNewline
Write-Host "  Fixed: AiEndpoints.cs" -ForegroundColor Green

# Fix 6: All services - Fix remaining DTO to string conversions
Write-Host "`nFixing DTO property mappings..." -ForegroundColor Yellow
$dtoFiles = @(
    "$apiPath\Services\PdfStorageService.cs",
    "$apiPath\Services\RuleSpecService.cs",
    "$apiPath\Services\RuleSpecCommentService.cs",
    "$apiPath\Services\RuleCommentService.cs",
    "$apiPath\Services\ChatService.cs",
    "$apiPath\Services\PromptManagementService.cs",
    "$apiPath\Services\PromptTemplateService.cs",
    "$apiPath\Services\PromptEvaluationService.cs",
    "$apiPath\Services\N8nConfigService.cs",
    "$apiPath\Services\OAuthService.cs",
    "$apiPath\Services\ConfigurationService.cs"
)

foreach ($f in $dtoFiles) {
    if (Test-Path $f) {
        $content = Get-Content $f -Raw

        # Generic DTO property mappings - convert Guid fields to string
        $guidProperties = @('Id', 'UserId', 'GameId', 'RuleSpecId', 'TemplateId', 'VersionId',
                            'PdfDocumentId', 'ConfigurationId', 'PromptTemplateId', 'PromptVersionId',
                            'ParentCommentId', 'CreatedByUserId', 'UpdatedByUserId')

        foreach ($prop in $guidProperties) {
            # Pattern: Property: entity.Property where entity has Guid
            $content = $content -replace "(\s+$prop`:\s*)(\w+\.$prop)(?!\.ToString)(?!\?\.ToString)(\s*,)", '$1$2.ToString()$3'

            # Pattern: Property: entity.Property? (nullable)
            if ($prop -match 'Parent|Created|Updated') {
                $content = $content -replace "(\s+$prop`:\s*)(\w+\.$prop)(?!\.ToString)(\s*,)", '$1$2?.ToString()$3'
            }
        }

        Set-Content -Path $f -Value $content -NoNewline
        Write-Host "  Fixed DTO mappings: $($f.Split('\')[-1])" -ForegroundColor Green
    }
}

# Fix 7: RuleSpecCommentService and RuleCommentService - AuditService calls
Write-Host "`nFixing RuleSpec and RuleComment services..." -ForegroundColor Yellow
$commentFiles = @(
    "$apiPath\Services\RuleSpecCommentService.cs",
    "$apiPath\Services\RuleCommentService.cs"
)

foreach ($f in $commentFiles) {
    if (Test-Path $f) {
        $content = Get-Content $f -Raw

        # Fix AuditService.LogAsync calls - all Guid parameters need .ToString()
        # Pattern 1: LogAsync(action, entityId, ...)
        $content = $content -replace '(_auditService\.LogAsync\(\s*"[^"]+"\s*,\s*)(\w+\.Id)(\s*,)', '$1$2.ToString()$3'

        # Pattern 2: LogAsync(action, id, details, relatedEntityId, ...)
        $content = $content -replace '(_auditService\.LogAsync\(\s*"[^"]+"\s*,\s*[^,]+,\s*[^,]+,\s*)(\w+\.Id)(\s*,)', '$1$2.ToString()$3'
        $content = $content -replace '(_auditService\.LogAsync\(\s*"[^"]+"\s*,\s*[^,]+,\s*[^,]+,\s*)(\w+\?)(\s*,)', '$1$2?.ToString()$3'

        # Pattern 3: LogAsync(action, id, details, relatedEntityId, userId)
        $content = $content -replace '(_auditService\.LogAsync\(\s*"[^"]+"\s*,\s*[^,]+,\s*[^,]+,\s*[^,]+,\s*)(\w+\.Id)(\s*\))', '$1$2.ToString()$3'

        Set-Content -Path $f -Value $content -NoNewline
        Write-Host "  Fixed: $($f.Split('\')[-1])" -ForegroundColor Green
    }
}

# Fix 8: Admin endpoints - DTO conversions
Write-Host "`nFixing Admin endpoints..." -ForegroundColor Yellow
$adminFiles = @(
    "$apiPath\Routing\AdminEndpoints.cs",
    "$apiPath\Routing\ChatEndpoints.cs",
    "$apiPath\Routing\GameEndpoints.cs",
    "$apiPath\Routing\AuthEndpoints.cs"
)

foreach ($f in $adminFiles) {
    if (Test-Path $f) {
        $content = Get-Content $f -Raw

        # Fix any remaining entity.Id to string conversions in DTOs
        $guidProps = @('Id', 'UserId', 'GameId', 'ChatId', 'SessionId')
        foreach ($prop in $guidProps) {
            $content = $content -replace "(\s+$prop`:\s*)(\w+\.$prop)(?!\.ToString)(\s*,)", '$1$2.ToString()$3'
        }

        Set-Content -Path $f -Value $content -NoNewline
        Write-Host "  Fixed: $($f.Split('\')[-1])" -ForegroundColor Green
    }
}

Write-Host "`n=== Build check ===" -ForegroundColor Cyan
Set-Location "D:\Repositories\meepleai-monorepo\apps\api"
$buildOutput = dotnet build 2>&1
$errorCount = ($buildOutput | Select-String "error CS").Count

if ($errorCount -eq 0) {
    Write-Host "`nSUCCESS! All compilation errors fixed!" -ForegroundColor Green
} else {
    Write-Host "`nRemaining errors: $errorCount" -ForegroundColor Yellow
    if ($errorCount -lt 50) {
        Write-Host "Showing all errors:" -ForegroundColor Yellow
        $buildOutput | Select-String "error CS" | ForEach-Object {
            Write-Host $_ -ForegroundColor Red
        }
    } else {
        Write-Host "Showing first 30 errors:" -ForegroundColor Yellow
        $buildOutput | Select-String "error CS" | Select-Object -First 30 | ForEach-Object {
            Write-Host $_ -ForegroundColor Red
        }
    }
}
