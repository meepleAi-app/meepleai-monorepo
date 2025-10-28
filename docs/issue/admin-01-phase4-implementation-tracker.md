# ADMIN-01 Phase 4: Prompt Testing Framework - Implementation Tracker

**Epic**: Admin-Configurable Prompt Management System
**Phase**: 4 of 5 - Prompt Testing Framework
**Status**: 🟡 IN PROGRESS (30% Complete)
**Branch**: `feature/admin-01-phase4-testing-framework`
**Tracking Issue**: ADMIN-01 Phase 4
**Estimated Total Effort**: 80 hours (Week 4-5)
**Completed**: 24 hours | **Remaining**: 56 hours

---

## Overview

Implement comprehensive automated testing framework for evaluating prompt quality before activation. Enables data-driven prompt optimization with 5 core metrics, A/B testing, and historical tracking.

---

## ✅ Part 1: Foundation (COMPLETED - 24 hours)

**Status**: ✅ Merged in commit `8eb1af5`
**Files Created**: 6 files, 821 lines of code
**Branch**: `feature/admin-01-phase4-testing-framework`

### Completed Components

- [x] **IPromptEvaluationService.cs** (7 methods)
  - LoadDatasetAsync
  - EvaluateAsync
  - CompareVersionsAsync
  - GenerateReport
  - StoreResultsAsync
  - GetHistoricalResultsAsync

- [x] **PromptEvaluationDto.cs** (10+ models)
  - PromptTestDataset
  - PromptTestCase
  - QualityThresholds
  - PromptEvaluationResult
  - EvaluationMetrics
  - QueryEvaluationResult
  - PromptComparisonResult
  - MetricDeltas
  - ComparisonRecommendation enum
  - ReportFormat enum

- [x] **prompt-evaluation-dataset.schema.json**
  - JSON Schema for test dataset validation
  - Enforces structure for test cases, thresholds, metadata

- [x] **IRagService Extension**
  - Added `AskWithCustomPromptAsync` method signature

- [x] **RagService Implementation**
  - Implemented `AskWithCustomPromptAsync` (~160 lines)
  - Hybrid search integration
  - Custom prompt injection
  - Metrics tracking

- [x] **PromptEvaluationResultEntity.cs**
  - Database entity for `prompt_evaluation_results` table
  - Fields for all 5 metrics + metadata
  - JSONB field for detailed query results

---

## 🟡 Part 2: Backend Services (IN PROGRESS - 40 hours)

**Estimated Completion**: 3-4 days
**Priority**: HIGH
**Depends On**: Part 1 (completed)

### Tasks

#### 2.1 Database Migration (4 hours)

- [ ] **Create Migration** (`apps/api/src/Api/Migrations/`)
  ```bash
  cd apps/api/src/Api
  dotnet ef migrations add AddPromptEvaluationResults
  ```

- [ ] **Migration Content**:
  - Create `prompt_evaluation_results` table
  - Indexes on: `template_id`, `version_id`, `executed_at`
  - Foreign key to `prompt_templates` table
  - JSONB column for `query_results_json`

- [ ] **Update MeepleAiDbContext.cs**
  ```csharp
  public DbSet<PromptEvaluationResultEntity> PromptEvaluationResults { get; set; }
  ```

- [ ] **Test Migration**
  ```bash
  dotnet ef database update
  # Verify table created in PostgreSQL
  ```

**Acceptance Criteria**:
- Migration applies without errors
- Table schema matches entity definition
- Indexes created for performance
- Can insert/query test records

---

#### 2.2 Implement PromptEvaluationService (24 hours)

**File**: `apps/api/src/Api/Services/PromptEvaluationService.cs`
**Estimated Lines**: ~800

**Dependencies**:
```csharp
private readonly IRagService _ragService;
private readonly IPromptTemplateService _promptTemplateService;
private readonly MeepleAiDbContext _dbContext;
private readonly ILogger<PromptEvaluationService> _logger;
```

##### 2.2.1 LoadDatasetAsync (4 hours)

```csharp
public async Task<PromptTestDataset> LoadDatasetAsync(
    string datasetPath,
    CancellationToken ct = default)
{
    // 1. Read JSON file from path (support relative/absolute)
    // 2. Deserialize to PromptTestDataset
    // 3. Validate against JSON schema (optional but recommended)
    // 4. Return validated dataset
    // Error handling: FileNotFoundException, JsonException
}
```

