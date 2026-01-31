# Issue #2031: Migrate remaining integration tests to SharedTestcontainersFixture
# This script automates the migration pattern for all remaining test files

$files = @(
    # ONLY non-migrated files (files already migrated manually are excluded to avoid duplications)
    # Already migrated: AdminDisable2FA, BulkApiKeyOperations, TotpReplayAttack, TwoFactorSecurity, OAuth,
    #                   CreateDocumentCollection, DeletePdf, DocumentCollectionRepo, IndexPdf

    # Batch 1 - DocumentProcessing (3 remaining files with Redis)
    @{Path="apps/api/tests/Api.Tests/Integration/UploadPdfIntegrationTests.cs"; DbName="test_uploadpdf"; Context="DocumentProcessing"; HasRedis=$true},
    @{Path="apps/api/tests/Api.Tests/Integration/UploadPdfMidPhaseCancellationTests.cs"; DbName="test_uploadcancel"; Context="DocumentProcessing"; HasRedis=$true},
    @{Path="apps/api/tests/Api.Tests/Integration/PdfUploadQuotaEnforcementIntegrationTests.cs"; DbName="test_quota"; Context="DocumentProcessing"; HasRedis=$true},

    # Batch 2 - GameManagement (6 files)
    @{Path="apps/api/tests/Api.Tests/Integration/GameManagement/CreateRuleCommentIntegrationTests.cs"; DbName="test_createcomment"; Context="GameManagement"},
    @{Path="apps/api/tests/Api.Tests/Integration/GameManagement/DeleteRuleCommentIntegrationTests.cs"; DbName="test_deletecomment"; Context="GameManagement"},
    @{Path="apps/api/tests/Api.Tests/Integration/GameManagement/ReplyToRuleCommentIntegrationTests.cs"; DbName="test_replycomment"; Context="GameManagement"},
    @{Path="apps/api/tests/Api.Tests/Integration/GameManagement/ResolveRuleCommentIntegrationTests.cs"; DbName="test_resolvecomment"; Context="GameManagement"},
    @{Path="apps/api/tests/Api.Tests/Integration/GameManagement/UnresolveRuleCommentIntegrationTests.cs"; DbName="test_unresolvecomment"; Context="GameManagement"},
    @{Path="apps/api/tests/Api.Tests/Integration/GameManagement/UpdateRuleCommentIntegrationTests.cs"; DbName="test_updatecomment"; Context="GameManagement"},

    # Batch 3 - Cross-Context (4 files)
    @{Path="apps/api/tests/Api.Tests/Integration/AuthenticationGameManagementCrossContextTests.cs"; DbName="test_authgame"; Context="CrossContext"},
    @{Path="apps/api/tests/Api.Tests/Integration/DocumentProcessingKnowledgeBaseCrossContextTests.cs"; DbName="test_docknowledge"; Context="CrossContext"},
    @{Path="apps/api/tests/Api.Tests/Integration/FullStackCrossContextWorkflowTests.cs"; DbName="test_fullstack"; Context="CrossContext"},
    @{Path="apps/api/tests/Api.Tests/Integration/KnowledgeBaseGameManagementCrossContextTests.cs"; DbName="test_knowledgegame"; Context="CrossContext"},

    # Batch 4 - KnowledgeBase + Administration (3 files)
    @{Path="apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/Month4QualityMetricsE2ETests.cs"; DbName="test_qualitymetrics"; Context="KnowledgeBase"},
    @{Path="apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Integration/RagValidationPipelineIntegrationTests.cs"; DbName="test_ragvalidation"; Context="KnowledgeBase"},
    @{Path="apps/api/tests/Api.Tests/Integration/Administration/BulkUserOperationsE2ETests.cs"; DbName="test_bulkuser"; Context="Administration"},

    # Batch 5 - Administration (1 file) - NEWLY DISCOVERED
    @{Path="apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Handlers/UpdateUserTierCommandHandlerTests.cs"; DbName="test_usertier"; Context="Administration"}
)

Write-Host "Issue #2031: Migrating $($files.Count) test files to SharedTestcontainersFixture" -ForegroundColor Cyan
Write-Host ""

$migratedCount = 0
$errorCount = 0

