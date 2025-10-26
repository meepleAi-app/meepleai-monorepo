# Fix RagService test instantiations to include HybridSearchService parameter
# AI-14: After hybrid search integration, RagService constructor requires IHybridSearchService

$testFiles = Get-ChildItem -Path "apps/api/tests" -Filter "*.cs" -Recurse

foreach ($file in $testFiles) {
    $content = Get-Content -Path $file.FullName -Raw

    # Pattern 1: new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object)
    $oldPattern1 = 'new RagService\(dbContext, mockEmbedding\.Object, mockQdrant\.Object, mockLlm\.Object, mockCache\.Object, CreatePromptTemplateMock\(\)\.Object, _mockLogger\.Object\)'
    $newPattern1 = 'new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object)'

    if ($content -match $oldPattern1) {
        Write-Host "Fixing $($file.FullName)" -ForegroundColor Yellow
        $content = $content -replace $oldPattern1, $newPattern1
        Set-Content -Path $file.FullName -Value $content -NoNewline
    }
}

Write-Host "Fix complete!" -ForegroundColor Green