**Implementation Steps**:
- Use `System.IO.File.ReadAllTextAsync` for file reading
- Use `System.Text.Json.JsonSerializer.Deserialize<PromptTestDataset>`
- Optional: Validate with NJsonSchema library
- Log dataset metadata (ID, test case count)

---

##### 2.2.2 EvaluateAsync (12 hours) - **CORE LOGIC**

```csharp
public async Task<PromptEvaluationResult> EvaluateAsync(
    string templateId,
    string versionId,
    string datasetPath,
    Action<int, int>? progressCallback = null,
    CancellationToken ct = default)
{
    // 1. Load dataset
    // 2. Get prompt content from database (version_id)
    // 3. For each test case:
    //    - Execute query with custom prompt via IRagService
    //    - Measure latency with Stopwatch
    //    - Calculate metrics for this query
    // 4. Aggregate metrics across all queries
    // 5. Determine pass/fail based on thresholds
    // 6. Return PromptEvaluationResult
}
```

**5 Metrics Calculation Logic**:

**Metric 1: Accuracy** (keyword matching)
```csharp
private bool CalculateAccuracy(QueryEvaluationResult queryResult, PromptTestCase testCase)
{
    if (testCase.RequiredKeywords == null || testCase.RequiredKeywords.Count == 0)
        return true; // No keywords required, pass by default

    var response = queryResult.Response.ToLowerInvariant();
    return testCase.RequiredKeywords.All(keyword =>
        response.Contains(keyword.ToLowerInvariant()));
}

// Aggregate: (accurate_count / total_queries) * 100
```

**Metric 2: Hallucination Rate** (forbidden keywords)
```csharp
private bool DetectHallucination(QueryEvaluationResult queryResult, PromptTestCase testCase)
{
    if (testCase.ForbiddenKeywords == null || testCase.ForbiddenKeywords.Count == 0)
        return false; // No forbidden keywords, not hallucinated

    var response = queryResult.Response.ToLowerInvariant();
    return testCase.ForbiddenKeywords.Any(keyword =>
        response.Contains(keyword.ToLowerInvariant()));
}

// Aggregate: (hallucinated_count / total_queries) * 100
```

**Metric 3: Average Confidence** (from RAG response)
```csharp
// Get from QaResponse.Confidence (already calculated by RAG)
queryResult.Confidence = qaResponse.Confidence ?? 0.0;

// Aggregate: Sum(confidence) / total_queries
```

**Metric 4: Citation Correctness** (parse citations)
```csharp
private bool ValidateCitations(QueryEvaluationResult queryResult, PromptTestCase testCase)
{
    if (testCase.ExpectedCitations == null || testCase.ExpectedCitations.Count == 0)
        return true; // No expected citations, pass by default

    // Parse citations from response (regex for "Page X", "p. X", etc.)
    var citationPattern = @"(?:page|p\.?)\s*(\d+)";
    var matches = Regex.Matches(queryResult.Response, citationPattern, RegexOptions.IgnoreCase);

    var foundCitations = matches.Select(m => m.Groups[1].Value).ToList();

    // Check if any expected citation is found
    return testCase.ExpectedCitations.Any(expected =>
        foundCitations.Contains(expected));
}

// Aggregate: (correct_citations / queries_with_expected_citations) * 100
```

**Metric 5: Average Latency** (stopwatch measurement)
```csharp
var stopwatch = Stopwatch.StartNew();
var qaResponse = await _ragService.AskWithCustomPromptAsync(/*...*/);
stopwatch.Stop();

queryResult.LatencyMs = (int)stopwatch.ElapsedMilliseconds;

// Aggregate: Sum(latency_ms) / total_queries
```

**Implementation Steps**:
1. Load dataset with LoadDatasetAsync
2. Query database for prompt content:
   ```csharp
   var version = await _dbContext.PromptVersions
       .FirstOrDefaultAsync(v => v.Id == versionId, ct);
   if (version == null) throw new ArgumentException($"Version {versionId} not found");
   var customPrompt = version.Content;
   ```
3. Initialize result tracking lists
4. Loop through test cases with progress callback
5. Calculate each metric per query
6. Aggregate metrics
7. Determine pass/fail:
   ```csharp
   var passed = metrics.Accuracy >= thresholds.MinAccuracy &&
                metrics.HallucinationRate <= thresholds.MaxHallucinationRate &&
                metrics.AvgConfidence >= thresholds.MinAvgConfidence &&
                metrics.CitationCorrectness >= thresholds.MinCitationCorrectness &&
                metrics.AvgLatencyMs <= thresholds.MaxAvgLatencyMs;
   ```

