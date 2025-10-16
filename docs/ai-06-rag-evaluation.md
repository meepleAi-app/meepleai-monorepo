# AI-06: RAG Offline Evaluation System

**Status:** ✅ Implemented
**Issue:** #303
**Component:** AI/RAG
**Priority:** P1

## Overview

The RAG (Retrieval-Augmented Generation) offline evaluation system provides comprehensive metrics for measuring the performance and quality of the vector search and retrieval components. It implements standard information retrieval metrics and integrates with CI to enforce quality gates.

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────────┐
│                   RAG Evaluation System                     │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  RagEvaluationDataset (JSON)                         │  │
│  │  - queries, ground truth, relevant docs              │  │
│  └──────────────────────────────────────────────────────┘  │
│                           │                                   │
│                           ▼                                   │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  RagEvaluationService                                │  │
│  │  - LoadDatasetAsync()                                │  │
│  │  - EvaluateAsync()                                   │  │
│  │  - GenerateMarkdownReport()                          │  │
│  │  - GenerateJsonReport()                              │  │
│  └──────────────────────────────────────────────────────┘  │
│           │                │                 │               │
│           ▼                ▼                 ▼               │
│   EmbeddingService   QdrantService    Metrics Engine        │
│                                                               │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Metrics Calculated                                  │  │
│  │  - Precision@K (K=1,3,5,10)                          │  │
│  │  - Recall@K                                          │  │
│  │  - Mean Reciprocal Rank (MRR)                        │  │
│  │  - Latency (p50, p95, p99)                           │  │
│  └──────────────────────────────────────────────────────┘  │
│                           │                                   │
│                           ▼                                   │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  RagEvaluationReport                                 │  │
│  │  - Aggregate statistics                              │  │
│  │  - Per-query results                                 │  │
│  │  - Quality gate status                               │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### Files

#### Core Service
- **`src/Api/Services/RagEvaluationService.cs`**
  Main evaluation service implementing metrics calculation and report generation.

#### Models
- **`src/Api/Models/RagEvaluation.cs`**
  DTOs for evaluation: `RagEvaluationQuery`, `RagEvaluationDataset`, `RagEvaluationQueryResult`, `RagEvaluationReport`, `RagQualityThresholds`

#### Test Dataset
- **`tests/Api.Tests/TestData/rag-evaluation-dataset.json`**
  24 test queries covering Tic-Tac-Toe and Chess rules with ground truth answers and relevant document IDs.

#### Tests
- **`tests/Api.Tests/RagEvaluationServiceTests.cs`**
  20+ unit tests covering metric calculations, edge cases, report generation

- **`tests/Api.Tests/RagEvaluationIntegrationTests.cs`**
  8 integration tests with Testcontainers (Postgres + Qdrant) for end-to-end validation

#### CI Integration
- **`.github/workflows/ci.yml`**
  New `rag-evaluation` job that runs evaluation tests and enforces quality gates

## Metrics Explained

### Precision@K

**Formula:** `Precision@K = (# relevant docs in top K) / K`

**Interpretation:**
- Measures the proportion of retrieved documents that are actually relevant
- K=5 means "of the top 5 results, how many are relevant?"
- Range: 0.0 (no relevant results) to 1.0 (all results relevant)
- Higher is better

**Example:**
If query retrieves 5 documents and 4 are relevant:
`Precision@5 = 4/5 = 0.80`

### Recall@K

**Formula:** `Recall@K = (# relevant docs in top K) / (total # relevant docs)`

**Interpretation:**
- Measures how many of the total relevant documents were retrieved
- Range: 0.0 (missed all relevant) to 1.0 (found all relevant)
- Higher is better

**Example:**
If there are 10 relevant documents total and we retrieve 6 of them:
`Recall@K = 6/10 = 0.60`

### Mean Reciprocal Rank (MRR)

**Formula:** `MRR = average of (1 / rank of first relevant doc) across all queries`

**Interpretation:**
- Measures how quickly we show a relevant result
- Rank is 1-indexed (first position = rank 1)
- Range: 0.0 (no relevant results) to 1.0 (first result always relevant)
- Higher is better

**Example:**
- Query 1: First relevant doc at position 1 → RR = 1/1 = 1.0
- Query 2: First relevant doc at position 3 → RR = 1/3 = 0.333
- Query 3: No relevant docs → RR = 0.0
- MRR = (1.0 + 0.333 + 0.0) / 3 = **0.444**