foreach ($file in $files) {
    $filePath = Join-Path $PSScriptRoot ".." $file.Path

    if (-not (Test-Path $filePath)) {
        Write-Host "  ⚠️  File not found: $($file.Path)" -ForegroundColor Yellow
        $errorCount++
        continue
    }

    Write-Host "Migrating: $($file.Path)" -ForegroundColor White

    try {
        $content = Get-Content $filePath -Raw

        # Skip if already migrated (has SharedTestcontainers collection attribute)
        if ($content -match '\[Collection\("SharedTestcontainers"\)\]') {
            Write-Host "  ℹ️  Already migrated, skipping..." -ForegroundColor Blue
            continue
        }

        # Step 1: Add imports
        if ($content -notmatch "using Api.Tests.Infrastructure;") {
            $content = $content -replace "(using Api\.BoundedContexts\.)", "using Api.Tests.Infrastructure;`r`n`$1"
        }

        # Remove Docker imports
        $content = $content -replace "using Docker\.DotNet;`r`n", ""
        $content = $content -replace "using DotNet\.Testcontainers\.Containers;`r`n", "using DotNet.Testcontainers.Containers;  // Only IContainer for type compatibility`r`n"

        # Step 2: Add collection attribute
        $content = $content -replace "(\[Trait\(`"Category`", TestCategories\.Integration\)\])", "[Collection(`"SharedTestcontainers`")]`r`n[Trait(`"Issue`", `"2031`")]`r`n`$1"

        # Step 3: Update class fields - remove container fields
        $content = $content -replace "private IContainer\? _postgresContainer;`r`n", ""
        if ($file.HasRedis) {
            $content = $content -replace "private IContainer\? _redisContainer;`r`n", ""
        }

        # Add fixture fields
        $dbNameField = "`r`n    private readonly SharedTestcontainersFixture _fixture;`r`n    private string _isolatedDbConnectionString = string.Empty;`r`n    private string _databaseName = string.Empty;"
        $content = $content -replace "(public sealed class \w+IntegrationTests : IAsyncLifetime\r\n\s*\{)", "`$1$dbNameField"

        # Step 4: Update constructor
        $className = [System.IO.Path]::GetFileNameWithoutExtension($file.Path)
        $content = $content -replace "(public $className\(\))", "public $className(SharedTestcontainersFixture fixture)"
        $content = $content -replace "(public $className\(\)\s*\{)", "public $className(SharedTestcontainersFixture fixture)`r`n    {`r`n        _fixture = fixture;"

        # Step 5: Replace container creation with isolated database
        $dbNameWithGuid = "$($file.DbName)_{Guid.NewGuid():N}"
        $isolatedDbCreation = @"
        // Issue #2031: Migrated to SharedTestcontainersFixture for Docker hijack prevention and performance
        _databaseName = `$"$dbNameWithGuid";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _output(`$"Isolated database created: {_databaseName}");
"@

        # Remove PostgreSQL container creation
        $content = $content -replace "(?s)_postgresContainer = new ContainerBuilder\(\).*?await _postgresContainer\.StartAsync\(.*?\);.*?var postgresPort = _postgresContainer\.GetMappedPublicPort\(5432\);.*?var connectionString = .*?;", $isolatedDbCreation

        # Remove Redis container creation if present
        if ($file.HasRedis) {
            $content = $content -replace "(?s)_redisContainer = new ContainerBuilder\(\).*?await _redisContainer\.StartAsync\(.*?\);", ""
        }

        # Step 6: Replace DisposeAsync container disposal
        $dbCleanup = @"
        // Issue #2031: Use SharedTestcontainersFixture for cleanup instead of individual container disposal
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
                _output(`$"Isolated database dropped: {_databaseName}");
            }
            catch (Exception ex)
            {
                _output(`$"Warning: Failed to drop database {_databaseName}: {ex.Message}");
            }
        }
"@

        # Remove PostgreSQL container disposal
        $content = $content -replace "(?s)if \(_postgresContainer != null\).*?\{.*?await _postgresContainer\.StopAsync.*?;.*?await _postgresContainer\.DisposeAsync.*?;.*?\}", $dbCleanup

        # Remove Redis container disposal if present
        if ($file.HasRedis) {
            $content = $content -replace "(?s)if \(_redisContainer != null\).*?\{.*?await _redisContainer\.StopAsync.*?;.*?await _redisContainer\.DisposeAsync.*?;.*?\}", ""
        }

        # Step 7: Update XML doc comments
        $content = $content -replace "(\<summary\>.*?)", "`$1`r`n/// Uses SharedTestcontainersFixture for optimized performance and Docker hijack prevention (Issue #2031)."

        # Save migrated file
        Set-Content -Path $filePath -Value $content -NoNewline

        Write-Host "  ✓ Migrated successfully" -ForegroundColor Green
        $migratedCount++
    }
    catch {
        Write-Host "  ❌ Migration failed: $_" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host ""
Write-Host "Migration Summary:" -ForegroundColor Cyan
Write-Host "  ✓ Successfully migrated: $migratedCount files" -ForegroundColor Green
Write-Host "  ❌ Errors: $errorCount files" -ForegroundColor Red
Write-Host ""

if ($errorCount -eq 0) {
    Write-Host "All files migrated successfully! Run 'dotnet build' to verify." -ForegroundColor Green
} else {
    Write-Host "Some files failed to migrate. Please review manually." -ForegroundColor Yellow
}