---

##### 2.2.3 CompareVersionsAsync (4 hours)

```csharp
public async Task<PromptComparisonResult> CompareVersionsAsync(
    string templateId,
    string baselineVersionId,
    string candidateVersionId,
    string datasetPath,
    CancellationToken ct = default)
{
    // 1. Run EvaluateAsync on baseline version
    // 2. Run EvaluateAsync on candidate version
    // 3. Calculate deltas for all 5 metrics
    // 4. Generate recommendation (Activate, Reject, ManualReview)
    // 5. Return PromptComparisonResult
}
```

**Recommendation Algorithm**:
```csharp
private ComparisonRecommendation GenerateRecommendation(MetricDeltas deltas)
{
    // REJECT if candidate fails any threshold
    if (!candidateResult.Passed)
        return ComparisonRecommendation.Reject;

    // REJECT if significant regression
    if (deltas.AccuracyDelta <= -10.0 || deltas.HallucinationRateDelta >= 15.0)
        return ComparisonRecommendation.Reject;

    // ACTIVATE if meaningful improvement
    if (deltas.AccuracyDelta >= 5.0 ||
        deltas.HallucinationRateDelta <= -5.0 ||
        deltas.AvgConfidenceDelta >= 0.10)
        return ComparisonRecommendation.Activate;

    // Otherwise require manual review
    return ComparisonRecommendation.ManualReview;
}
```

---

##### 2.2.4 GenerateReport (2 hours)

```csharp
public string GenerateReport(
    PromptEvaluationResult result,
    ReportFormat format = ReportFormat.Markdown)
{
    return format switch
    {
        ReportFormat.Markdown => GenerateMarkdownReport(result),
        ReportFormat.Json => JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
        _ => throw new ArgumentException($"Unsupported format: {format}")
    };
}
```

**Markdown Template**:
```markdown
# Prompt Evaluation Report

**Evaluation ID**: {result.EvaluationId}
**Template**: {result.TemplateId}
**Version**: {result.VersionId}
**Dataset**: {result.DatasetId}
**Executed**: {result.ExecutedAt:yyyy-MM-dd HH:mm:ss UTC}
**Status**: {(result.Passed ? "✅ PASSED" : "❌ FAILED")}

## Metrics Summary

| Metric | Value | Threshold | Status |
|--------|-------|-----------|--------|
| Accuracy | {metrics.Accuracy:F1}% | ≥ {thresholds.MinAccuracy*100:F0}% | {status} |
| Hallucination Rate | {metrics.HallucinationRate:F1}% | ≤ {thresholds.MaxHallucinationRate*100:F0}% | {status} |
| Avg Confidence | {metrics.AvgConfidence:F2} | ≥ {thresholds.MinAvgConfidence:F2} | {status} |
| Citation Correctness | {metrics.CitationCorrectness:F1}% | ≥ {thresholds.MinCitationCorrectness*100:F0}% | {status} |
| Avg Latency | {metrics.AvgLatencyMs:F0}ms | ≤ {thresholds.MaxAvgLatencyMs}ms | {status} |

## Query Breakdown

{foreach query in queryResults}
### Query {index}: {query.TestCaseId}

**Query**: {query.Query}
**Response**: {query.Response}
**Confidence**: {query.Confidence:F2}
**Latency**: {query.LatencyMs}ms
**Accurate**: {query.IsAccurate ? "✅" : "❌"}
**Hallucinated**: {query.IsHallucinated ? "⚠️ YES" : "✅ NO"}
**Citations Correct**: {query.AreCitationsCorrect ? "✅" : "❌"}

---
{end foreach}

## Summary

{result.Summary}
```

---

##### 2.2.5 StoreResultsAsync & GetHistoricalResultsAsync (2 hours)

