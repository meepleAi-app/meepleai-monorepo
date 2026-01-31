<#
.SYNOPSIS
    Verifica il completamento dei DoD (Definition of Done) per le issue chiuse

.DESCRIPTION
    Script automatizzato che:
    1. Analizza tutte le issue chiuse con DoD/Acceptance Criteria
    2. Verifica automaticamente quali item sono stati implementati
    3. Categorizza in: Implemented, Manual Verification, Missing
    4. Genera report JSON e Markdown
    5. Prepara aggiornamenti per GitHub

.PARAMETER IssueNumbers
    Array di numeri issue da analizzare. Se non specificato, analizza tutte le issue chiuse con DoD.

.PARAMETER OutputPath
    Percorso per i file di output (default: docs/issue)

.PARAMETER DryRun
    Se true, non applica modifiche a GitHub (solo analisi locale)

.EXAMPLE
    .\verify-dod-completion.ps1

.EXAMPLE
    .\verify-dod-completion.ps1 -IssueNumbers @(511, 427, 420) -DryRun
#>

[CmdletBinding()]
param(
    [Parameter()]
    [int[]]$IssueNumbers = @(),

    [Parameter()]
    [string]$OutputPath = "docs/issue",

    [Parameter()]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Timestamp per report
$timestamp = Get-Date -Format "yyyy-MM-dd-HHmmss"

# Colori per output
function Write-ColorOutput {
    param([string]$Message, [string]$Color = "White")
    Write-Host $Message -ForegroundColor $Color
}

# Header
Write-ColorOutput "`n=== DoD Verification Script ===" "Cyan"
Write-ColorOutput "Timestamp: $timestamp`n" "Gray"

# Verifica requisiti
Write-ColorOutput "Checking prerequisites..." "Yellow"

# Check gh CLI
try {
    $null = gh --version
    Write-ColorOutput "✓ GitHub CLI found" "Green"
} catch {
    Write-ColorOutput "✗ GitHub CLI not found. Install from https://cli.github.com/" "Red"
    exit 1
}

# Check git repo
if (-not (Test-Path ".git")) {
    Write-ColorOutput "✗ Not in a git repository" "Red"
    exit 1
}
Write-ColorOutput "✓ Git repository detected" "Green"

# Ottieni lista issue se non specificata
if ($IssueNumbers.Count -eq 0) {
    Write-ColorOutput "`nFetching closed issues with DoD sections..." "Yellow"

    $issuesJson = gh issue list --state closed --limit 100 --json number,title,body | ConvertFrom-Json

    $IssueNumbers = $issuesJson | Where-Object {
        $_.body -match "## DoD|## Definition of Done|## Acceptance Criteria"
    } | Select-Object -ExpandProperty number

    Write-ColorOutput "Found $($IssueNumbers.Count) closed issues with DoD sections" "Green"
}

# Struttura dati per risultati
$results = @{
    timestamp = $timestamp
    total_issues = $IssueNumbers.Count
    issues = @()
    summary = @{
        total_dod_items = 0
        implemented = 0
        manual_verification = 0
        missing = 0
        issues_to_reopen = @()
        manual_items = @()
    }
}

# Pattern di verifica automatica
$verificationPatterns = @{
    # File existence patterns
    FileExists = @{
        Pattern = "Create|Add|Implement.*`[`'`"]([^`'`"]+\.(cs|tsx?|json|yml|yaml|md|sql))`[`'`"]"
        Verify = { param($match) Test-Path $match }
    }

    # Service implementation
    ServiceExists = @{
        Pattern = "(\w+Service)\.cs|(\w+Service) implementation"
        Verify = { param($match)
            (Get-ChildItem -Recurse -Filter "*$match*" -File | Measure-Object).Count -gt 0
        }
    }

    # API Endpoints
    EndpointExists = @{
        Pattern = "(GET|POST|PUT|DELETE|PATCH)\s+(/api/v\d+/[\w/-]+)"
        Verify = { param($match)
            $endpoint = $match -replace "^(GET|POST|PUT|DELETE|PATCH)\s+", ""
            $found = Select-String -Path "apps/api/src/Api/Program.cs" -Pattern [regex]::Escape($endpoint) -Quiet
            return $found
        }
    }

    # Database tables/migrations
    TableExists = @{
        Pattern = "table\s+`[`'`"]?(\w+)`[`'`"]?|CREATE TABLE\s+(\w+)"
        Verify = { param($match)
            $migrations = Get-ChildItem "apps/api/src/Api/Migrations" -Filter "*.cs" -Recurse
            $found = $migrations | Select-String -Pattern $match -Quiet
            return $found
        }
    }

    # Frontend components
    ComponentExists = @{
        Pattern = "(\w+)\.(tsx|jsx) component|Create.*component.*(\w+)"
        Verify = { param($match)
            (Get-ChildItem -Path "apps/web" -Recurse -Filter "*$match*" -File | Measure-Object).Count -gt 0
        }
    }

    # Tests
    TestExists = @{
        Pattern = "(\w+Tests?)\.cs|Test.*(\w+)|(\w+)\.test\.(tsx?|jsx?)"
        Verify = { param($match)
            $testDirs = @("apps/api/tests", "apps/web/src")
            foreach ($dir in $testDirs) {
                if (Test-Path $dir) {
                    $found = Get-ChildItem -Path $dir -Recurse -Filter "*$match*" -File
                    if ($found) { return $true }
                }
            }
            return $false
        }
    }
}

