# Fix remaining FluentAssertions syntax errors
# This script fixes common patterns across all test files

param(
    [string]$TestsPath = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"
)

Write-Host "Fixing FluentAssertions syntax errors in $TestsPath" -ForegroundColor Cyan

$files = @(
    "Services\ChatMessageEditDeleteServiceTests.cs",
    "Services\MdExportFormatterTests.cs",
    "Services\ResponseQualityServiceTests.cs",
    "Services\N8nTemplateServiceTests.cs",
    "Services\RuleCommentServiceTests.cs",
    "Services\PdfExportFormatterTests.cs",
    "Services\UserManagementServiceTests.cs",
    "Services\PdfTextExtractionServicePagedTests.cs"
)

foreach ($file in $files) {
    $fullPath = Join-Path $TestsPath $file
    if (Test-Path $fullPath) {
        Write-Host "Processing: $file" -ForegroundColor Yellow
        $content = Get-Content $fullPath -Raw

        # Pattern 1: var exception = var act = act;  →  Remove duplicate var
        $content = $content -replace 'var exception = var act = ([^;]+);([^}]+)act\.Should\(\)\.Throw<([^>]+)>\(\);([^}]+)exception\.ParamName\.Should\(\)\.Be\("([^"]+)"\);',
            '$1.Should().Throw<$3>().WithParameterName("$5");'

        # Pattern 2: var exception = var act = async () =>  →  Exception assertions
        $content = $content -replace 'var exception = var act = async \(\) => ([^;]+);([^}]+)await act\.Should\(\)\.ThrowAsync<([^>]+)>\(\);([^}]+)exception\.Message\.Should\(\)\.Contain\(([^\)]+)\);',
            'var act = async () => $1;$2await act.Should().ThrowAsync<$3>().WithMessage("*" + $5 + "*");'

        # Pattern 3: Split condition and message  value, precision.Should().
        $content = $content -replace '([a-zA-Z0-9_\.]+), (\d+)\.Should\(\)\.Be\(', '$1.Should().BeApproximately('

        # Pattern 4: GetSingleHeaderValue wrong order
        $content = $content -replace '"([^"]+)"\)\.Should\(\)\.Be\(([^,]+), GetSingleHeaderValue\(([^)]+)\);',
            'GetSingleHeaderValue($3, "$1").Should().Be($2);'

        Set-Content -Path $fullPath -Value $content -NoNewline
        Write-Host "  Fixed: $file" -ForegroundColor Green
    }
}

Write-Host "`nAll files processed. Building to check remaining errors..." -ForegroundColor Cyan
Set-Location "D:\Repositories\meepleai-monorepo\apps\api"
$buildOutput = dotnet build 2>&1 | Select-String "error CS"
$errorCount = ($buildOutput | Measure-Object).Count

Write-Host "`nRemaining errors: $errorCount" -ForegroundColor $(if ($errorCount -eq 0) { "Green" } else { "Yellow" })

if ($errorCount -gt 0 -and $errorCount -lt 50) {
    Write-Host "`nShowing remaining errors:" -ForegroundColor Yellow
    $buildOutput | Select-Object -First 20 | ForEach-Object { Write-Host $_ }
}