```csharp
public async Task StoreResultsAsync(
    PromptEvaluationResult result,
    CancellationToken ct = default)
{
    var entity = new PromptEvaluationResultEntity
    {
        Id = result.EvaluationId,
        TemplateId = result.TemplateId,
        VersionId = result.VersionId,
        DatasetId = result.DatasetId,
        ExecutedAt = result.ExecutedAt,
        TotalQueries = result.TotalQueries,
        Accuracy = result.Metrics.Accuracy,
        HallucinationRate = result.Metrics.HallucinationRate,
        AvgConfidence = result.Metrics.AvgConfidence,
        CitationCorrectness = result.Metrics.CitationCorrectness,
        AvgLatencyMs = result.Metrics.AvgLatencyMs,
        Passed = result.Passed,
        Summary = result.Summary,
        QueryResultsJson = JsonSerializer.Serialize(result.QueryResults),
        CreatedAt = DateTime.UtcNow
    };

    _dbContext.PromptEvaluationResults.Add(entity);
    await _dbContext.SaveChangesAsync(ct);
}

public async Task<List<PromptEvaluationResult>> GetHistoricalResultsAsync(
    string templateId,
    int limit = 10,
    CancellationToken ct = default)
{
    var entities = await _dbContext.PromptEvaluationResults
        .Where(e => e.TemplateId == templateId)
        .OrderByDescending(e => e.ExecutedAt)
        .Take(limit)
        .ToListAsync(ct);

    return entities.Select(e => new PromptEvaluationResult
    {
        EvaluationId = e.Id,
        TemplateId = e.TemplateId,
        VersionId = e.VersionId,
        DatasetId = e.DatasetId,
        ExecutedAt = e.ExecutedAt,
        TotalQueries = e.TotalQueries,
        Metrics = new EvaluationMetrics
        {
            Accuracy = e.Accuracy,
            HallucinationRate = e.HallucinationRate,
            AvgConfidence = e.AvgConfidence,
            CitationCorrectness = e.CitationCorrectness,
            AvgLatencyMs = e.AvgLatencyMs
        },
        Passed = e.Passed,
        Summary = e.Summary,
        QueryResults = string.IsNullOrEmpty(e.QueryResultsJson)
            ? new List<QueryEvaluationResult>()
            : JsonSerializer.Deserialize<List<QueryEvaluationResult>>(e.QueryResultsJson) ?? new List<QueryEvaluationResult>()
    }).ToList();
}
```

**Acceptance Criteria for PromptEvaluationService**:
- All 7 interface methods implemented
- 5 metrics calculated correctly
- A/B comparison logic works as specified
- Reports generated in both Markdown and JSON
- Database persistence functional
- Comprehensive error handling
- Logging for debugging
- ~90% code coverage achievable with unit tests

---

#### 2.3 Register Services in DI (1 hour)

**File**: `apps/api/src/Api/Program.cs`

**Location**: After line ~100 (service registrations section)

```csharp
// ADMIN-01 Phase 4: Prompt Evaluation Service
builder.Services.AddScoped<IPromptEvaluationService, PromptEvaluationService>();
```

**Verify**:
```bash
cd apps/api/src/Api
dotnet build
# Should compile without errors
```

---

#### 2.4 Add Admin API Endpoints (8 hours)

**File**: `apps/api/src/Api/Program.cs`

**Location**: In `v1Api` group (after line 4400+)

**Endpoints to Add**:

