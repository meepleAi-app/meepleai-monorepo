# Fix common Guid to string conversion errors in test files
$files = @(
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\TotpServiceTests.cs",
    "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests\PasswordResetServiceTests.cs"
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "Fixing $file..."
        $content = Get-Content $file -Raw

        # Fix string userId declarations to Guid
        $content = $content -replace 'var userId = "test-user-(\d+)";', 'var userId = Guid.NewGuid();'
        $content = $content -replace 'var userId = "[\w-]+";', 'var userId = Guid.NewGuid();'

        # Fix string declarations for other common IDs
        $content = $content -replace 'var gameId = "[\w-]+";', 'var gameId = Guid.NewGuid();'
        $content = $content -replace 'var chatId = "[\w-]+";', 'var chatId = Guid.NewGuid();'
        $content = $content -replace 'var specId = "[\w-]+";', 'var specId = Guid.NewGuid();'

        Set-Content $file $content -NoNewline
        Write-Host "Fixed $file"
    }
}

Write-Host "Done!"
