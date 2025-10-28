# Fix mock setup signature - remove CancellationToken parameter

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

    # Fix the mock setup - remove CancellationToken parameter
    $content = $content -replace 'mock\.Setup\(x => x\.GetTemplateAsync\(It\.IsAny<Guid\?\(\)>, It\.IsAny<QuestionType>\(\), It\.IsAny<CancellationToken>\(\)\)\)', 'mock.Setup(x => x.GetTemplateAsync(It.IsAny<Guid?>(), It.IsAny<QuestionType>()))'

    Set-Content $file $content -NoNewline
    Write-Host "✅ Fixed $file"
}

Write-Host "`n✅ All mock signatures fixed!"
