# Final comprehensive fix for ALL FluentAssertions patterns
$testDir = "D:\Repositories\meepleai-monorepo\apps\api\tests\Api.Tests"
$files = Get-ChildItem -Path $testDir -Filter "*.cs" -Recurse
$totalFixed = 0

foreach ($file in $files) {
    $lines = Get-Content -Path $file.FullName
    if (!$lines) { continue }

    $modified = $false
    $newLines = New-Object System.Collections.ArrayList

    for ($i = 0; $i < $lines.Count; $i++) {
        $line = $lines[$i]
        $original = $line

        # Pattern 1: value, "reason".Should().BeTrue()
        if ($line -match '^\s+(\w+),\s*("[^"]+")\.Should\(\)\.BeTrue\(\);') {
            $line = $line -replace '(\w+),\s*("[^"]+")\.Should\(\)\.BeTrue\(\)', '($1).Should().BeTrue($2)'
            $modified = $true
        }

        # Pattern 2: value, "reason".Should().BeFalse()
        if ($line -match '^\s+(\w+),\s*("[^"]+")\.Should\(\)\.BeFalse\(\);') {
            $line = $line -replace '(\w+),\s*("[^"]+")\.Should\(\)\.BeFalse\(\)', '($1).Should().BeFalse($2)'
            $modified = $true
        }

        # Pattern 3: Multi-line boolean with .Should().BeTrue()
        if ($line -match '^\s+\(') {
            # Check if next few lines contain .Should().BeTrue/False
            $j = $i + 1
            while ($j < $lines.Count -and $lines[$j] -match '^\s*["\w()]+\s*$') {
                if ($lines[$j] -match '^\s*("[^"]+")\.Should\(\)\.BeTrue\(\);') {
                    $reason = $matches[1]
                    $lines[$j] = $lines[$j] -replace '("[^"]+")\.Should\(\)\.BeTrue\(\)', ".Should().BeTrue($reason)"
                    $modified = $true
                    break
                }
                if ($lines[$j] -match '^\s*("[^"]+")\.Should\(\)\.BeFalse\(\);') {
                    $reason = $matches[1]
                    $lines[$j] = $lines[$j] -replace '("[^"]+")\.Should\(\)\.BeFalse\(\)', ".Should().BeFalse($reason)"
                    $modified = $true
                    break
                }
                $j++
            }
        }

        $null = $newLines.Add($line)
    }

    if ($modified) {
        Set-Content -Path $file.FullName -Value $newLines
        $totalFixed++
        Write-Host "Fixed: $($file.Name)"
    }
}

Write-Host "`nTotal files fixed: $totalFixed"