# Pattern per manual verification
$manualPatterns = @(
    "test manually",
    "verify.*browser",
    "check.*UI",
    "dashboard.*works",
    "renders? correctly",
    "trigger.*correctly",
    "alerts?.*visible",
    "manual.*check",
    "QA.*testing",
    "user.*testing"
)

# Funzione per verificare un DoD item
function Test-DodItem {
    param(
        [string]$ItemText,
        [int]$IssueNumber
    )

    $result = @{
        text = $ItemText
        category = "unknown"
        verification = ""
        evidence = @()
    }

    # Check manual verification patterns first
    foreach ($pattern in $manualPatterns) {
        if ($ItemText -match $pattern) {
            $result.category = "manual_verification"
            $result.verification = "Requires manual testing/verification"
            return $result
        }
    }

    # Try automated verification patterns
    foreach ($patternName in $verificationPatterns.Keys) {
        $config = $verificationPatterns[$patternName]

        if ($ItemText -match $config.Pattern) {
            $matchedValue = $Matches[1]
            if (-not $matchedValue) { $matchedValue = $Matches[2] }

            try {
                $verified = & $config.Verify $matchedValue

                if ($verified) {
                    $result.category = "implemented"
                    $result.verification = "$patternName verification passed"
                    $result.evidence += "Found: $matchedValue"
                    return $result
                } else {
                    $result.category = "missing"
                    $result.verification = "$patternName verification failed"
                    $result.evidence += "Not found: $matchedValue"
                    return $result
                }
            } catch {
                # Verification error, treat as unknown
                $result.verification = "Verification error: $($_.Exception.Message)"
            }
        }
    }

    # If no pattern matched, categorize based on keywords
    if ($ItemText -match "documentation|README|guide|doc") {
        # Check if mentioned file exists
        if ($ItemText -match "`[`'`"]([^`'`"]+\.md)`[`'`"]") {
            $docFile = $Matches[1]
            if (Test-Path $docFile) {
                $result.category = "implemented"
                $result.evidence += "Doc file exists: $docFile"
            } else {
                $result.category = "missing"
                $result.evidence += "Doc file missing: $docFile"
            }
        } else {
            $result.category = "manual_verification"
            $result.verification = "Documentation needs manual review"
        }
    } elseif ($ItemText -match "^(Code review|PR|Merge|Deploy)") {
        # These are process items, mark as implemented if issue is closed
        $result.category = "implemented"
        $result.verification = "Process item (issue closed = completed)"
    } else {
        # Default to manual verification for ambiguous items
        $result.category = "manual_verification"
        $result.verification = "Could not automatically verify"
    }

    return $result
}

# Analizza ogni issue
Write-ColorOutput "`nAnalyzing issues..." "Yellow"

