# PowerShell script to fix all FluentAssertions errors in test files

$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"

# Get all .cs files recursively
$files = Get-ChildItem -Path $testDir -Filter "*.cs" -Recurse

Write-Host "Found $($files.Count) test files to process"

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Fix Assert.IsType patterns
    $content = $content -replace 'Assert\.IsType<([^>]+)>\(([^)]+)\)', '$2.Should().BeOfType<$1>()'

    # Fix Assert.Equal patterns
    $content = $content -replace 'Assert\.Equal\(([^,]+),\s*([^)]+)\)', '$2.Should().Be($1)'

    # Fix Assert.NotEqual patterns
    $content = $content -replace 'Assert\.NotEqual\(([^,]+),\s*([^)]+)\)', '$2.Should().NotBe($1)'

    # Fix Assert.Null patterns
    $content = $content -replace 'Assert\.Null\(([^)]+)\)', '$1.Should().BeNull()'

    # Fix Assert.NotNull patterns
    $content = $content -replace 'Assert\.NotNull\(([^)]+)\)', '$1.Should().NotBeNull()'

    # Fix Assert.True patterns
    $content = $content -replace 'Assert\.True\(([^)]+)\)', '$1.Should().BeTrue()'

    # Fix Assert.False patterns
    $content = $content -replace 'Assert\.False\(([^)]+)\)', '$1.Should().BeFalse()'

    # Fix Assert.Empty patterns
    $content = $content -replace 'Assert\.Empty\(([^)]+)\)', '$1.Should().BeEmpty()'

    # Fix Assert.NotEmpty patterns
    $content = $content -replace 'Assert\.NotEmpty\(([^)]+)\)', '$1.Should().NotBeEmpty()'

    # Fix Assert.Contains patterns
    $content = $content -replace 'Assert\.Contains\(([^,]+),\s*([^)]+)\)', '$2.Should().Contain($1)'

    # Fix Assert.DoesNotContain patterns
    $content = $content -replace 'Assert\.DoesNotContain\(([^,]+),\s*([^)]+)\)', '$2.Should().NotContain($1)'

    # Fix Assert.StartsWith patterns
    $content = $content -replace 'Assert\.StartsWith\(([^,]+),\s*([^)]+)\)', '$2.Should().StartWith($1)'

    # Fix Assert.EndsWith patterns
    $content = $content -replace 'Assert\.EndsWith\(([^,]+),\s*([^)]+)\)', '$2.Should().EndWith($1)'

    # Fix Assert.Single patterns
    $content = $content -replace 'Assert\.Single\(([^)]+)\)', '$1.Should().ContainSingle()'

    # Fix Assert.InRange patterns
    $content = $content -replace 'Assert\.InRange\(([^,]+),\s*([^,]+),\s*([^)]+)\)', '$1.Should().BeInRange($2, $3)'

    # Fix Assert.All patterns
    $content = $content -replace 'Assert\.All\(([^,]+),\s*([^)]+)\)', '$1.Should().AllSatisfy($2)'

    # Fix Assert.Collection patterns
    $content = $content -replace 'Assert\.Collection\(([^,]+),([^)]+)\)', '$1.Should().SatisfyRespectively($2)'

    # Fix Assert.Throws patterns
    $content = $content -replace 'Assert\.Throws<([^>]+)>\(([^)]+)\)', 'FluentActions.Invoking($2).Should().Throw<$1>()'

    # Fix await Assert.ThrowsAsync patterns
    $content = $content -replace 'await Assert\.ThrowsAsync<([^>]+)>\(([^)]+)\)', 'await FluentActions.Awaiting($2).Should().ThrowAsync<$1>()'

    # Fix var exception = await Assert.ThrowsAsync patterns
    $content = $content -replace 'var (\w+) = await Assert\.ThrowsAsync<([^>]+)>\(([^)]+)\)', 'var $1 = await FluentActions.Awaiting($3).Should().ThrowAsync<$2>()'

    # Ensure FluentAssertions using is present
    if ($content -notmatch 'using FluentAssertions;') {
        $content = $content -replace '(using Xunit;)', "`$1`nusing FluentAssertions;"
    }

    # Only write if content changed
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.Name)"
    }
}

Write-Host "Processing complete!"