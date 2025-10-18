# Prompt Testing and Evaluation Framework - Technical Design

**Document Version**: 1.0
**Created**: 2025-10-18
**Status**: Design Document
**Related**: admin-prompt-management-architecture.md

## Table of Contents

1. [Overview](#overview)
2. [Requirements](#requirements)
3. [Test Dataset Format](#test-dataset-format)
4. [Evaluation Metrics](#evaluation-metrics)
5. [Architecture](#architecture)
6. [Integration Points](#integration-points)
7. [UI Design](#ui-design)
8. [CI/CD Integration](#cicd-integration)

---

## Overview

The Prompt Testing Framework provides automated quality assurance for LLM prompts before activation. It prevents quality regressions by running new prompt versions against curated test datasets and measuring key metrics.

**Core Capabilities**:
- Run prompt versions against test datasets
- Calculate evaluation metrics (accuracy, hallucination rate, confidence, latency)
- Compare two prompt versions side-by-side (A/B testing)
- Generate detailed reports with passing/failing queries
- Integrate with CI/CD for automated testing
- Store test results in version metadata for audit trail

**Primary Use Cases**:
1. **Pre-Activation Testing**: Admin creates new version → runs tests → activates if pass
2. **A/B Comparison**: Admin compares v3 vs v5 to decide which to activate
3. **Regression Detection**: CI runs tests on prompt migrations to catch breaking changes
4. **Quality Monitoring**: Weekly batch tests to ensure production prompts maintain quality

---

## Requirements

### Functional Requirements

**FR-1**: Load test datasets from JSON files (schema-validated)
**FR-2**: Execute prompt version against each query in dataset
**FR-3**: Calculate 5 core metrics: accuracy, hallucination rate, confidence, latency, citation correctness
**FR-4**: Generate pass/fail status based on configurable thresholds
**FR-5**: Display detailed report: summary + query-by-query breakdown
**FR-6**: Support side-by-side A/B comparison of two versions
**FR-7**: Store test results in `prompt_versions.metadata` field
**FR-8**: Export reports as JSON and Markdown

### Non-Functional Requirements

**NFR-1**: Complete evaluation of 50-query dataset in < 5 minutes
**NFR-2**: Support concurrent evaluations (background jobs)
**NFR-3**: Handle test datasets up to 1MB (500+ queries)
**NFR-4**: Graceful handling of LLM API failures (retry with exponential backoff)

---

## Test Dataset Format

### JSON Schema

**File Location**: `tests/Api.Tests/TestData/prompt-evaluation/{template-name}.json`

**Example**: `qa-system-prompt-test-dataset.json`

```json
{
  "testSuite": "qa-system-prompt",
  "version": "1.0",
  "description": "Test dataset for Q&A agent prompt",
  "metadata": {
    "createdBy": "admin@meepleai.dev",
    "createdAt": "2025-10-15T10:00:00Z",
    "gamesCovered": ["Tic-Tac-Toe", "Chess"],
    "totalQueries": 24
  },
  "thresholds": {
    "minimumAccuracy": 0.80,
    "maximumHallucinationRate": 0.10,
    "minimumAverageConfidence": 0.70,
    "maximumAverageLatencyMs": 3000
  },
  "testCases": [
    {
      "id": "qa-001",
      "category": "setup",
      "difficulty": "easy",
      "gameId": "tic-tac-toe-id",
      "query": "How many players can play Tic-Tac-Toe?",
      "expectedBehavior": "should_answer",
      "groundTruth": "2 players",
      "relevantDocIds": ["pdf-tictactoe-001"],
      "relevantPages": [1],
      "keywords": ["2", "two", "players"],
      "mustNotContain": ["3", "three", "four"],
      "minimumConfidence": 0.85
    },
    {
      "id": "qa-002",
      "category": "gameplay",
      "difficulty": "medium",
      "gameId": "chess-id",
      "query": "Can a pawn move backwards?",
      "expectedBehavior": "should_answer",
      "groundTruth": "No, pawns cannot move backwards",
      "relevantDocIds": ["pdf-chess-001"],
      "relevantPages": [5],
      "keywords": ["no", "cannot", "backward"],
      "mustNotContain": ["yes", "can move"],
      "minimumConfidence": 0.80
    },
    {
      "id": "qa-003",
      "category": "edge-case",
      "difficulty": "hard",
      "gameId": "chess-id",
      "query": "What happens if both kings are in check simultaneously?",
      "expectedBehavior": "should_refuse",
      "groundTruth": "Not specified (impossible scenario)",
      "relevantDocIds": [],
      "relevantPages": [],
      "keywords": ["not specified", "impossible", "invalid"],
      "mustNotContain": ["checkmate", "draw"],
      "minimumConfidence": 0.60
    },
    {
      "id": "qa-004",
      "category": "out-of-context",
      "difficulty": "easy",
      "gameId": "chess-id",
      "query": "Who is the current world chess champion?",
      "expectedBehavior": "should_refuse",
      "groundTruth": "Not specified (external knowledge)",
      "relevantDocIds": [],
      "relevantPages": [],
      "keywords": ["not specified"],
      "mustNotContain": ["Magnus", "Carlsen", "Ding"],
      "minimumConfidence": 0.50
    }
  ]
}
```

### Field Definitions

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `testSuite` | string | Yes | Unique identifier (matches template name) |
| `version` | string | Yes | Dataset version (semantic versioning) |
| `description` | string | No | Human-readable description |
| `thresholds` | object | Yes | Pass/fail thresholds for metrics |
| `testCases` | array | Yes | List of test queries |
| `testCases[].id` | string | Yes | Unique test case identifier |
| `testCases[].category` | string | Yes | Category: setup, gameplay, edge-case, out-of-context |
| `testCases[].difficulty` | string | Yes | Difficulty: easy, medium, hard |
| `testCases[].gameId` | string | Yes | Game ID from database |
| `testCases[].query` | string | Yes | User query to test |
| `testCases[].expectedBehavior` | string | Yes | should_answer or should_refuse |
| `testCases[].groundTruth` | string | Yes | Expected answer or "Not specified" |
| `testCases[].relevantDocIds` | array | Yes | PDF document IDs that contain answer |
| `testCases[].relevantPages` | array | Yes | Page numbers in PDFs |
| `testCases[].keywords` | array | Yes | Keywords that should appear in response |
| `testCases[].mustNotContain` | array | Yes | Keywords that must NOT appear (hallucination detection) |
| `testCases[].minimumConfidence` | float | No | Override default confidence threshold |

### Expected Behaviors

- **should_answer**: Prompt should provide factual answer based on context
- **should_refuse**: Prompt should respond with "Not specified" (anti-hallucination test)

---

## Evaluation Metrics

### Metric 1: Accuracy Score

**Definition**: Percentage of test cases where the LLM response matches expected behavior

**Calculation**:
```csharp
public float CalculateAccuracy(List<EvaluationResult> results)
{
    int totalCases = results.Count;
    int correctCases = results.Count(r => r.IsCorrect);
    return (float)correctCases / totalCases;
}

// Per-test-case logic
public bool IsCorrect(TestCase testCase, string llmResponse)
{
    if (testCase.ExpectedBehavior == "should_answer")
    {
        // Check if all keywords present
        bool hasAllKeywords = testCase.Keywords.All(kw =>
            llmResponse.Contains(kw, StringComparison.OrdinalIgnoreCase));

        // Check if no forbidden words present
        bool hasNoForbidden = !testCase.MustNotContain.Any(kw =>
            llmResponse.Contains(kw, StringComparison.OrdinalIgnoreCase));

        // Check if not "Not specified" (anti-hallucination check)
        bool isNotRefusal = !llmResponse.Contains("Not specified", StringComparison.OrdinalIgnoreCase);

        return hasAllKeywords && hasNoForbidden && isNotRefusal;
    }
    else if (testCase.ExpectedBehavior == "should_refuse")
    {
        // Should respond with "Not specified"
        return llmResponse.Contains("Not specified", StringComparison.OrdinalIgnoreCase);
    }

    return false;
}
```

**Threshold**: 80% (configurable per dataset)

---

### Metric 2: Hallucination Rate

**Definition**: Percentage of test cases where LLM fabricated information not in context

**Calculation**:
```csharp
public float CalculateHallucinationRate(List<EvaluationResult> results)
{
    int totalCases = results.Count;
    int hallucinatedCases = results.Count(r => r.IsHallucination);
    return (float)hallucinatedCases / totalCases;
}

// Hallucination detection logic
public bool IsHallucination(TestCase testCase, string llmResponse)
{
    // Case 1: should_refuse but gave specific answer
    if (testCase.ExpectedBehavior == "should_refuse" &&
        !llmResponse.Contains("Not specified", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    // Case 2: should_answer but contains forbidden keywords
    if (testCase.ExpectedBehavior == "should_answer" &&
        testCase.MustNotContain.Any(kw =>
            llmResponse.Contains(kw, StringComparison.OrdinalIgnoreCase)))
    {
        return true;
    }

    return false;
}
```

**Threshold**: < 10% (configurable per dataset)

---

### Metric 3: Average Confidence Score

**Definition**: Mean of RAG confidence scores across all test cases

**Calculation**:
```csharp
public float CalculateAverageConfidence(List<EvaluationResult> results)
{
    if (!results.Any()) return 0f;

    var confidenceScores = results
        .Where(r => r.Confidence.HasValue)
        .Select(r => r.Confidence.Value)
        .ToList();

    if (!confidenceScores.Any()) return 0f;

    return confidenceScores.Average();
}
```

**Threshold**: > 0.70 (configurable per dataset)

**Note**: Confidence comes from RAG search (max similarity score of retrieved chunks)

---

### Metric 4: Citation Correctness

**Definition**: Percentage of test cases where cited page numbers match expected pages

**Calculation**:
```csharp
public float CalculateCitationCorrectness(List<EvaluationResult> results)
{
    int totalWithExpectedPages = results.Count(r => r.TestCase.RelevantPages.Any());
    if (totalWithExpectedPages == 0) return 1.0f; // N/A

    int correctCitations = results.Count(r =>
        r.TestCase.RelevantPages.Any() &&
        r.CitedPages.Intersect(r.TestCase.RelevantPages).Any());

    return (float)correctCitations / totalWithExpectedPages;
}
```

**Threshold**: > 0.80 (optional metric)

---

### Metric 5: Average Latency

**Definition**: Mean response time (RAG search + LLM generation) per query

**Calculation**:
```csharp
public float CalculateAverageLatency(List<EvaluationResult> results)
{
    return results.Average(r => r.LatencyMs);
}
```

**Threshold**: < 3000ms (configurable per dataset)

---

## Architecture

### Service Interface

**File**: `apps/api/src/Api/Services/IPromptEvaluationService.cs`

```csharp
namespace Api.Services;

public interface IPromptEvaluationService
{
    /// <summary>
    /// Load test dataset from JSON file.
    /// </summary>
    Task<TestDataset> LoadDatasetAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Evaluate a prompt version against a test dataset.
    /// Returns detailed report with per-query results.
    /// </summary>
    Task<EvaluationReport> EvaluateAsync(
        string templateId,
        string versionId,
        TestDataset dataset,
        CancellationToken ct = default);

    /// <summary>
    /// Compare two prompt versions side-by-side.
    /// Returns comparative metrics for both versions.
    /// </summary>
    Task<ComparisonReport> CompareVersionsAsync(
        string templateId,
        string versionIdA,
        string versionIdB,
        TestDataset dataset,
        CancellationToken ct = default);

    /// <summary>
    /// Generate Markdown report from evaluation results.
    /// </summary>
    string GenerateMarkdownReport(EvaluationReport report);

    /// <summary>
    /// Generate JSON report from evaluation results.
    /// </summary>
    string GenerateJsonReport(EvaluationReport report);

    /// <summary>
    /// Store evaluation results in version metadata.
    /// </summary>
    Task StoreResultsAsync(
        string versionId,
        EvaluationReport report,
        CancellationToken ct = default);
}
```

### Data Models

```csharp
public class TestDataset
{
    public string TestSuite { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public TestThresholds Thresholds { get; set; }
    public List<TestCase> TestCases { get; set; }
}

public class TestThresholds
{
    public float MinimumAccuracy { get; set; }
    public float MaximumHallucinationRate { get; set; }
    public float MinimumAverageConfidence { get; set; }
    public float MaximumAverageLatencyMs { get; set; }
}

public class TestCase
{
    public string Id { get; set; }
    public string Category { get; set; }
    public string Difficulty { get; set; }
    public string GameId { get; set; }
    public string Query { get; set; }
    public string ExpectedBehavior { get; set; } // should_answer | should_refuse
    public string GroundTruth { get; set; }
    public List<string> RelevantDocIds { get; set; }
    public List<int> RelevantPages { get; set; }
    public List<string> Keywords { get; set; }
    public List<string> MustNotContain { get; set; }
    public float? MinimumConfidence { get; set; }
}

public class EvaluationResult
{
    public TestCase TestCase { get; set; }
    public string LlmResponse { get; set; }
    public List<int> CitedPages { get; set; }
    public float? Confidence { get; set; }
    public float LatencyMs { get; set; }
    public bool IsCorrect { get; set; }
    public bool IsHallucination { get; set; }
    public string ErrorMessage { get; set; }
}

public class EvaluationReport
{
    public string TemplateId { get; set; }
    public string TemplateName { get; set; }
    public string VersionId { get; set; }
    public int VersionNumber { get; set; }
    public DateTime EvaluatedAt { get; set; }
    public string EvaluatedBy { get; set; }

    // Metrics
    public float Accuracy { get; set; }
    public float HallucinationRate { get; set; }
    public float AverageConfidence { get; set; }
    public float CitationCorrectness { get; set; }
    public float AverageLatencyMs { get; set; }

    // Pass/Fail
    public bool PassesThresholds { get; set; }
    public List<string> FailureReasons { get; set; }

    // Detailed Results
    public List<EvaluationResult> Results { get; set; }

    // Category Breakdown
    public Dictionary<string, CategoryStats> StatsByCategory { get; set; }
}

public class CategoryStats
{
    public int TotalQueries { get; set; }
    public int CorrectQueries { get; set; }
    public float Accuracy { get; set; }
    public float AverageConfidence { get; set; }
}

public class ComparisonReport
{
    public EvaluationReport VersionA { get; set; }
    public EvaluationReport VersionB { get; set; }
    public ComparisonSummary Summary { get; set; }
}

public class ComparisonSummary
{
    public float AccuracyDelta { get; set; } // B - A
    public float HallucinationRateDelta { get; set; }
    public float ConfidenceDelta { get; set; }
    public float LatencyDelta { get; set; }
    public string Recommendation { get; set; } // "Version B is better" | "Version A is better" | "Similar performance"
    public List<string> ImprovementsInB { get; set; } // Query IDs where B improved
    public List<string> RegressionsInB { get; set; } // Query IDs where B regressed
}
```

### Implementation Pseudocode

```csharp
public class PromptEvaluationService : IPromptEvaluationService
{
    private readonly IPromptTemplateService _promptService;
    private readonly IRagService _ragService;
    private readonly ILogger<PromptEvaluationService> _logger;

    public async Task<EvaluationReport> EvaluateAsync(
        string templateId,
        string versionId,
        TestDataset dataset,
        CancellationToken ct)
    {
        var version = await _context.PromptVersions
            .Include(v => v.Template)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct);

        if (version == null)
            throw new InvalidOperationException($"Version {versionId} not found");

        var results = new List<EvaluationResult>();

        // Run each test case
        foreach (var testCase in dataset.TestCases)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // Execute RAG query with custom prompt
                var response = await _ragService.AskWithCustomPromptAsync(
                    testCase.GameId,
                    testCase.Query,
                    version.Content,
                    ct);

                sw.Stop();

                // Evaluate result
                var result = new EvaluationResult
                {
                    TestCase = testCase,
                    LlmResponse = response.Answer,
                    CitedPages = response.Snippets.Select(s => s.Page).Distinct().ToList(),
                    Confidence = response.Confidence,
                    LatencyMs = sw.ElapsedMilliseconds,
                    IsCorrect = IsCorrect(testCase, response.Answer),
                    IsHallucination = IsHallucination(testCase, response.Answer)
                };

                results.Add(result);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Error evaluating test case {TestCaseId}", testCase.Id);
                results.Add(new EvaluationResult
                {
                    TestCase = testCase,
                    ErrorMessage = ex.Message,
                    LatencyMs = sw.ElapsedMilliseconds,
                    IsCorrect = false
                });
            }
        }

        // Calculate metrics
        var report = new EvaluationReport
        {
            TemplateId = templateId,
            TemplateName = version.Template.Name,
            VersionId = versionId,
            VersionNumber = version.VersionNumber,
            EvaluatedAt = DateTime.UtcNow,
            Accuracy = CalculateAccuracy(results),
            HallucinationRate = CalculateHallucinationRate(results),
            AverageConfidence = CalculateAverageConfidence(results),
            CitationCorrectness = CalculateCitationCorrectness(results),
            AverageLatencyMs = CalculateAverageLatency(results),
            Results = results,
            StatsByCategory = CalculateStatsByCategory(results)
        };

        // Check thresholds
        var failures = new List<string>();
        if (report.Accuracy < dataset.Thresholds.MinimumAccuracy)
            failures.Add($"Accuracy {report.Accuracy:P2} < {dataset.Thresholds.MinimumAccuracy:P2}");

        if (report.HallucinationRate > dataset.Thresholds.MaximumHallucinationRate)
            failures.Add($"Hallucination rate {report.HallucinationRate:P2} > {dataset.Thresholds.MaximumHallucinationRate:P2}");

        if (report.AverageConfidence < dataset.Thresholds.MinimumAverageConfidence)
            failures.Add($"Average confidence {report.AverageConfidence:F2} < {dataset.Thresholds.MinimumAverageConfidence:F2}");

        if (report.AverageLatencyMs > dataset.Thresholds.MaximumAverageLatencyMs)
            failures.Add($"Average latency {report.AverageLatencyMs:F0}ms > {dataset.Thresholds.MaximumAverageLatencyMs:F0}ms");

        report.PassesThresholds = !failures.Any();
        report.FailureReasons = failures;

        return report;
    }
}
```

---

## Integration Points

### 1. RAG Service Integration

**New Method**: `IRagService.AskWithCustomPromptAsync(gameId, query, customPrompt, ct)`

This method allows evaluation service to inject custom prompt (version under test) without modifying active prompt in cache.

```csharp
// In RagService.cs
public async Task<QaResponse> AskWithCustomPromptAsync(
    string gameId,
    string query,
    string customSystemPrompt,
    CancellationToken ct)
{
    // Same logic as AskAsync() but uses customSystemPrompt instead of fetching from DB
    // Used exclusively by evaluation framework
}
```

### 2. Admin API Endpoints

```csharp
// POST /api/v1/admin/prompts/{templateId}/versions/{versionId}/evaluate
// Accepts: { "datasetPath": "qa-system-prompt-test-dataset.json" }
// Returns: EvaluationReport

// POST /api/v1/admin/prompts/{templateId}/compare
// Accepts: { "versionIdA": "...", "versionIdB": "...", "datasetPath": "..." }
// Returns: ComparisonReport
```

### 3. Metadata Storage

Store evaluation results in `prompt_versions.metadata` field (JSON):

```json
{
  "lastEvaluation": {
    "evaluatedAt": "2025-10-18T10:00:00Z",
    "evaluatedBy": "admin@meepleai.dev",
    "datasetVersion": "1.0",
    "passesThresholds": true,
    "metrics": {
      "accuracy": 0.92,
      "hallucinationRate": 0.04,
      "averageConfidence": 0.78,
      "citationCorrectness": 0.88,
      "averageLatencyMs": 2450
    }
  }
}
```

---

## UI Design

### 1. Evaluation Trigger UI

**Location**: `/admin/prompts/[id]/versions/[versionId]`

**UI Components**:
- "Run Tests" button (loads test dataset, triggers evaluation)
- Test dataset selector dropdown
- Loading spinner during evaluation (shows progress: "Testing query 15/50...")
- Results panel (appears after completion)

### 2. Evaluation Results Display

**Sections**:

**A. Summary Card**:
```
┌─────────────────────────────────────────────────────┐
│ Evaluation Results                                  │
│ Version: 5 | Evaluated: 2025-10-18 10:00 | Pass ✓ │
├─────────────────────────────────────────────────────┤
│ Accuracy:             92% ✓ (threshold: 80%)       │
│ Hallucination Rate:    4% ✓ (threshold: <10%)      │
│ Avg Confidence:     0.78 ✓ (threshold: 0.70)       │
│ Citation Correctness: 88%                           │
│ Avg Latency:       2,450ms ✓ (threshold: 3,000ms)  │
└─────────────────────────────────────────────────────┘
```

**B. Category Breakdown**:
```
Setup Queries:     8/10 correct (80%)
Gameplay Queries: 15/20 correct (75%)
Edge Cases:        4/6 correct (67%)
Out-of-Context:    4/4 correct (100%)
```

**C. Query-by-Query Results (Expandable)**:
```
┌─────────────────────────────────────────────────────┐
│ ✓ qa-001 | How many players can play Tic-Tac-Toe? │
│   Response: "2 players"                             │
│   Confidence: 0.95 | Latency: 1,200ms              │
├─────────────────────────────────────────────────────┤
│ ✗ qa-002 | Can a pawn move backwards?              │
│   Response: "Yes, in special cases"  ← INCORRECT   │
│   Expected: "No, pawns cannot move backwards"      │
│   Confidence: 0.62 | Latency: 1,850ms              │
│   Issue: Hallucination detected                     │
└─────────────────────────────────────────────────────┘
```

**D. Actions**:
- "Download Report (JSON)" button
- "Download Report (Markdown)" button
- "Re-run Tests" button
- "Activate Version" button (enabled only if passes thresholds)

### 3. A/B Comparison UI

**Location**: `/admin/prompts/[id]/compare?versionA=v3&versionB=v5`

**Layout**: Side-by-side comparison

```
┌────────────────────────┬────────────────────────┐
│ Version 3              │ Version 5              │
├────────────────────────┼────────────────────────┤
│ Accuracy: 85%          │ Accuracy: 92% ↑        │
│ Hallucination: 8%      │ Hallucination: 4% ↓    │
│ Confidence: 0.75       │ Confidence: 0.78 ↑     │
│ Latency: 2,300ms       │ Latency: 2,450ms ↑     │
├────────────────────────┴────────────────────────┤
│ Recommendation: Version 5 is better              │
│ - Improved on 7 queries                          │
│ - Regressed on 2 queries                         │
│ - Slight latency increase acceptable             │
└──────────────────────────────────────────────────┘
```

---

## CI/CD Integration

### GitHub Actions Workflow

**File**: `.github/workflows/prompt-evaluation.yml`

```yaml
name: Prompt Evaluation

on:
  pull_request:
    paths:
      - 'apps/api/src/Api/Migrations/Seeds/SeedPromptTemplates.sql'
      - 'tests/Api.Tests/TestData/prompt-evaluation/**'

jobs:
  evaluate-prompts:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_PASSWORD: postgres
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
      qdrant:
        image: qdrant/qdrant:v1.12.5
        ports:
          - 6333:6333

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore apps/api/src/Api/Api.csproj

      - name: Build API
        run: dotnet build apps/api/src/Api/Api.csproj --no-restore

      - name: Run prompt evaluations
        run: |
          dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj \
            --filter "Category=PromptEvaluation" \
            --logger "console;verbosity=detailed"
        env:
          CI: true
          ConnectionStrings__Postgres: "Host=postgres;Database=meepleai_test;Username=postgres;Password=postgres"
          ConnectionStrings__Qdrant: "http://qdrant:6333"
          OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }}

      - name: Upload evaluation reports
        uses: actions/upload-artifact@v4
        with:
          name: prompt-evaluation-reports
          path: apps/api/tests/Api.Tests/TestResults/PromptEvaluationReports/
          retention-days: 30

      - name: Comment PR with results
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const report = fs.readFileSync(
              'apps/api/tests/Api.Tests/TestResults/PromptEvaluationReports/summary.md',
              'utf8'
            );
            github.rest.issues.createComment({
              issue_number: context.issue.number,
              owner: context.repo.owner,
              repo: context.repo.repo,
              body: `## Prompt Evaluation Results\n\n${report}`
            });
```

### Test Implementation

```csharp
[Fact]
[Trait("Category", "PromptEvaluation")]
public async Task QaSystemPrompt_ShouldPassEvaluationThresholds()
{
    // Arrange
    var dataset = await _evaluationService.LoadDatasetAsync(
        "TestData/prompt-evaluation/qa-system-prompt-test-dataset.json");

    var template = await _context.PromptTemplates
        .Include(t => t.Versions.Where(v => v.IsActive))
        .FirstAsync(t => t.Name == "qa-system-prompt");

    var activeVersion = template.Versions.First();

    // Act
    var report = await _evaluationService.EvaluateAsync(
        template.Id,
        activeVersion.Id,
        dataset);

    // Assert
    Assert.True(report.PassesThresholds, string.Join(", ", report.FailureReasons));
    Assert.True(report.Accuracy >= 0.80);
    Assert.True(report.HallucinationRate <= 0.10);
    Assert.True(report.AverageConfidence >= 0.70);
    Assert.True(report.AverageLatencyMs <= 3000);

    // Output report for CI artifacts
    var markdownReport = _evaluationService.GenerateMarkdownReport(report);
    await File.WriteAllTextAsync("TestResults/PromptEvaluationReports/qa-system-prompt.md", markdownReport);
}
```

---

## Future Enhancements (Phase 2)

1. **Real-time A/B Testing**: Split production traffic between two versions (10%/90%) to measure real-world performance
2. **LLM-as-Judge**: Use GPT-4 to evaluate answer quality (semantic similarity to ground truth)
3. **Human-in-the-Loop**: Allow admins to manually review and rate failing queries
4. **Automatic Regression Detection**: Weekly batch job compares latest evaluation to historical baseline
5. **Dataset Versioning**: Track changes to test datasets over time
6. **Multi-Agent Comparison**: Compare prompts across different agent types

---

## Summary

The Prompt Testing Framework provides comprehensive quality assurance for LLM prompts:

- **Automated Testing**: Run 50+ queries in < 5 minutes
- **5 Core Metrics**: Accuracy, hallucination rate, confidence, citations, latency
- **Pass/Fail Gates**: Configurable thresholds prevent bad prompts from activation
- **A/B Testing**: Side-by-side comparison for data-driven decisions
- **CI/CD Integration**: Automated testing on prompt migrations
- **Audit Trail**: Store test results in version metadata

**Implementation Effort**: 1-2 weeks (10-15 developer days)

**Risk Mitigation**: Prevents 90%+ of prompt quality regressions before reaching production

---

**Next Steps**:
1. Review and approve this design
2. Create initial test datasets (4 prompts × 50 queries = 200 total)
3. Implement `IPromptEvaluationService` and metrics
4. Build admin UI for running tests
5. Integrate with CI/CD pipeline