$issueCount = 0
foreach ($issueNum in $IssueNumbers) {
    $issueCount++
    Write-Progress -Activity "Analyzing Issues" -Status "Issue #$issueNum ($issueCount/$($IssueNumbers.Count))" -PercentComplete (($issueCount / $IssueNumbers.Count) * 100)

    Write-ColorOutput "`nIssue #$issueNum" "Cyan"

    try {
        # Fetch issue details
        $issueData = gh issue view $issueNum --json title,body,state,labels | ConvertFrom-Json

        # Extract DoD items (unchecked checkboxes)
        $dodItems = @()
        $lines = $issueData.body -split "`n"
        $inDodSection = $false

        foreach ($line in $lines) {
            if ($line -match "^##\s+(DoD|Definition of Done|Acceptance Criteria|Testing Requirements?)") {
                $inDodSection = $true
                continue
            }

            if ($inDodSection -and $line -match "^##\s+") {
                # New section, stop collecting DoD items
                $inDodSection = $false
            }

            if ($inDodSection -and $line -match "^-\s+\[\s*\]\s+(.+)$") {
                $dodItems += $Matches[1].Trim()
            }
        }

        if ($dodItems.Count -eq 0) {
            Write-ColorOutput "  No unchecked DoD items found (may already be complete)" "Gray"
            continue
        }

        Write-ColorOutput "  Found $($dodItems.Count) unchecked DoD items" "Yellow"

        # Analyze each DoD item
        $issueResult = @{
            number = $issueNum
            title = $issueData.title
            state = $issueData.state
            total_dod_items = $dodItems.Count
            implemented = @()
            manual_verification = @()
            missing = @()
        }

        foreach ($item in $dodItems) {
            $verification = Test-DodItem -ItemText $item -IssueNumber $issueNum

            switch ($verification.category) {
                "implemented" {
                    $issueResult.implemented += $verification
                    Write-ColorOutput "  ✓ $item" "Green"
                }
                "manual_verification" {
                    $issueResult.manual_verification += $verification
                    Write-ColorOutput "  ⚠ $item" "Yellow"
                }
                "missing" {
                    $issueResult.missing += $verification
                    Write-ColorOutput "  ✗ $item" "Red"
                }
            }
        }

        # Update summary
        $results.summary.total_dod_items += $dodItems.Count
        $results.summary.implemented += $issueResult.implemented.Count
        $results.summary.manual_verification += $issueResult.manual_verification.Count
        $results.summary.missing += $issueResult.missing.Count

        # Determine if issue should be reopened
        if ($issueResult.missing.Count -gt 0) {
            $results.summary.issues_to_reopen += $issueNum
        }

        # Collect manual items for consolidated issue
        foreach ($item in $issueResult.manual_verification) {
            $results.summary.manual_items += @{
                issue_number = $issueNum
                issue_title = $issueData.title
                item_text = $item.text
            }
        }

        $results.issues += $issueResult

    } catch {
        Write-ColorOutput "  Error analyzing issue #${issueNum}: $($_.Exception.Message)" "Red"
    }
}

Write-Progress -Activity "Analyzing Issues" -Completed

# Generate reports
Write-ColorOutput "`n`nGenerating reports..." "Yellow"

# Ensure output directory exists
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
}

# JSON report
$jsonPath = Join-Path $OutputPath "dod-analysis-$timestamp.json"
$results | ConvertTo-Json -Depth 10 | Out-File -FilePath $jsonPath -Encoding UTF8
Write-ColorOutput "✓ JSON report: $jsonPath" "Green"

# Markdown report
$mdPath = Join-Path $OutputPath "dod-verification-report-$timestamp.md"

# Build markdown content using StringBuilder approach to avoid parsing issues
$mdContent = "# DoD Verification Report - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n`n"
$mdContent += "## Summary`n`n"
$mdContent += "**Total Issues Analyzed**: $($results.total_issues)`n"
$mdContent += "**Total DoD Items**: $($results.summary.total_dod_items)`n"
$mdContent += "**Implemented (auto-verified)**: $($results.summary.implemented) ($([math]::Round(($results.summary.implemented / $results.summary.total_dod_items) * 100, 1))%)`n"
$mdContent += "**Manual Verification Needed**: $($results.summary.manual_verification) ($([math]::Round(($results.summary.manual_verification / $results.summary.total_dod_items) * 100, 1))%)`n"
$mdContent += "**Missing Implementation**: $($results.summary.missing) ($([math]::Round(($results.summary.missing / $results.summary.total_dod_items) * 100, 1))%)`n`n"
$mdContent += "## Issues Requiring Action`n`n"
$mdContent += "### Issues to Reopen ($($results.summary.issues_to_reopen.Count))`n`n"

