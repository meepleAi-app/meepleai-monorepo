# Fix test method calls: Convert string parameters to Guid
# DDD-PHASE2: Tests call service methods that now expect Guid parameters

$files = Get-ChildItem -Path "apps/api/tests/Api.Tests" -Include "*.cs" -Recurse |
    Where-Object { $_.FullName -notmatch "\\bin\\|\\obj\\|BoundedContexts" }

$totalFixed = 0

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $original = $content

    # Pattern 1: Method calls with "string-id" as parameter → Guid.NewGuid()
    # Common in test assertions and service calls
    $content = $content -replace '([a-zA-Z]+Async\([^)]*)"([0-9a-fA-F-]+)"', '$1Guid.NewGuid()'

    # Pattern 2: InlineData with string IDs → remove quotes to trigger Guid parsing
    # [InlineData("guid-string")] → [InlineData("admin")] only for roles
    # Keep actual Guid strings as is for now - will need Guid.Parse

    # Pattern 3: Variable assignments in tests
    $content = $content -replace 'var (userId|gameId|ruleSpecId|chatId|templateId|versionId|agentId|apiKeyId) = "([0-9a-fA-F-]+)"', 'var $1 = Guid.NewGuid()'

    # Pattern 4: Const string IDs in tests → Convert to static Guid
    $content = $content -replace 'const string (Test.*Id) = "([0-9a-fA-F-]+)"', 'static readonly Guid $1 = Guid.NewGuid()'
    $content = $content -replace 'private const string (.*Id) = "([0-9a-fA-F-]+)"', 'private static readonly Guid $1 = Guid.NewGuid()'

    if ($content -ne $original) {
        Set-Content $file.FullName -Value $content -NoNewline
        $totalFixed++
        Write-Host "Fixed: $($file.Name)" -ForegroundColor Green
    }
}

Write-Host "`nTotal files fixed: $totalFixed" -ForegroundColor Cyan
