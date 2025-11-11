# Fix mock signatures for AI-09 multilingual support

$files = @(
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\Ai04ComprehensiveTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\Ai04IntegrationTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\QaEndpointTests.cs"
)

foreach ($file in $files) {
    if (-not (Test-Path $file)) {
        Write-Host "Skipping $file (not found)"
        continue
    }

    Write-Host "Processing $file..."
    $content = Get-Content $file -Raw

    # Fix EmbeddingService mock: Add language parameter (string) between text and CancellationToken
    $content = $content -replace 'GenerateEmbeddingAsync\(It\.IsAny<string>\(\), It\.IsAny<CancellationToken>\(\)\)', 'GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())'

    # Fix QdrantService mock: Add language parameter (string) between embedding and limit
    $content = $content -replace 'SearchAsync\(It\.IsAny<string>\(\), It\.IsAny<float\[\]>\(\), It\.IsAny<int>\(\), It\.IsAny<CancellationToken>\(\)\)', 'SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())'

    Set-Content $file $content -NoNewline
    Write-Host "✅ Fixed $file"
}

Write-Host "`n✅ All language mock signatures fixed!"