### Latency Percentiles

**Definitions:**
- **p50 (median):** 50% of queries are faster than this
- **p95:** 95% of queries are faster than this
- **p99:** 99% of queries are faster than this (worst case)

**Interpretation:**
- Measured in milliseconds
- p95 is commonly used for SLA (Service Level Agreement) targets
- Lower is better

**Example:**
If latency p95 = 150ms, it means 95% of queries complete within 150ms.

## Quality Thresholds

Default thresholds enforced in CI:

| Metric | Threshold | Rationale |
|--------|-----------|-----------|
| **Precision@5** | ≥ 0.70 (70%) | At least 3.5/5 top results should be relevant |
| **Mean Reciprocal Rank** | ≥ 0.60 (60%) | First relevant result should typically appear in top 2 |
| **Latency p95** | ≤ 2000ms (2s) | 95% of queries complete within 2 seconds |
| **Success Rate** | ≥ 0.95 (95%) | At least 95% of queries succeed without errors |

### Configuring Thresholds

Thresholds can be adjusted programmatically:

```csharp
var customThresholds = new RagQualityThresholds
{
    MinPrecisionAt5 = 0.80,        // Stricter: 80%
    MinMeanReciprocalRank = 0.70,  // Stricter: 70%
    MaxLatencyP95Ms = 1000.0,      // Stricter: 1 second
    MinSuccessRate = 0.98          // Stricter: 98%
};

var report = await evaluationService.EvaluateAsync(dataset, topK: 10, customThresholds);
```

## Usage

### Running Evaluation Locally

#### Option 1: Via Integration Tests

```bash
cd apps/api

# Run all RAG evaluation tests
dotnet test --filter "FullyQualifiedName~RagEvaluation"

# Run with detailed output
dotnet test --filter "FullyQualifiedName~RagEvaluationIntegrationTests" \
  --logger "console;verbosity=detailed"
```

#### Option 2: Programmatically

```csharp
// Setup (typically in DI container)
var evaluationService = serviceProvider.GetRequiredService<IRagEvaluationService>();

// Load dataset
var datasetPath = "path/to/rag-evaluation-dataset.json";
var dataset = await evaluationService.LoadDatasetAsync(datasetPath);

// Run evaluation
var report = await evaluationService.EvaluateAsync(
    dataset,
    topK: 10,
    thresholds: new RagQualityThresholds()
);

// Generate reports
var markdownReport = evaluationService.GenerateMarkdownReport(report);
Console.WriteLine(markdownReport);

var jsonReport = evaluationService.GenerateJsonReport(report);
File.WriteAllText("evaluation-report.json", jsonReport);

// Check quality gates
if (!report.PassedQualityGates)
{
    Console.WriteLine("Quality gates FAILED:");
    foreach (var failure in report.QualityGateFailures)
    {
        Console.WriteLine($"  - {failure}");
    }
    Environment.Exit(1);
}
```

### Viewing CI Reports

1. Navigate to GitHub Actions → select a workflow run
2. Find the `AI-06 - RAG Offline Evaluation` job
3. View console output for test results
4. Download artifacts:
   - Click on workflow run
   - Scroll to "Artifacts" section
   - Download `rag-evaluation-report-{run_number}`

### Understanding Report Output

#### Markdown Report Structure

```markdown
# RAG Evaluation Report

**Dataset:** MeepleAI RAG Evaluation Dataset v1.0
**Evaluated:** 2025-10-16 14:30:00 UTC
**Status:** ✅ PASSED

## Summary
- **Total Queries:** 24
- **Successful:** 24 (100.00%)
- **Failed:** 0

## Information Retrieval Metrics
| Metric | Value |
|--------|-------|
| Mean Reciprocal Rank (MRR) | 0.8542 |
| Precision@1 | 0.7917 |
| Precision@3 | 0.7639 |
| Precision@5 | 0.7500 |
| Precision@10 | 0.6250 |
| Recall@K | 0.8333 |

## Performance Metrics
| Metric | Value |
|--------|-------|
| Average Latency | 125.43 ms |
| Latency p50 (median) | 110.00 ms |
| Latency p95 | 245.00 ms |
| Latency p99 | 312.00 ms |
| Average Confidence | 0.8234 |

## Top 10 Slowest Queries
| Query ID | Query | Latency (ms) |
|----------|-------|--------------|
| chess-016 | What is the fifty-move rule? | 312.45 |
...

## Top 10 Lowest Precision Queries
| Query ID | Query | P@5 | Recall |
|----------|-------|-----|--------|
| chess-014 | Can you castle while in check? | 0.4000 | 0.6667 |
...
```

