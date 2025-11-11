# Final script to fix remaining compilation errors
$ErrorActionPreference = "Stop"

Write-Host "Fixing remaining Guid errors in Routing and Services..." -ForegroundColor Yellow

# Map of files and fixes
$fixes = @{
    "apps/api/src/Api/Routing/GameEndpoints.cs" = @(
        @('(await _gameService\.CreateGameAsync\()([^)]+), \$"user-', '$1$2.ToString(), $"user-')
        @('(await _gameService\.UpdateGameAsync\()([^,]+), ', '$1$2.ToString(), ')
        @('(await _pdfStorageService\.UploadPdfAsync\()([^,]+), ([^,]+),', '$1$2, $3.ToString(),')
    )
    "apps/api/src/Api/Routing/ChatEndpoints.cs" = @(
        @('(await _chatService\.CreateConversationAsync\([^,]+, )([^,]+), ([^,]+),', '$1$2.ToString(), $3.ToString(),')
        @('(await _chatService\.AddMessageAsync\([^,]+, [^,]+, )([^,]+), ([^,]+),', '$1$2.ToString(), $3.ToString(),')
        @('(await _chatService\.UpdateMessageAsync\([^,]+, [^,]+, )([^,]+)\)', '$1$2.ToString())')
    )
    "apps/api/src/Api/Routing/AuthEndpoints.cs" = @(
        @('(await _passwordResetService\.RequestPasswordResetAsync\()request\.Email', '$1request.Email')
        @('(await _sessionManagement\.GetUserSessionsAsync\()userId', '$1Guid.Parse(userId)')
    )
    "apps/api/src/Api/Routing/PdfEndpoints.cs" = @(
        @('(\.UserId == )userId', '$1Guid.Parse(userId)')
    )
    "apps/api/src/Api/Routing/AiEndpoints.cs" = @(
        @('(\.UserId == )userId', '$1Guid.Parse(userId)')
    )
    "apps/api/src/Api/Services/AgentFeedbackService.cs" = @(
        @('(\.UserId == )userId', '$1Guid.Parse(userId)')
        @('UserId = userId', 'UserId = Guid.Parse(userId)')
    )
    "apps/api/src/Api/Services/AdminStatsService.cs" = @(
        @('(\.CreatedByUserId == )userId', '$1Guid.Parse(userId)')
        @('(\.UserId == )userId', '$1Guid.Parse(userId)')
    )
    "apps/api/src/Api/Services/AuthService.cs" = @(
        @('(\.UserId == )userId', '$1Guid.Parse(userId)')
    )
    "apps/api/src/Api/Services/ChatService.cs" = @(
        @('ConversationId = conversationId', 'ConversationId = Guid.Parse(conversationId)')
    )
    "apps/api/src/Api/Services/CacheWarmingService.cs" = @(
        @('(PathSecurity\.SanitizeFilename\()pdfId', '$1pdfId.ToString()')
    )
    "apps/api/src/Api/Services/ConfigurationService.cs" = @(
        @('(c\.Id == )id\)', '$1Guid.Parse(id))')
    )
    "apps/api/src/Api/Services/SetupGuideService.cs" = @(
        @('(\.GameId == )gameId', '$1Guid.Parse(gameId)')
    )
    "apps/api/src/Api/Services/PromptEvaluationService.cs" = @(
        @('(\.TemplateId == )templateId', '$1Guid.Parse(templateId)')
    )
    "apps/api/src/Api/Services/PdfIndexingService.cs" = @(
        @('(\.Id == )pdfId', '$1Guid.Parse(pdfId)')
        @('PdfDocumentId = pdfId', 'PdfDocumentId = Guid.Parse(pdfId)')
        @('GameId = gameId', 'GameId = Guid.Parse(gameId)')
    )
    "apps/api/src/Api/Infrastructure/EntityConfigurations/RuleSpecCommentEntityConfiguration.cs" = @(
        @('\.Select\(id => id\)\.ToList\(\)', '.Select(id => Guid.Parse(id)).ToList()')
    )
}

foreach ($file in $fixes.Keys) {
    $fullPath = Join-Path $PSScriptRoot $file
    if (Test-Path $fullPath) {
        $content = Get-Content $fullPath -Raw

        foreach ($fix in $fixes[$file]) {
            $pattern = $fix[0]
            $replacement = $fix[1]
            $content = $content -replace $pattern, $replacement
        }

        Set-Content $fullPath -Value $content -NoNewline
        Write-Host "Fixed: $file" -ForegroundColor Green
    }
}

Write-Host "`nAll remaining errors fixed. Running final build..." -ForegroundColor Cyan
cd apps/api
$output = dotnet build --no-restore 2>&1
$errors = $output | Select-String "error CS"

if ($errors.Count -eq 0) {
    Write-Host "`n✅ SUCCESS: All compilation errors fixed!" -ForegroundColor Green
    Write-Host "Total lines built successfully" -ForegroundColor Green
} else {
    Write-Host "`n⚠️ Remaining errors: $($errors.Count)" -ForegroundColor Yellow
    $errors | Select-Object -First 20 | ForEach-Object { Write-Host $_ -ForegroundColor Red }
}