```csharp
// ADMIN-01 Phase 4: Prompt Evaluation Endpoints

// Run evaluation on a specific prompt version
v1Api.MapPost("/admin/prompts/{templateId}/versions/{versionId}/evaluate",
    async (
        string templateId,
        string versionId,
        EvaluatePromptRequest request,
        IPromptEvaluationService evaluationService,
        HttpContext context,
        CancellationToken ct) =>
{
    // Authorization: Admin only
    if (!context.User.IsInRole("admin"))
        return Results.Forbid();

    try
    {
        var result = await evaluationService.EvaluateAsync(
            templateId,
            versionId,
            request.DatasetPath,
            progressCallback: null, // TODO: Add SSE support for progress
            ct);

        // Optionally store results
        if (request.StoreResults)
        {
            await evaluationService.StoreResultsAsync(result, ct);
        }

        return Results.Ok(result);
    }
    catch (FileNotFoundException ex)
    {
        return Results.NotFound(new { error = $"Dataset not found: {ex.Message}" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Evaluation failed: {ex.Message}");
    }
})
.WithName("EvaluatePromptVersion")
.WithTags("Admin - Prompts")
.RequireAuthorization()
.Produces<PromptEvaluationResult>(200)
.Produces(403)
.Produces(404)
.Produces(500);

// Compare two prompt versions (A/B testing)
v1Api.MapPost("/admin/prompts/{templateId}/compare",
    async (
        string templateId,
        ComparePromptsRequest request,
        IPromptEvaluationService evaluationService,
        HttpContext context,
        CancellationToken ct) =>
{
    if (!context.User.IsInRole("admin"))
        return Results.Forbid();

    try
    {
        var comparison = await evaluationService.CompareVersionsAsync(
            templateId,
            request.BaselineVersionId,
            request.CandidateVersionId,
            request.DatasetPath,
            ct);

        return Results.Ok(comparison);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Comparison failed: {ex.Message}");
    }
})
.WithName("ComparePromptVersions")
.WithTags("Admin - Prompts")
.RequireAuthorization()
.Produces<PromptComparisonResult>(200);

// Get historical evaluation results
v1Api.MapGet("/admin/prompts/{templateId}/evaluations",
    async (
        string templateId,
        int limit,
        IPromptEvaluationService evaluationService,
        HttpContext context,
        CancellationToken ct) =>
{
    if (!context.User.IsInRole("admin"))
        return Results.Forbid();

    var results = await evaluationService.GetHistoricalResultsAsync(templateId, limit, ct);
    return Results.Ok(results);
})
.WithName("GetEvaluationHistory")
.WithTags("Admin - Prompts")
.RequireAuthorization()
.Produces<List<PromptEvaluationResult>>(200);

// Generate report for an evaluation
v1Api.MapGet("/admin/prompts/evaluations/{evaluationId}/report",
    async (
        string evaluationId,
        string format, // "markdown" or "json"
        IPromptEvaluationService evaluationService,
        HttpContext context,
        CancellationToken ct) =>
{
    if (!context.User.IsInRole("admin"))
        return Results.Forbid();

    // Get evaluation from database
    var results = await evaluationService.GetHistoricalResultsAsync("", 1000, ct);
    var result = results.FirstOrDefault(r => r.EvaluationId == evaluationId);

    if (result == null)
        return Results.NotFound();

    var reportFormat = format?.ToLowerInvariant() == "json"
        ? ReportFormat.Json
        : ReportFormat.Markdown;

    var report = evaluationService.GenerateReport(result, reportFormat);

    var contentType = reportFormat == ReportFormat.Json
        ? "application/json"
        : "text/markdown";

    return Results.Content(report, contentType);
})
.WithName("GetEvaluationReport")
.WithTags("Admin - Prompts")
.RequireAuthorization()
.Produces<string>(200);
```

**DTOs for Requests**:
```csharp
// Add to PromptEvaluationDto.cs

public class EvaluatePromptRequest
{
    public required string DatasetPath { get; set; }
    public bool StoreResults { get; set; } = true;
}

public class ComparePromptsRequest
{
    public required string BaselineVersionId { get; set; }
    public required string CandidateVersionId { get; set; }
    public required string DatasetPath { get; set; }
}
```

**Testing Endpoints**:
```bash
# Evaluate a prompt version
curl -X POST http://localhost:8080/api/v1/admin/prompts/{templateId}/versions/{versionId}/evaluate \
  -H "Content-Type: application/json" \
  -d '{"datasetPath": "tests/Api.Tests/TestData/qa-test-dataset.json", "storeResults": true}'

# Compare two versions
curl -X POST http://localhost:8080/api/v1/admin/prompts/{templateId}/compare \
  -H "Content-Type: application/json" \
  -d '{"baselineVersionId": "v1", "candidateVersionId": "v2", "datasetPath": "..."}'

# Get historical results
curl http://localhost:8080/api/v1/admin/prompts/{templateId}/evaluations?limit=10

# Get report
curl http://localhost:8080/api/v1/admin/prompts/evaluations/{evaluationId}/report?format=markdown
```

---

#### 2.5 Integration Tests (3 hours)

**File**: `apps/api/tests/Api.Tests/Integration/PromptEvaluationIntegrationTests.cs`

**Test Count**: 5+ tests

**Test Scenarios**:

```csharp
public class PromptEvaluationIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory;
    private PostgreSqlContainer _postgresContainer;
    private QdrantContainer _qdrantContainer;

    [Fact]
    public async Task EvaluateAsync_WithValidDataset_ReturnsCorrectMetrics()
    {
        // Arrange: Create test dataset with 5 queries
        // Act: Call EvaluateAsync
        // Assert: Verify 5 metrics calculated, passed/failed correct
    }

    [Fact]
    public async Task CompareVersionsAsync_CandidateBetter_RecommendsActivate()
    {
        // Arrange: Two versions, dataset
        // Act: Call CompareVersionsAsync
        // Assert: Recommendation = Activate, deltas positive
    }

    [Fact]
    public async Task StoreResultsAsync_SavesToDatabaseCorrectly()
    {
        // Arrange: PromptEvaluationResult
        // Act: Call StoreResultsAsync
        // Assert: Query database, verify stored
    }

    [Fact]
    public async Task GetHistoricalResultsAsync_ReturnsOrderedByDate()
    {
        // Arrange: Insert 3 evaluation results
        // Act: Call GetHistoricalResultsAsync
        // Assert: Returns 3, ordered by ExecutedAt DESC
    }

    [Fact]
    public async Task EvaluateAsync_WithMissingDataset_ThrowsFileNotFoundException()
    {
        // Arrange: Invalid path
        // Act & Assert: Expect FileNotFoundException
    }
}
```