## Dataset Structure

### JSON Schema

```json
{
  "name": "string (dataset name)",
  "version": "string (semver)",
  "createdAt": "ISO 8601 timestamp",
  "metadata": {
    "description": "optional metadata",
    "...": "..."
  },
  "queries": [
    {
      "id": "unique-query-id",
      "gameId": "game-identifier",
      "query": "the actual question",
      "groundTruthAnswer": "expected answer (optional)",
      "relevantDocIds": ["doc-id-1", "doc-id-2"],
      "difficulty": "easy|medium|hard (optional)",
      "category": "setup|gameplay|winning (optional)"
    }
  ]
}
```

### Creating Custom Datasets

1. **Identify Test Scenarios**
   Cover diverse query types: simple facts, complex rules, edge cases

2. **Determine Relevant Documents**
   List PDF IDs that contain the answer (ground truth for recall)

3. **Write Ground Truth Answers**
   Optional but useful for future LLM-based semantic evaluation

4. **Categorize Queries**
   Tag by difficulty and category for analysis

5. **Validate JSON**
   ```bash
   cat your-dataset.json | jq empty
   ```

### Example Query Entry

```json
{
  "id": "chess-castling-001",
  "gameId": "chess",
  "query": "Can you castle if your king has already moved?",
  "groundTruthAnswer": "No, castling is only legal if the king has not moved",
  "relevantDocIds": ["chess-rulebook-001"],
  "difficulty": "medium",
  "category": "special-moves"
}
```

## CI Integration Details

### When It Runs

- **Trigger:** PRs or pushes that modify `apps/api/**`
- **Parallel:** Runs alongside `ci-api` job
- **Duration:** ~2-3 minutes (with Testcontainers startup)

### What It Does

1. **Setup Infrastructure**
   - Starts Postgres 16 container
   - Starts Qdrant 1.12.4 container
   - Verifies health of both services

2. **Run Tests**
   - Executes `RagEvaluationIntegrationTests`
   - Tests load dataset, run evaluation, generate reports
   - Validates metric calculations

3. **Generate Reports**
   - Creates markdown summary
   - Documents quality thresholds
   - Provides run metadata

4. **Upload Artifacts**
   - Stores reports for 30 days
   - Available for download from workflow runs

5. **Enforce Quality Gates**
   - If tests fail, CI fails
   - Quality threshold violations cause test failures

### Interpreting CI Results

#### ✅ Success Scenario
```
✓ RAG evaluation quality gates checked
If tests passed, all quality thresholds were met:
  - Precision@5 >= 0.70
  - MRR >= 0.60
  - Latency p95 <= 2000ms
  - Success rate >= 95%
```

#### ❌ Failure Scenario
```
Test Failed: Evaluation_QualityGates_EnforcedCorrectly
Expected: PassedQualityGates == true
Actual: false

Quality Gate Failures:
  - Precision@5 (0.6500) below threshold (0.7000)
  - MRR (0.5500) below threshold (0.6000)
```

**Action Items:**
1. Review per-query results to identify problem queries
2. Investigate low-scoring queries (relevance issue? missing data?)
3. Consider if thresholds are too strict for current dataset
4. Improve chunking, embedding, or indexing strategy

## Troubleshooting

### Common Issues

#### Issue: All queries return 0 results

**Symptoms:**
- Precision@K = 0.0
- Recall@K = 0.0
- MRR = 0.0

**Causes:**
- Qdrant collection not initialized
- Documents not indexed
- Wrong gameId filter

**Solutions:**
```csharp
// Ensure collection exists
await qdrantService.EnsureCollectionExistsAsync();

// Index documents
await qdrantService.IndexDocumentChunksAsync(gameId, pdfId, chunks);

// Verify gameId matches
// Dataset: "gameId": "tic-tac-toe"
// Must match indexed documents' game_id payload
```

#### Issue: Low Precision@5 despite relevant documents

**Symptoms:**
- Documents are indexed
- Search returns results
- But relevantDocIds don't match retrieved PdfIds

**Causes:**
- Mismatch between dataset `relevantDocIds` and actual `PdfId` in Qdrant
- Ground truth is outdated

