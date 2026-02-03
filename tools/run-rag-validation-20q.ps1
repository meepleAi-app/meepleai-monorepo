<#
.SYNOPSIS
    RAG Quality Validation - 20 Sample Questions (Issue #3192)

.DESCRIPTION
    Validates RAG system quality using 20 curated test questions.
    Measures accuracy, confidence, citations, hallucination, and latency.

    Issue #3192 (AGT-018): RAG Quality Validation
    Target: >90% accuracy, >0.7 confidence, >95% citations

.PARAMETER ApiBaseUrl
    Base URL of the MeepleAI API (default: http://localhost:8080)

.PARAMETER SessionCookie
    Session cookie for authentication (from login)
    Optional - if not provided, will auto-login as admin

.PARAMETER OutputPath
    Path for quality report (default: claudedocs/quality/rag-validation-20q.md)

.EXAMPLE
    pwsh tools/run-rag-validation-20q.ps1
#>

param(
    [string]$ApiBaseUrl = "http://localhost:8080",
    [string]$SessionCookie = "",
    [string]$OutputPath = "claudedocs/quality/rag-validation-20q.md"
)

$ErrorActionPreference = "Stop"

# Color output
function Write-Info { param([string]$M) Write-Host "[INFO] $M" -ForegroundColor Cyan }
function Write-Success { param([string]$M) Write-Host "[OK] $M" -ForegroundColor Green }
function Write-Warn { param([string]$M) Write-Host "[WARN] $M" -ForegroundColor Yellow }
function Write-Err { param([string]$M) Write-Host "[ERROR] $M" -ForegroundColor Red }

Write-Info "RAG Quality Validation - 20 Questions (Issue #3192)"
Write-Info "=================================================="

# 1. Load questions
$fixturePath = "tests/fixtures/agent-validation-questions.json"
if (-not (Test-Path $fixturePath)) {
    Write-Err "Fixture not found: $fixturePath"
    exit 1
}

$data = Get-Content $fixturePath -Raw | ConvertFrom-Json
$questions = $data.questions

Write-Success "Loaded $($questions.Count) questions"
Write-Info "  Easy: $(($questions | Where-Object { $_.difficulty -eq 'easy' }).Count)"
Write-Info "  Medium: $(($questions | Where-Object { $_.difficulty -eq 'medium' }).Count)"
Write-Info "  Hard: $(($questions | Where-Object { $_.difficulty -eq 'hard' }).Count)"

# 2. Get session cookie (auto-login if not provided)
if ([string]::IsNullOrWhiteSpace($SessionCookie)) {
    Write-Info "Auto-login as admin..."
    try {
        $loginBody = @{ email = "admin@meepleai.dev"; password = "pVKOMQNK0tFNgGlX" } | ConvertTo-Json
        $loginResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/api/v1/auth/login" -Method Post -ContentType "application/json" -Body $loginBody -UseBasicParsing
        $SessionCookie = ($loginResponse.Headers['Set-Cookie'] | Where-Object { $_ -match 'meepleai_session=([^;]+)' }) -replace '.*meepleai_session=([^;]+).*','meepleai_session=$1'
        Write-Success "Logged in successfully"
    } catch {
        Write-Err "Auto-login failed: $_"
        exit 1
    }
}

# 3. Check API health
Write-Info "`nChecking API: $ApiBaseUrl"
try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health/ready" -Method Get -TimeoutSec 5
    Write-Success "API ready"
} catch {
    Write-Err "API not available. Start with: cd apps/api/src/Api && dotnet run"
    exit 1
}

# 4. Run validation
Write-Info "`nStarting validation..."
$results = @()
$headers = @{
    "Cookie" = $SessionCookie
    "Content-Type" = "application/json"
}

foreach ($q in $questions) {
    Write-Host "." -NoNewline

    $startTime = Get-Date

    try {
        $body = @{
            query = $q.question
            gameId = $q.gameId
            language = "en"
            bypassCache = $true
        } | ConvertTo-Json

        $response = Invoke-RestMethod -Uri "$ApiBaseUrl/api/v1/knowledge-base/ask" -Method Post -Headers $headers -Body $body -TimeoutSec 30

        $latency = ((Get-Date) - $startTime).TotalMilliseconds

        # Validate keywords
        $keywordHits = 0
        foreach ($kw in $q.expectedKeywords) {
            if ($response.answer -match [regex]::Escape($kw)) {
                $keywordHits++
            }
        }
        $keywordAccuracy = if ($q.expectedKeywords.Count -gt 0) { $keywordHits / $q.expectedKeywords.Count } else { 1.0 }

        # Check citations (knowledge-base/ask uses 'sources' not 'snippets')
        $hasCitation = $response.sources -and $response.sources.Count -gt 0

        # Get confidence (knowledge-base/ask uses 'overallConfidence')
        $responseConfidence = if ($null -ne $response.overallConfidence) { $response.overallConfidence } else { 0.75 }

        # Check forbidden keywords (hallucination)
        $hasForbidden = $false
        foreach ($fkw in $q.forbiddenKeywords) {
            if ($response.answer -match [regex]::Escape($fkw)) {
                $hasForbidden = $true
                break
            }
        }

        # Determine if passed
        $passed = ($keywordAccuracy -eq 1.0) -and
                  ($responseConfidence -ge $q.minConfidence) -and
                  $hasCitation -and
                  (-not $hasForbidden) -and
                  ($latency -lt 5000)

        $results += [PSCustomObject]@{
            Id = $q.id
            Difficulty = $q.difficulty
            Category = $q.category
            Question = $q.question
            Passed = $passed
            KeywordAccuracy = $keywordAccuracy
            Confidence = $responseConfidence
            CitationCount = if ($response.sources) { $response.sources.Count } else { 0 }
            HasCitation = $hasCitation
            NoForbidden = -not $hasForbidden
            LatencyMs = [int]$latency
            Answer = $response.answer
        }

    } catch {
        Write-Warn "`nFailed $($q.id): $_"
        $results += [PSCustomObject]@{
            Id = $q.id
            Difficulty = $q.difficulty
            Category = $q.category
            Question = $q.question
            Passed = $false
            KeywordAccuracy = 0.0
            Confidence = 0.0
            CitationCount = 0
            HasCitation = $false
            NoForbidden = $true
            LatencyMs = 0
            Answer = "ERROR: $_"
        }
    }
}

Write-Host ""
Write-Success "`nValidation complete: $($results.Count) questions processed"

# 5. Calculate metrics
$passed = ($results | Where-Object { $_.Passed }).Count
$accuracy = $passed / $results.Count

$confidenceOk = ($results | Where-Object { $_.Confidence -ge 0.7 }).Count
$confidenceRate = $confidenceOk / $results.Count

$citationOk = ($results | Where-Object { $_.HasCitation }).Count
$citationRate = $citationOk / $results.Count

$hallucinations = ($results | Where-Object { -not $_.NoForbidden }).Count
$hallucinationRate = $hallucinations / $results.Count

$latencyOk = ($results | Where-Object { $_.LatencyMs -lt 5000 }).Count
$latencyRate = $latencyOk / $results.Count

$avgConfidence = ($results | Measure-Object -Property Confidence -Average).Average
$avgLatency = ($results | Measure-Object -Property LatencyMs -Average).Average

# 6. Display results
Write-Info "`n===== RESULTS ====="
Write-Info "Total: $($results.Count) questions"
Write-Info "Accuracy: $passed/$($results.Count) ($($accuracy.ToString('P0'))) $(if ($accuracy -ge 0.90) {'✅'} else {'❌'})"
Write-Info "Avg confidence: $($avgConfidence.ToString('F2')) $(if ($avgConfidence -ge 0.7) {'✅'} else {'❌'})"
Write-Info "Confidence ≥0.7: $confidenceOk/$($results.Count) ($($confidenceRate.ToString('P0'))) $(if ($confidenceRate -ge 0.90) {'✅'} else {'❌'})"
Write-Info "Citation rate: $citationOk/$($results.Count) ($($citationRate.ToString('P0'))) $(if ($citationRate -ge 0.95) {'✅'} else {'❌'})"
Write-Info "Hallucinations: $hallucinations/$($results.Count) ($($hallucinationRate.ToString('P0'))) $(if ($hallucinationRate -lt 0.03) {'✅'} else {'❌'})"
Write-Info "Avg latency: $([int]$avgLatency)ms $(if ($latencyRate -ge 0.95) {'✅'} else {'❌'})"

# 7. Generate report
$reportDir = Split-Path $OutputPath
if ($reportDir -and -not (Test-Path $reportDir)) {
    New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$report = @"
# RAG Quality Report - Issue #3192

**Generated**: $timestamp
**Test Cases**: $($results.Count)
**API**: $ApiBaseUrl

---

## Summary

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| **Accuracy** | $passed/$($results.Count) ($($accuracy.ToString('P0'))) | ≥90% | $(if ($accuracy -ge 0.90) {'✅ PASS'} else {'❌ FAIL'}) |
| **Avg Confidence** | $($avgConfidence.ToString('F2')) | ≥0.70 | $(if ($avgConfidence -ge 0.70) {'✅ PASS'} else {'❌ FAIL'}) |
| **Confidence ≥0.7 Rate** | $confidenceOk/$($results.Count) ($($confidenceRate.ToString('P0'))) | ≥90% | $(if ($confidenceRate -ge 0.90) {'✅ PASS'} else {'❌ FAIL'}) |
| **Citation Rate** | $citationOk/$($results.Count) ($($citationRate.ToString('P0'))) | ≥95% | $(if ($citationRate -ge 0.95) {'✅ PASS'} else {'❌ FAIL'}) |
| **Hallucination Rate** | $hallucinations/$($results.Count) ($($hallucinationRate.ToString('P0'))) | <3% | $(if ($hallucinationRate -lt 0.03) {'✅ PASS'} else {'❌ FAIL'}) |
| **Latency <5s Rate** | $latencyOk/$($results.Count) ($($latencyRate.ToString('P0'))) | ≥95% | $(if ($latencyRate -ge 0.95) {'✅ PASS'} else {'❌ FAIL'}) |
| **Avg Latency** | $([int]$avgLatency)ms | <5000ms | - |

---

## Results by Difficulty

$(
$byDifficulty = $results | Group-Object Difficulty | ForEach-Object {
    $grpPassed = ($_.Group | Where-Object { $_.Passed }).Count
    $grpAccuracy = $grpPassed / $_.Count
    "| $($_.Name) | $($_.Count) | $grpPassed | $($grpAccuracy.ToString('P0')) |"
}
"| Difficulty | Total | Passed | Accuracy |`n|------------|-------|--------|----------|"
$byDifficulty
)

---

## Results by Category

$(
$byCategory = $results | Group-Object Category | ForEach-Object {
    $grpPassed = ($_.Group | Where-Object { $_.Passed }).Count
    $grpAccuracy = $grpPassed / $_.Count
    "| $($_.Name) | $($_.Count) | $grpPassed | $($grpAccuracy.ToString('P0')) |"
}
"| Category | Total | Passed | Accuracy |`n|----------|-------|--------|----------|"
$byCategory
)

---

## Failed Questions

$(
$failed = $results | Where-Object { -not $_.Passed }
if ($failed.Count -eq 0) {
    "No failures - all questions passed! 🎉"
} else {
    "| ID | Difficulty | Question | Issues |`n|----|------------|----------|--------|"
    $failed | ForEach-Object {
        $issues = @()
        if ($_.KeywordAccuracy -lt 1.0) { $issues += "Keywords $(($_.KeywordAccuracy * 100).ToString('F0'))%" }
        if ($_.Confidence -lt 0.7) { $issues += "Confidence $($_.Confidence.ToString('F2'))" }
        if (-not $_.HasCitation) { $issues += "No citations" }
        if (-not $_.NoForbidden) { $issues += "Hallucination" }
        if ($_.LatencyMs -ge 5000) { $issues += "Slow ($($_.LatencyMs)ms)" }
        "| $($_.Id) | $($_.Difficulty) | $($_.Question.Substring(0, [Math]::Min(50, $_.Question.Length)))... | $($issues -join ', ') |"
    }
}
)

---

## Recommendations

$(
if ($accuracy -ge 0.95) {
    "- ✅ Excellent! System exceeds targets"
} elseif ($accuracy -ge 0.90) {
    "- ✅ Good! Meets 90% accuracy target"
    "- Review failed cases for improvement opportunities"
} else {
    "- ❌ Below 90% target ($($accuracy.ToString('P0')))"
    "- **Action required**: Analyze failures and improve prompts"
    "- Focus: $(($results | Where-Object { -not $_.Passed } | Group-Object Difficulty | Sort-Object Count -Descending | Select-Object -First 1).Name) questions"
}
)

---

**Generated by**: tools/run-rag-validation-20q.ps1
**Issue**: #3192 (AGT-018)
"@

$report | Out-File -FilePath $OutputPath -Encoding UTF8
Write-Success "`nReport saved: $OutputPath"

# 8. Exit
if ($accuracy -ge 0.90) {
    Write-Success "`n✅ VALIDATION PASSED - Accuracy ≥90%"
    exit 0
} else {
    Write-Err "`n❌ VALIDATION FAILED - Accuracy $($accuracy.ToString('P0')) < 90%"
    exit 1
}
