# Script to add PromptTemplateService mock to test files
# This script updates test files to include IPromptTemplateService parameter

$files = @(
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\Ai04ComprehensiveTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\Ai04IntegrationTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\QaEndpointTests.cs"
)

$mockHelper = @'
    private static Mock<IPromptTemplateService> CreatePromptTemplateMock()
    {
        var mock = new Mock<IPromptTemplateService>();

        var defaultTemplate = new PromptTemplate
        {
            SystemPrompt = "You are a board game rules assistant.",
            UserPromptTemplate = "CONTEXT: {context}\n\nQUESTION: {query}\n\nANSWER:",
            FewShotExamples = new List<FewShotExample>()
        };

        mock.Setup(x => x.GetTemplateAsync(It.IsAny<Guid?>(), It.IsAny<QuestionType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultTemplate);

        mock.Setup(x => x.RenderSystemPrompt(It.IsAny<PromptTemplate>()))
            .Returns((PromptTemplate t) => t.SystemPrompt);

        mock.Setup(x => x.RenderUserPrompt(It.IsAny<PromptTemplate>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((PromptTemplate t, string context, string query) =>
                t.UserPromptTemplate.Replace("{context}", context).Replace("{query}", query));

        mock.Setup(x => x.ClassifyQuestion(It.IsAny<string>()))
            .Returns(QuestionType.General);

        return mock;
    }

'@

foreach ($file in $files) {
    if (-not (Test-Path $file)) {
        Write-Host "Skipping $file (not found)"
        continue
    }

    Write-Host "Processing $file..."
    $content = Get-Content $file -Raw

    # Add mock helper after the first private field declaration
    if ($content -notmatch "CreatePromptTemplateMock") {
        $content = $content -replace '(private readonly Mock<ILogger<[^>]+>> [^;]+;)', "`$1`n$mockHelper"
    }

    # Replace RagService constructor calls
    # Pattern: new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object)
    # Replace with: new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object)
    $content = $content -replace 'new RagService\(([^,]+),\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*(_mockLogger\.Object)\)', 'new RagService($1, $2, $3, $4, $5, CreatePromptTemplateMock().Object, $6)'

    # Replace StreamingQaService constructor calls (if any in QaEndpointTests)
    # Pattern: new StreamingQaService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, mockAudit.Object, _mockLogger.Object)
    # Replace with: new StreamingQaService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, mockAudit.Object, CreatePromptTemplateMock().Object, _mockLogger.Object)
    $content = $content -replace 'new StreamingQaService\(([^,]+),\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*([^,]+),\s*(_mockLogger\.Object)\)', 'new StreamingQaService($1, $2, $3, $4, $5, $6, CreatePromptTemplateMock().Object, $7)'

    Set-Content $file $content -NoNewline
    Write-Host "✅ Updated $file"
}

Write-Host "`n✅ All test files updated successfully!"
