# Comprehensive FluentAssertions syntax fix script
# Fixes all known patterns from the migration

$ErrorActionPreference = "Stop"
$files = Get-ChildItem -Path . -Filter "*.cs" -Recurse

$patterns = @(
    # Pattern 1: X, precision: Y.Should().Be(Z) -> X.Should().Be(Z, precision: Y)
    @{
        Pattern = '(\w+(?:\.\w+)*(?:!\.\w+)?), precision: (\d+)\.Should\(\)\.Be\(([^)]+)\);'
        Replacement = '$1.Should().Be($3, precision: $2);'
    },
    # Pattern 2: X, StringComparison.Y.Should().Contain(Z) -> X.Should().Contain(Z, StringComparison.Y)
    @{
        Pattern = '(\w+(?:\.\w+)*), (StringComparison\.\w+)\.Should\(\)\.Contain\(([^)]*)\);'
        Replacement = '$1.Should().Contain($3, $2);'
    },
    @{
        Pattern = '(\w+(?:\.\w+)*), (StringComparison\.\w+)\.Should\(\)\.StartWith\(([^)]*)\);'
        Replacement = '$1.Should().StartWith($3, $2);'
    },
    # Pattern 3: X.SuccessRate, 3.Should().Be -> X.SuccessRate.Should().Be
    @{
        Pattern = '(\w+\.SuccessRate), 3\.Should\(\)\.Be\('
        Replacement = '$1.Should().Be('
    },
    # Pattern 4: sessionCookie, StringComparison -> sessionCookie.Should()
    @{
        Pattern = 'sessionCookie, (StringComparison\.\w+)\.Should\(\)\.Contain\("([^"]+)"\);'
        Replacement = 'sessionCookie.Should().Contain("$2", $1);'
    }
)

$totalFixed = 0
foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    foreach ($p in $patterns) {
        $content = $content -replace $p.Pattern, $p.Replacement
    }
    
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        $totalFixed++
        Write-Host "Fixed: $($file.Name)"
    }
}

Write-Host "`nTotal files fixed: $totalFixed"