---

### Part 2 Acceptance Criteria

- [ ] Migration created and applied successfully
- [ ] `PromptEvaluationService` fully implemented (~800 lines)
- [ ] All 7 interface methods working
- [ ] 5 metrics calculated correctly
- [ ] A/B comparison logic functional
- [ ] Reports generated (Markdown + JSON)
- [ ] Service registered in DI
- [ ] 4 admin API endpoints added
- [ ] 5+ integration tests passing
- [ ] Tests run in CI with Testcontainers
- [ ] Code coverage > 90% for service

---

## ⏳ Part 3: Datasets, Tests & UI (PENDING - 32 hours)

**Estimated Completion**: 3-4 days
**Priority**: MEDIUM
**Depends On**: Part 2 (completed)

### Tasks

#### 3.1 Create Test Datasets (8 hours)

**Location**: `apps/api/tests/Api.Tests/TestData/prompt-evaluation/`

**Files to Create**:

1. **qa-system-prompt-test-dataset.json** (50+ test cases)
   - Categories: setup, gameplay, edge-case, out-of-context
   - Difficulty: easy (20), medium (20), hard (10)
   - Example test cases:
     ```json
     {
       "dataset_id": "qa-prompt-test-v1",
       "template_name": "qa-system-prompt",
       "version": "1.0.0",
       "description": "Comprehensive test dataset for Q&A prompt evaluation",
       "test_cases": [
         {
           "id": "TC-001",
           "category": "setup",
           "difficulty": "easy",
           "query": "How many players can play Tic-Tac-Toe?",
           "game_id": "tic-tac-toe-game-id",
           "expected_answer": "2 players",
           "required_keywords": ["2", "two", "players"],
           "forbidden_keywords": ["3", "three", "four"],
           "expected_citations": ["1"],
           "min_confidence": 0.80,
           "max_latency_ms": 3000
         },
         // ... 49 more test cases
       ],
       "quality_thresholds": {
         "min_accuracy": 0.85,
         "max_hallucination_rate": 0.08,
         "min_avg_confidence": 0.75,
         "min_citation_correctness": 0.85,
         "max_avg_latency_ms": 2500
       }
     }
     ```

2. **chess-system-prompt-test-dataset.json** (30+ test cases)
   - Focus on chess-specific rules, positions, strategies
   - Include FEN notation examples
   - Test dynamic prompt building

3. **setup-guide-system-prompt-test-dataset.json** (30+ test cases)
   - Focus on game setup instructions
   - Test RAG-powered setup generation

4. **streaming-qa-system-prompt-test-dataset.json** (30+ test cases)
   - Similar to QA but for streaming variant
   - Test token-by-token generation quality

**Dataset Design Principles**:
- Cover all difficulty levels (easy, medium, hard)
- Include positive cases (should answer correctly)
- Include negative cases (should say "Not specified")
- Include hallucination detection cases (forbidden keywords)
- Include citation validation cases
- Realistic queries from actual users

**Acceptance Criteria**:
- 4 datasets created (140+ total test cases)
- All validate against JSON schema
- Cover all categories and difficulty levels
- Include edge cases and error scenarios
- Balanced distribution across categories

---

#### 3.2 Unit Tests for PromptEvaluationService (12 hours)

**File**: `apps/api/tests/Api.Tests/Services/PromptEvaluationServiceTests.cs`

**Test Count**: 15+ tests
**Coverage Target**: 90%+

**Test Scenarios**:

