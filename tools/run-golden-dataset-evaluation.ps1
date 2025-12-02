<#
.SYNOPSIS
    Runs full golden dataset accuracy evaluation against live RAG system

.DESCRIPTION
    Executes all 1000 test cases from tests/data/golden_dataset.json against
    the running RAG API and generates detailed accuracy report.

    Issue #999 (BGAI-059): Quality test implementation for accuracy validation
    Issue #1000 (BGAI-060): Updated to use DDD /knowledge-base/ask endpoint

    WARNING: This script makes real LLM API calls (cost: ~$2-5 per run)
             Execution time: ~8-10 minutes
             Requires running MeepleAI API (localhost:8080)

.PARAMETER ApiBaseUrl
    Base URL of the MeepleAI API (default: http://localhost:8080)

.PARAMETER SampleSize
    Number of test cases to evaluate (default: all 1000)
    Use lower number for quick validation (e.g., 50)

.PARAMETER Stratified
    Use stratified sampling for sample (default: true)

.PARAMETER OutputPath
    Path for the generated report (default: claudedocs/quality/golden-dataset-results.md)

.PARAMETER DryRun
    Parse dataset and show stats without running evaluation

.EXAMPLE
    # Run full evaluation (all 1000 cases)
    pwsh tools/run-golden-dataset-evaluation.ps1

.EXAMPLE
    # Quick validation (50 cases)
    pwsh tools/run-golden-dataset-evaluation.ps1 -SampleSize 50

.EXAMPLE
    # Dry run (no API calls)
    pwsh tools/run-golden-dataset-evaluation.ps1 -DryRun

.NOTES
    Author: MeepleAI Team
    Issue: #999 (BGAI-059)
    Date: 2025-11-28
#>

param(
    [string]$ApiBaseUrl = "http://localhost:8080",
    [int]$SampleSize = 0, # 0 = all
    [bool]$Stratified = $true,
    [string]$OutputPath = "claudedocs/quality/golden-dataset-results.md",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Color output functions
function Write-Info { param([string]$Message) Write-Host "[INFO] $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "[SUCCESS] $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "[WARNING] $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "[ERROR] $Message" -ForegroundColor Red }

Write-Info "Starting Golden Dataset Accuracy Evaluation"
Write-Info "============================================"

# 1. Load golden dataset
$datasetPath = "tests/data/golden_dataset.json"
if (-not (Test-Path $datasetPath)) {
    Write-Error "Golden dataset not found at: $datasetPath"
    exit 1
}

Write-Info "Loading golden dataset from: $datasetPath"
$dataset = Get-Content $datasetPath | ConvertFrom-Json

$totalCases = 0
foreach ($game in $dataset.games) {
    $totalCases += $game.test_cases.Count
}

Write-Success "Loaded dataset: $totalCases test cases from $($dataset.games.Count) games"

# Show statistics
Write-Info "`nDataset Statistics:"
Write-Info "  Version: $($dataset.metadata.version)"
Write-Info "  Total test cases: $totalCases"
Write-Info "  Games: $($dataset.games.Count)"

# 2. Prepare test cases list
$testCases = @()
foreach ($game in $dataset.games) {
    foreach ($testCase in $game.test_cases) {
        $testCases += [PSCustomObject]@{
            Id = $testCase.id
            GameId = $game.game_id
            GameName = $game.game_name
            Question = $testCase.question
            ExpectedKeywords = $testCase.expected_answer_keywords
            ExpectedCitations = $testCase.expected_citations
            ForbiddenKeywords = $testCase.forbidden_keywords
            Difficulty = $testCase.difficulty
            Category = $testCase.category
        }
    }
}

# 3. Sample if requested
if ($SampleSize -gt 0 -and $SampleSize -lt $testCases.Count) {
    Write-Info "`nSampling $SampleSize test cases (Stratified: $Stratified)"

    if ($Stratified) {
        # Stratified sampling by difficulty
        $easy = $testCases | Where-Object { $_.Difficulty -eq "easy" }
        $medium = $testCases | Where-Object { $_.Difficulty -eq "medium" }
        $hard = $testCases | Where-Object { $_.Difficulty -eq "hard" }

        $easyCount = [Math]::Round($easy.Count / $testCases.Count * $SampleSize)
        $mediumCount = [Math]::Round($medium.Count / $testCases.Count * $SampleSize)
        $hardCount = $SampleSize - $easyCount - $mediumCount

        $sampled = @()
        $sampled += $easy | Get-Random -Count $easyCount
        $sampled += $medium | Get-Random -Count $mediumCount
        $sampled += $hard | Get-Random -Count $hardCount

        $testCases = $sampled
        Write-Info "  Easy: $easyCount, Medium: $mediumCount, Hard: $hardCount"
    } else {
        $testCases = $testCases | Get-Random -Count $SampleSize
    }
}

Write-Success "Test cases to evaluate: $($testCases.Count)"

if ($DryRun) {
    Write-Warning "`nDry run mode - skipping evaluation"
    Write-Info "Test cases by difficulty:"
    $testCases | Group-Object Difficulty | ForEach-Object {
        Write-Info "  $($_.Name): $($_.Count)"
    }
    Write-Info "`nTest cases by category:"
    $testCases | Group-Object Category | ForEach-Object {
        Write-Info "  $($_.Name): $($_.Count)"
    }
    exit 0
}

# 4. Check API availability
Write-Info "`nChecking API availability at: $ApiBaseUrl"
try {
    $healthCheck = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -Method Get -TimeoutSec 5
    Write-Success "API is healthy: $($healthCheck.status)"
} catch {
    Write-Error "API not available at $ApiBaseUrl. Please start the API first."
    Write-Info "  cd apps/api/src/Api && dotnet run"
    exit 1
}

# 5. Run evaluation
Write-Info "`nStarting evaluation..."
Write-Warning "This will make ~$($testCases.Count) LLM API calls (cost: ~`$$(($testCases.Count * 0.005).ToString('F2')))"
Write-Info "Press Ctrl+C to cancel..."
Start-Sleep -Seconds 3

$results = @()
$progressCount = 0

foreach ($testCase in $testCases) {
    $progressCount++
    if ($progressCount % 10 -eq 0) {
        Write-Info "Progress: $progressCount / $($testCases.Count)"
    }

    try {
        # Call RAG API (DDD endpoint - Issue #1000)
        $body = @{
            query = $testCase.Question  # DDD uses 'query' instead of 'question'
            gameId = $testCase.GameId
            language = "it"
            bypassCache = $true  # Ensure fresh responses for accuracy testing
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "$ApiBaseUrl/knowledge-base/ask" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 30

        # Evaluate response
        $keywordMatches = 0
        foreach ($keyword in $testCase.ExpectedKeywords) {
            if ($response.answer -match [regex]::Escape($keyword)) {
                $keywordMatches++
            }
        }
        $keywordMatchRate = if ($testCase.ExpectedKeywords.Count -gt 0) { $keywordMatches / $testCase.ExpectedKeywords.Count } else { 1.0 }

        # Check citations
        $citationValid = $true
        if ($testCase.ExpectedCitations.Count -gt 0 -and $response.snippets) {
            foreach ($expectedCit in $testCase.ExpectedCitations) {
                $found = $false
                foreach ($snippet in $response.snippets) {
                    if ($snippet.page -eq $expectedCit.page -and $snippet.text -match [regex]::Escape($expectedCit.snippet_contains)) {
                        $found = $true
                        break
                    }
                }
                if (-not $found) {
                    $citationValid = $false
                    break
                }
            }
        }

        # Check forbidden keywords (hallucination)
        $hasForbidden = $false
        foreach ($forbidden in $testCase.ForbiddenKeywords) {
            if ($response.answer -match [regex]::Escape($forbidden)) {
                $hasForbidden = $true
                break
            }
        }

        $isCorrect = ($keywordMatchRate -eq 1.0) -and $citationValid -and (-not $hasForbidden)

        $results += [PSCustomObject]@{
            TestCaseId = $testCase.Id
            GameId = $testCase.GameId
            Difficulty = $testCase.Difficulty
            Category = $testCase.Category
            IsCorrect = $isCorrect
            KeywordMatchRate = $keywordMatchRate
            CitationValid = $citationValid
            NoForbidden = -not $hasForbidden
            Confidence = $response.overallConfidence  # DDD uses overallConfidence
        }

    } catch {
        Write-Warning "Failed to evaluate test case $($testCase.Id): $_"
        $results += [PSCustomObject]@{
            TestCaseId = $testCase.Id
            GameId = $testCase.GameId
            Difficulty = $testCase.Difficulty
            Category = $testCase.Category
            IsCorrect = $false
            KeywordMatchRate = 0.0
            CitationValid = $false
            NoForbidden = $true
            Confidence = 0.0
        }
    }
}

Write-Success "`nEvaluation complete: $($results.Count) test cases processed"

# 6. Calculate metrics
$correct = ($results | Where-Object { $_.IsCorrect }).Count
$accuracy = $correct / $results.Count
$avgConfidence = ($results | Measure-Object -Property Confidence -Average).Average

# Metrics by difficulty
$difficultyMetrics = $results | Group-Object Difficulty | ForEach-Object {
    $groupCorrect = ($_.Group | Where-Object { $_.IsCorrect }).Count
    $groupAccuracy = $groupCorrect / $_.Count
    [PSCustomObject]@{
        Difficulty = $_.Name
        Total = $_.Count
        Correct = $groupCorrect
        Accuracy = $groupAccuracy
    }
}

# Metrics by category
$categoryMetrics = $results | Group-Object Category | ForEach-Object {
    $groupCorrect = ($_.Group | Where-Object { $_.IsCorrect }).Count
    $groupAccuracy = $groupCorrect / $_.Count
    [PSCustomObject]@{
        Category = $_.Name
        Total = $_.Count
        Correct = $groupCorrect
        Accuracy = $groupAccuracy
    }
}

# Metrics by game
$gameMetrics = $results | Group-Object GameId | ForEach-Object {
    $groupCorrect = ($_.Group | Where-Object { $_.IsCorrect }).Count
    $groupAccuracy = $groupCorrect / $_.Count
    [PSCustomObject]@{
        GameId = $_.Name
        Total = $_.Count
        Correct = $groupCorrect
        Accuracy = $groupAccuracy
    }
}

# 7. Display results
Write-Info "`n===== OVERALL RESULTS ====="
Write-Info "Total test cases: $($results.Count)"
Write-Info "Correct answers: $correct"
Write-Info "Accuracy: $($accuracy.ToString('P2')) (threshold: 80%)"
Write-Info "Average confidence: $($avgConfidence.ToString('F2'))"
Write-Info "Baseline threshold: $(if ($accuracy -ge 0.80) { 'PASSED ✓' } else { 'FAILED ✗' })"

Write-Info "`n===== BY DIFFICULTY ====="
$difficultyMetrics | Format-Table -AutoSize

Write-Info "===== BY CATEGORY ====="
$categoryMetrics | Format-Table -AutoSize

Write-Info "===== BY GAME ====="
$gameMetrics | Format-Table -AutoSize

# 8. Generate markdown report
$reportDir = Split-Path $OutputPath
if ($reportDir -and -not (Test-Path $reportDir)) {
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$reportContent = @"
# Golden Dataset Accuracy Evaluation Report

**Generated**: $timestamp
**Test Cases**: $($results.Count)
**API**: $ApiBaseUrl

---

## Overall Results

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| **Accuracy** | $($accuracy.ToString('P2')) | ≥80% | $(if ($accuracy -ge 0.80) { '✅ PASS' } else { '❌ FAIL' }) |
| **Correct Answers** | $correct / $($results.Count) | - | - |
| **Average Confidence** | $($avgConfidence.ToString('F2')) | ≥0.70 | $(if ($avgConfidence -ge 0.70) { '✅ PASS' } else { '❌ FAIL' }) |

---

## Results by Difficulty

| Difficulty | Total | Correct | Accuracy |
|------------|-------|---------|----------|
$($difficultyMetrics | ForEach-Object { "| $($_.Difficulty) | $($_.Total) | $($_.Correct) | $($_.Accuracy.ToString('P2')) |" })

---

## Results by Category

| Category | Total | Correct | Accuracy |
|----------|-------|---------|----------|
$($categoryMetrics | ForEach-Object { "| $($_.Category) | $($_.Total) | $($_.Correct) | $($_.Accuracy.ToString('P2')) |" })

---

## Results by Game

| Game | Total | Correct | Accuracy |
|------|-------|---------|----------|
$($gameMetrics | ForEach-Object { "| $($_.GameId) | $($_.Total) | $($_.Correct) | $($_.Accuracy.ToString('P2')) |" })

---

## Failed Test Cases

$(if (($results | Where-Object { -not $_.IsCorrect }).Count -eq 0) {
    "No failed test cases. All tests passed! 🎉"
} else {
    "| Test ID | Game | Difficulty | Reason |`n|---------|------|------------|--------|"
    ($results | Where-Object { -not $_.IsCorrect } | ForEach-Object {
        $reason = @()
        if ($_.KeywordMatchRate -lt 1.0) { $reason += "Keywords" }
        if (-not $_.CitationValid) { $reason += "Citations" }
        if (-not $_.NoForbidden) { $reason += "Hallucination" }
        "| $($_.TestCaseId) | $($_.GameId) | $($_.Difficulty) | $($reason -join ', ') |"
    })
})

---

## Recommendations

$(if ($accuracy -ge 0.95) {
    "- ✅ Excellent performance! System exceeds expectations."
    "- Consider maintaining current prompt templates and validation settings."
} elseif ($accuracy -ge 0.80) {
    "- ✅ Good performance! System meets baseline threshold."
    "- Review failed cases to identify improvement opportunities."
} else {
    "- ❌ Performance below threshold ($($accuracy.ToString('P2')) < 80%)"
    "- **Action required**: Investigate failed cases and improve validation layers."
    "- Focus on: $(($difficultyMetrics | Sort-Object Accuracy | Select-Object -First 1).Difficulty) difficulty questions"
})

---

**Generated by**: tools/run-golden-dataset-evaluation.ps1
**Issue**: #999 (BGAI-059)
"@

$reportContent | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Success "`nReport saved to: $OutputPath"

# 9. Exit code
if ($accuracy -ge 0.80) {
    Write-Success "`n✅ EVALUATION PASSED - Accuracy meets threshold (≥80%)"
    exit 0
} else {
    Write-Error "`n❌ EVALUATION FAILED - Accuracy below threshold ($($accuracy.ToString('P2')) < 80%)"
    exit 1
}