**Solutions:**
```csharp
// Check what PdfIds are actually retrieved
var searchResult = await qdrantService.SearchAsync(gameId, embedding, limit: 5);
foreach (var result in searchResult.Results)
{
    Console.WriteLine($"Retrieved PdfId: {result.PdfId}");
}

// Update dataset to match actual PdfIds
```

#### Issue: High latency p95

**Symptoms:**
- Latency p95 > 2000ms
- Most queries are fast, but a few are slow

**Causes:**
- Cold start (first queries slower)
- Qdrant container resource constraints
- Large embedding vectors
- Network issues in CI

**Solutions:**
- Warm up Qdrant before evaluation (run dummy queries)
- Increase timeout thresholds for CI environment
- Profile slow queries individually
- Consider caching embeddings

#### Issue: Integration tests fail with connection errors

**Symptoms:**
```
Qdrant connection failed: Connection refused
```

**Causes:**
- Testcontainers not starting properly
- Port conflicts
- Docker not available

**Solutions:**
```bash
# Verify Docker is running
docker ps

# Check for port conflicts
netstat -an | grep 6333

# Run with verbose logging
dotnet test --logger "console;verbosity=detailed"
```

## Performance Considerations

### Evaluation Runtime

- **24 queries** × **~50-100ms per query** = **1.2-2.4 seconds**
- Plus overhead:
  - Dataset loading: ~10ms
  - Report generation: ~50ms
  - Testcontainer startup: ~30-60 seconds (one-time per test run)

**Total CI Time:** ~2-3 minutes including infrastructure setup

### Optimizations

1. **Parallel Evaluation (Future)**
   ```csharp
   var tasks = dataset.Queries.Select(q => EvaluateQueryAsync(q, topK, ct));
   var results = await Task.WhenAll(tasks);
   ```

2. **Embedding Caching**
   - Cache query embeddings between runs
   - Useful for regression testing

3. **Incremental Evaluation**
   - Only evaluate changed queries
   - Track query checksums

## Future Enhancements

### Phase 2: Semantic Metrics

**LLM-as-Judge:**
- Faithfulness: Is answer grounded in retrieved context?
- Answer Relevancy: Does answer address the question?
- Answer Correctness: Compare with ground truth semantically

**Implementation:**
```csharp
public async Task<double> CalculateFaithfulnessAsync(
    string answer,
    List<string> contexts,
    CancellationToken ct)
{
    // Use LLM to check if answer is supported by contexts
    // Return score 0.0 to 1.0
}
```

### Phase 3: Production Monitoring

**Online Evaluation:**
- Log retrieval metrics for production queries
- Track Precision@K over time (using user feedback as ground truth)
- Alert on metric degradation

**Grafana Dashboard:**
- Real-time MRR trends
- Latency distributions
- Quality gate status

### Phase 4: A/B Testing

**Experiment Framework:**
- Compare embedding models (OpenAI vs Ollama)
- Test chunking strategies (512 vs 1024 chars)
- Evaluate different retrieval K values

## References

### Information Retrieval Metrics
- [TREC Evaluation Metrics](https://trec.nist.gov/pubs/trec16/appendices/measures.pdf)
- [Mean Reciprocal Rank (Wikipedia)](https://en.wikipedia.org/wiki/Mean_reciprocal_rank)
- [Precision and Recall (Wikipedia)](https://en.wikipedia.org/wiki/Precision_and_recall)

### RAG Evaluation
- [Ragas Framework (Python)](https://github.com/explodinggradients/ragas) - Inspiration for metrics
- [RAG Evaluation Best Practices](https://www.anyscale.com/blog/a-comprehensive-guide-for-building-rag-based-llm-applications)

### Project Documentation
- [`docs/observability.md`](./observability.md) - Health checks and logging
- [`docs/ops-02-opentelemetry-design.md`](./ops-02-opentelemetry-design.md) - Metrics and tracing
- [`docs/code-coverage.md`](./code-coverage.md) - Testing standards

## Summary

The RAG offline evaluation system provides:

✅ **Objective Metrics:** Precision, Recall, MRR, latency
✅ **Quality Gates:** Enforced minimum standards in CI
✅ **Comprehensive Testing:** 20+ unit tests, 8 integration tests
✅ **CI Integration:** Automated evaluation on every API change
✅ **Detailed Reports:** Markdown and JSON output with per-query analysis
✅ **24 Test Queries:** Covering Tic-Tac-Toe and Chess rules

This enables data-driven decisions about RAG system improvements and prevents quality regressions.