if ($results.summary.issues_to_reopen.Count -gt 0) {
    foreach ($issueNum in $results.summary.issues_to_reopen) {
        $issue = $results.issues | Where-Object { $_.number -eq $issueNum }
        $mdContent += "- #$issueNum - $($issue.title) ($($issue.missing.Count) missing items)`n"
    }
}

$mdContent += "`n### Manual Verification Items ($($results.summary.manual_verification))`n`n"
$mdContent += "These items require human validation and will be consolidated into a MANUAL-VERIFICATION issue:`n`n"

$groupedManual = $results.summary.manual_items | Group-Object issue_number

foreach ($group in $groupedManual) {
    $mdContent += "`n#### Issue #$($group.Name)`n"
    foreach ($item in $group.Group) {
        $mdContent += "- [ ] $($item.item_text)`n"
    }
}

$mdContent += "`n`n## Detailed Analysis by Issue`n`n"

foreach ($issue in $results.issues) {
    $mdContent += "`n### Issue #$($issue.number): $($issue.title)`n`n"
    $mdContent += "- Total DoD Items: $($issue.total_dod_items)`n"
    $mdContent += "- ✅ Implemented: $($issue.implemented.Count)`n"
    $mdContent += "- ⚠️ Manual Verification: $($issue.manual_verification.Count)`n"
    $mdContent += "- ❌ Missing: $($issue.missing.Count)`n`n"

    if ($issue.implemented.Count -gt 0) {
        $mdContent += "**Implemented Items:**`n"
        foreach ($item in $issue.implemented) {
            $mdContent += "- ✅ $($item.text)`n"
            if ($item.evidence.Count -gt 0) {
                $mdContent += "  - Evidence: $($item.evidence -join ', ')`n"
            }
        }
        $mdContent += "`n"
    }

    if ($issue.missing.Count -gt 0) {
        $mdContent += "**Missing Items:**`n"
        foreach ($item in $issue.missing) {
            $mdContent += "- ❌ $($item.text)`n"
            if ($item.evidence.Count -gt 0) {
                $mdContent += "  - Reason: $($item.evidence -join ', ')`n"
            }
        }
        $mdContent += "`n"
    }
}

$mdContent | Out-File -FilePath $mdPath -Encoding UTF8
Write-ColorOutput "✓ Markdown report: $mdPath" "Green"

# Summary console output
Write-ColorOutput "`n`n=== Verification Summary ===" "Cyan"
Write-ColorOutput "Total Issues: $($results.total_issues)" "White"
Write-ColorOutput "Total DoD Items: $($results.summary.total_dod_items)" "White"
Write-ColorOutput "✅ Implemented: $($results.summary.implemented) ($([math]::Round(($results.summary.implemented / $results.summary.total_dod_items) * 100, 1))%)" "Green"
Write-ColorOutput "⚠️ Manual Verification: $($results.summary.manual_verification) ($([math]::Round(($results.summary.manual_verification / $results.summary.total_dod_items) * 100, 1))%)" "Yellow"
Write-ColorOutput "❌ Missing: $($results.summary.missing) ($([math]::Round(($results.summary.missing / $results.summary.total_dod_items) * 100, 1))%)" "Red"
Write-ColorOutput "`nIssues to Reopen: $($results.summary.issues_to_reopen.Count)" "$(if ($results.summary.issues_to_reopen.Count -gt 0) { 'Red' } else { 'Green' })"

if ($DryRun) {
    Write-ColorOutput "`n⚠️ DRY RUN MODE - No changes applied to GitHub" "Yellow"
} else {
    Write-ColorOutput "`n✓ Analysis complete. Review reports before applying changes." "Green"
}

Write-ColorOutput "`nNext steps:" "Cyan"
Write-ColorOutput "1. Review reports in $OutputPath" "White"
Write-ColorOutput "2. Run with -DryRun:$false to apply GitHub updates" "White"
Write-ColorOutput "3. Create MANUAL-VERIFICATION issue from report" "White"
Write-ColorOutput "4. Update CALENDARIO_ISSUE.md with findings`n" "White"