```csharp
public class PromptEvaluationServiceTests
{
    // LoadDatasetAsync tests (3 tests)
    [Fact] public async Task LoadDatasetAsync_ValidPath_LoadsCorrectly() { }
    [Fact] public async Task LoadDatasetAsync_InvalidPath_ThrowsFileNotFoundException() { }
    [Fact] public async Task LoadDatasetAsync_MalformedJson_ThrowsJsonException() { }

    // EvaluateAsync tests (5 tests)
    [Fact] public async Task EvaluateAsync_AllAccurate_Returns100PercentAccuracy() { }
    [Fact] public async Task EvaluateAsync_WithHallucinations_CalculatesCorrectRate() { }
    [Fact] public async Task EvaluateAsync_BelowThresholds_ReturnsFailed() { }
    [Fact] public async Task EvaluateAsync_AboveThresholds_ReturnsPassed() { }
    [Fact] public async Task EvaluateAsync_CallsProgressCallback_WithCorrectCounts() { }

    // CompareVersionsAsync tests (3 tests)
    [Fact] public async Task CompareVersionsAsync_CandidateBetter_ReturnsActivate() { }
    [Fact] public async Task CompareVersionsAsync_CandidateWorse_ReturnsReject() { }
    [Fact] public async Task CompareVersionsAsync_MixedResults_ReturnsManualReview() { }

    // GenerateReport tests (2 tests)
    [Fact] public void GenerateReport_MarkdownFormat_ReturnsValidMarkdown() { }
    [Fact] public void GenerateReport_JsonFormat_ReturnsValidJson() { }

    // StoreResultsAsync & GetHistoricalResultsAsync tests (2 tests)
    [Fact] public async Task StoreResultsAsync_ValidResult_SavesCorrectly() { }
    [Fact] public async Task GetHistoricalResultsAsync_MultipleResults_ReturnsOrderedByDate() { }
}
```

**Mocking Strategy**:
- Mock `IRagService.AskWithCustomPromptAsync` to return controlled responses
- Mock `IPromptTemplateService` for version lookups
- Use in-memory SQLite for `MeepleAiDbContext`
- Use `ILogger<T>` mock for logging verification

---

#### 3.3 Admin UI - Evaluation Results Page (8 hours)

**File**: `apps/web/src/pages/admin/prompts/[id]/versions/[versionId]/evaluate.tsx`

**Features**:
1. Dataset selector dropdown (list available datasets)
2. "Run Evaluation" button
3. Progress indicator (X/N queries complete)
4. Real-time results display (optional: SSE/polling)
5. Metrics visualization:
   - 5 gauges for each metric (with red/green zones)
   - Overall pass/fail badge
6. Query breakdown table:
   - Expandable rows showing query, response, metrics
   - Color-coded pass/fail indicators
7. Download report buttons (Markdown, JSON)
8. "Activate Version" button (if passed)

**UI Components**:
```tsx
export default function EvaluatePromptPage() {
  const router = useRouter();
  const { id: templateId, versionId } = router.query;
  const [evaluationResult, setEvaluationResult] = useState<PromptEvaluationResult | null>(null);
  const [isRunning, setIsRunning] = useState(false);
  const [progress, setProgress] = useState({ current: 0, total: 0 });

  const runEvaluation = async (datasetPath: string) => {
    setIsRunning(true);
    try {
      const response = await api.post(
        `/admin/prompts/${templateId}/versions/${versionId}/evaluate`,
        { datasetPath, storeResults: true }
      );
      setEvaluationResult(response.data);
    } catch (error) {
      // Error handling
    } finally {
      setIsRunning(false);
    }
  };

  return (
    <div>
      <h1>Evaluate Prompt Version</h1>

      {/* Dataset selector */}
      <DatasetSelector onSelect={runEvaluation} disabled={isRunning} />

      {/* Progress indicator */}
      {isRunning && <ProgressBar current={progress.current} total={progress.total} />}

      {/* Results display */}
      {evaluationResult && (
        <>
          <MetricsDashboard metrics={evaluationResult.metrics} passed={evaluationResult.passed} />
          <QueryBreakdownTable queries={evaluationResult.queryResults} />
          <ReportDownloadButtons evaluationId={evaluationResult.evaluationId} />
        </>
      )}
    </div>
  );
}
```

**Acceptance Criteria**:
- Page loads without errors
- Can trigger evaluation via API
- Displays metrics with visualizations
- Shows query breakdown
- Download reports functional
- Responsive design
- Accessible (ARIA labels)

---

#### 3.4 Admin UI - A/B Comparison Page (4 hours)

**File**: `apps/web/src/pages/admin/prompts/[id]/compare-evaluation.tsx`

**Features**:
1. Version selectors: Baseline vs. Candidate
2. Dataset selector
3. "Run Comparison" button
4. Side-by-side metrics comparison:
   - Two columns: Baseline | Candidate
   - Delta indicators (↑ ↓ with colors)
5. Recommendation display (Activate / Reject / Manual Review)
6. Reasoning explanation
7. Query-by-query comparison (optional)

**UI Layout**:
```
+--------------------------------------------------+
| Compare Prompt Versions                          |
+--------------------------------------------------+
| Baseline: [v1.0 ▼]    Candidate: [v2.0 ▼]       |
| Dataset: [qa-test-v1 ▼]          [Run Comparison]|
+--------------------------------------------------+
| Metric            | Baseline | Candidate | Delta |
|-------------------|----------|-----------|-------|
| Accuracy          | 85.0%    | 92.0%     | +7.0% ↑ (green) |
| Hallucination     | 8.0%     | 3.0%      | -5.0% ↓ (green) |
| Avg Confidence    | 0.75     | 0.82      | +0.07 ↑ (green) |
| Citation Correct. | 80.0%    | 85.0%     | +5.0% ↑ (green) |
| Avg Latency       | 2500ms   | 2200ms    | -300ms ↓ (green)|
+--------------------------------------------------+
| Recommendation: ✅ ACTIVATE                      |
| Reasoning: Candidate shows significant           |
| improvements across all metrics with no          |
| regressions. Recommend activation.               |
+--------------------------------------------------+
```

---

### Part 3 Acceptance Criteria

- [ ] 4 test datasets created (140+ test cases)
- [ ] Datasets validate against JSON schema
- [ ] 15+ unit tests written and passing
- [ ] Test coverage > 90% for PromptEvaluationService
- [ ] Evaluation results UI page functional
- [ ] A/B comparison UI page functional
- [ ] Jest tests for UI components (90% coverage)
- [ ] Playwright E2E tests for evaluation flow

---

## 🚀 Deployment Checklist

**Before Merging to Main**:

- [ ] All Part 2 tasks completed
- [ ] All Part 3 tasks completed
- [ ] Backend tests passing (unit + integration)
- [ ] Frontend tests passing (Jest + Playwright)
- [ ] Code coverage > 90% (backend + frontend)
- [ ] Code review approved by lead engineer
- [ ] Security review approved (prompt injection risks)
- [ ] Documentation updated:
  - [ ] `CLAUDE.md` updated with Phase 4 completion
  - [ ] `docs/guide/admin-prompt-management-guide.md` updated
  - [ ] API documentation in Swagger
- [ ] Database migration tested in staging
- [ ] Performance tested (100 test cases < 5 minutes)
- [ ] No regressions in existing tests

**Post-Merge**:

- [ ] Deploy to staging environment
- [ ] Run full evaluation test suite
- [ ] Monitor metrics and logs
- [ ] Create sample datasets for production use
- [ ] Train admin team on UI
- [ ] Update LISTA_ISSUE.md: ADMIN-01 Phase 4 ✅ COMPLETED

---

## 📊 Success Metrics

**Functional**:
- [ ] Admin can run evaluations via UI
- [ ] All 5 metrics calculated correctly
- [ ] A/B comparisons provide actionable recommendations
- [ ] Historical tracking shows quality trends

**Performance**:
- [ ] Evaluation of 50 queries completes in < 3 minutes
- [ ] Database queries < 100ms (p95)
- [ ] API endpoints respond in < 500ms (excluding LLM calls)

**Quality**:
- [ ] Test coverage > 90% (backend + frontend)
- [ ] Zero prompt-related regressions
- [ ] All evaluation datasets validate correctly
- [ ] Reports readable and actionable

---

## 🎯 Next Steps

1. **Immediate** (Next Session):
   - Complete Part 2.1: Database migration
   - Start Part 2.2: Implement PromptEvaluationService (LoadDatasetAsync + EvaluateAsync)

2. **This Week**:
   - Finish PromptEvaluationService implementation
   - Add admin API endpoints
   - Write integration tests

3. **Next Week**:
   - Create test datasets
   - Write unit tests
   - Build admin UI pages

4. **Validation**:
   - Run full test suite
   - Code review
   - Merge to main

---

## 📚 Related Documentation

- **Implementation Checklist**: `docs/issue/admin-01-prompt-management-implementation-checklist.md`
- **Architecture**: `docs/technic/admin-prompt-management-architecture.md`
- **Testing Framework**: `docs/technic/admin-prompt-testing-framework.md`
- **LISTA_ISSUE.md**: Track overall ADMIN-01 progress

---

**Document Version**: 1.0
**Last Updated**: 2025-10-26
**Author**: Claude Code
**Status**: 🟡 IN PROGRESS

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>
