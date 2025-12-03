# Runbook: RAG Evaluation Pipeline (ADR-016 Phase 5)

**Issue**: Running and interpreting RAG evaluation metrics
**Severity**: Informational (Operational Guidance)
**Related Dashboards**: [RAG Evaluation](http://localhost:3001/d/rag-evaluation)
**Related Docs**: [ADR-016](../../01-architecture/adr/adr-016-advanced-pdf-embedding-pipeline.md), [Observability Guide](../observability.md)

---

## Overview

The RAG Evaluation Pipeline measures retrieval quality through systematic evaluation against curated datasets. It implements:

- **Grid Search**: Systematic evaluation across 12 configuration combinations
- **Metrics Collection**: Recall@5, Recall@10, nDCG@10, MRR, P95 latency
- **Benchmark Reports**: Automated markdown reports for decision support
- **Observability**: Prometheus metrics with Grafana dashboard

### Phase 5 Targets (ADR-016)

| Metric | Target | Priority |
|--------|--------|----------|
| Recall@10 | ≥ 70% | Primary |
| P95 Latency | < 1500ms | Primary |
| nDCG@10 | ≥ 65% | Secondary |
| MRR | ≥ 0.50 | Secondary |

---

## Running Evaluations

### Prerequisites

1. **Dataset File**: Ensure evaluation dataset exists at:
   ```
   tests/evaluation-datasets/meepleai-custom/custom-dataset.json
   ```

2. **Services Running**: Verify all required services:
   ```bash
   # Check health
   curl http://localhost:8080/health/ready | jq .

   # Required services: postgres, qdrant, redis, api
   docker compose ps
   ```

3. **Reranker Service** (optional but recommended):
   ```bash
   # Verify reranker is running
   curl http://localhost:8003/health
   ```

### Quick Mode Evaluation

For rapid iteration and testing (3 key configurations):

```bash
# Via MediatR command (from code/tests)
dotnet test --filter "RunGridSearchHandler" \
  --logger "console;verbosity=detailed"
```

Quick mode evaluates:
- `baseline-small-none-none` (baseline)
- `semantic-medium-binary-none` (balanced)
- `semantic-large-scalar-bge-reranker` (full pipeline)

### Full Grid Search

Evaluates all 12 configuration combinations:

```bash
# Configuration matrix:
# Chunking: [512-10%, 1024-20%, 2048-15%]
# Quantization: [None, Binary, Scalar]
# Reranking: [None, BGE-reranker-v2-m3]
```

**Expected Duration**: 10-30 minutes depending on dataset size

### Custom Configuration Evaluation

To evaluate specific configurations:

```csharp
// From RunGridSearchCommand
var command = new RunGridSearchCommand
{
    ConfigurationIds = new List<string>
    {
        "semantic-medium-binary-none",
        "semantic-large-scalar-bge-reranker"
    },
    MaxSamplesPerConfig = 50 // Limit for faster iteration
};
```

---

## Interpreting Results

### Grafana Dashboard

Access: http://localhost:3001/d/rag-evaluation

#### Key Panels

1. **Phase 5 Target Status (Gauges)**
   - Green zone: ≥70% Recall, <1500ms P95
   - Yellow zone: 60-70% Recall, 1500-2000ms P95
   - Red zone: <60% Recall, >2000ms P95

2. **Recall and Ranking Metrics Over Time**
   - Track improvements across evaluation runs
   - Filter by configuration using dropdown

3. **P95 Latency Distribution**
   - Histogram shows latency spread
   - Watch for bimodal distributions (indicates inconsistent performance)

4. **Configuration Comparison Table**
   - Side-by-side metrics for all evaluated configs
   - Sort by Recall@10 or P95 to find optimal balance

### Prometheus Queries

```promql
# Current Recall@10 average
avg(meepleai_evaluation_recall_at10_sum / meepleai_evaluation_recall_at10_count)

# P95 Latency by configuration
histogram_quantile(0.95,
  sum(rate(meepleai_evaluation_p95_latency_bucket[5m])) by (le, configuration)
)

# Configurations meeting Phase 5 target
sum(meepleai_evaluation_target_met_total) by (configuration)

# Grid search duration trend
histogram_quantile(0.95,
  sum(rate(meepleai_grid_search_duration_bucket[1h])) by (le)
)
```

### Benchmark Reports

After grid search completion, generate markdown report:

```csharp
var report = BenchmarkReport.Create(
    title: "Monthly RAG Evaluation",
    gridSearchResult: result,
    sampleCount: dataset.Count
);

var markdown = reportGenerator.GenerateMarkdownReport(report);
File.WriteAllText($"reports/benchmark-{DateTime.UtcNow:yyyyMMdd}.md", markdown);
```

Report sections:
- Executive Summary (configs evaluated, target status)
- Phase 5 Target Status (pass/fail with metrics)
- Configuration Comparison (full metrics table)
- Best Configurations (by Recall, nDCG, Latency)
- Recommendations (next steps based on results)

---

## Troubleshooting Guide

### Problem: Low Recall Scores (<60%)

**Symptoms**: Recall@5 and Recall@10 below acceptable thresholds

**Common Causes**:
1. **Poor embedding quality** (outdated model or config)
2. **Chunk size mismatch** (chunks too large/small for queries)
3. **Dataset-pipeline mismatch** (evaluation dataset doesn't match production patterns)

**Diagnostic Steps**:
```bash
# 1. Check embedding service
curl http://localhost:8000/health
docker compose logs embedding | tail -50

# 2. Verify Qdrant collection
curl http://localhost:6333/collections/meepleai-documents | jq .

# 3. Sample query test
curl -X POST http://localhost:8080/api/v1/search \
  -H "Content-Type: application/json" \
  -d '{"query":"How many cards in starting hand?","topK":10}'
```

**Resolution Actions**:
- Review chunk configuration (try smaller chunks for precise retrieval)
- Enable reranking for improved relevance
- Verify embedding model matches production

### Problem: High P95 Latency (>2000ms)

**Symptoms**: P95 latency exceeding targets, slow responses

**Common Causes**:
1. **Reranker cold start** (first request after idle period)
2. **Large retrieval set** (topK too high)
3. **Resource contention** (Qdrant CPU/memory pressure)

**Diagnostic Steps**:
```bash
# 1. Check Qdrant resource usage
docker stats qdrant

# 2. Check reranker response time
curl -X POST http://localhost:8003/rerank \
  -H "Content-Type: application/json" \
  -d '{"query":"test","documents":["doc1","doc2"]}' \
  -w "\n\nTime: %{time_total}s\n"

# 3. Check API logs for slow operations
docker compose logs api | grep -E "duration|latency" | tail -20
```

**Resolution Actions**:
- Enable quantization (binary or scalar) for faster vector search
- Reduce topK for initial retrieval
- Scale Qdrant resources if under pressure
- Implement reranker connection pooling

### Problem: Evaluation Fails to Start

**Symptoms**: Grid search command errors, no metrics recorded

**Common Causes**:
1. **Dataset file not found**
2. **Service dependencies unavailable**
3. **Configuration errors**

**Diagnostic Steps**:
```bash
# 1. Verify dataset exists
ls -la tests/evaluation-datasets/meepleai-custom/

# 2. Validate dataset JSON
cat tests/evaluation-datasets/meepleai-custom/custom-dataset.json | jq .

# 3. Check service health
curl http://localhost:8080/health/ready | jq .
```

**Resolution Actions**:
- Create/copy evaluation dataset to expected path
- Restart required services
- Check logs for configuration errors

### Problem: Inconsistent Results Between Runs

**Symptoms**: Same configuration produces different metrics across runs

**Common Causes**:
1. **Non-deterministic retrieval** (tie-breaking in scoring)
2. **Cache interference** (stale cached results)
3. **Concurrent modifications** (documents added/removed during evaluation)

**Diagnostic Steps**:
```bash
# 1. Clear Redis cache
docker compose exec redis redis-cli FLUSHALL

# 2. Verify document count stability
curl http://localhost:6333/collections/meepleai-documents | jq '.result.points_count'

# 3. Run evaluation with fixed seed (if supported)
```

**Resolution Actions**:
- Clear caches before evaluation runs
- Ensure stable document corpus during evaluation
- Run multiple evaluations and average results

---

## Alerts Reference

### Warning Alerts

#### LowRecallScore
**Trigger**: Best Recall@10 < 65% for any configuration
**Impact**: RAG quality may be degraded
**SLA**: Investigate within 4 hours

#### HighEvaluationLatency
**Trigger**: P95 latency > 2000ms consistently
**Impact**: User experience degraded
**SLA**: Investigate within 2 hours

### Critical Alerts

#### Phase5TargetNotMet
**Trigger**: No configuration meets Recall@10 ≥ 70% AND P95 < 1500ms
**Impact**: RAG pipeline needs optimization before production
**SLA**: Investigate within 1 hour

#### EvaluationPipelineDown
**Trigger**: No evaluation metrics recorded for 24 hours (when expected)
**Impact**: Quality monitoring blind spot
**SLA**: Investigate within 4 hours

---

## Operational Procedures

### Monthly Evaluation Cadence

1. **Week 1**: Run full grid search against production dataset
2. **Week 2**: Analyze results, identify optimization opportunities
3. **Week 3**: Implement top improvements
4. **Week 4**: Validate improvements with focused evaluation

### Pre-Release Evaluation

Before deploying RAG changes:

```bash
# 1. Create evaluation branch
git checkout -b eval/pre-release-$(date +%Y%m%d)

# 2. Run quick mode evaluation
dotnet test --filter "QuickMode"

# 3. Compare against baseline
# Review benchmark report for regression

# 4. If acceptable, merge to main
git checkout main && git merge eval/pre-release-*
```

### Dataset Maintenance

```bash
# Location
tests/evaluation-datasets/meepleai-custom/

# Structure
{
  "name": "dataset-name",
  "version": "1.0.0",
  "samples": [
    {
      "id": "sample-001",
      "query": "How many cards in starting hand?",
      "relevant_document_ids": ["doc-uuid-1", "doc-uuid-2"],
      "expected_answer": "Each player starts with 7 cards."
    }
  ]
}
```

**Best Practices**:
- Include diverse query types (factual, procedural, conceptual)
- Minimum 50 samples for meaningful evaluation
- Version datasets alongside code changes
- Review and update quarterly

---

## Configuration Reference

### Grid Search Configurations

| Config ID | Chunking | Quantization | Reranking |
|-----------|----------|--------------|-----------|
| baseline-small-none-none | 512 tokens, 10% overlap | None | None |
| baseline-medium-none-none | 1024 tokens, 20% overlap | None | None |
| baseline-large-none-none | 2048 tokens, 15% overlap | None | None |
| semantic-small-binary-none | 512 tokens, 10% overlap | Binary | None |
| semantic-medium-binary-none | 1024 tokens, 20% overlap | Binary | None |
| semantic-large-binary-none | 2048 tokens, 15% overlap | Binary | None |
| semantic-small-scalar-none | 512 tokens, 10% overlap | Scalar | None |
| semantic-medium-scalar-none | 1024 tokens, 20% overlap | Scalar | None |
| semantic-large-scalar-none | 2048 tokens, 15% overlap | Scalar | None |
| semantic-small-scalar-bge-reranker | 512 tokens, 10% overlap | Scalar | BGE-reranker-v2-m3 |
| semantic-medium-scalar-bge-reranker | 1024 tokens, 20% overlap | Scalar | BGE-reranker-v2-m3 |
| semantic-large-scalar-bge-reranker | 2048 tokens, 15% overlap | Scalar | BGE-reranker-v2-m3 |

### Environment Variables

```bash
# Reranking service
RERANKING_BASE_URL=http://localhost:8003
RERANKING_TIMEOUT_SECONDS=10

# Evaluation defaults
EVALUATION_DEFAULT_TOPK=10
EVALUATION_MAX_SAMPLES=100
```

### Metrics Reference

| Metric Name | Type | Labels | Description |
|-------------|------|--------|-------------|
| meepleai_evaluation_runs_total | Counter | configuration, dataset, meets_target | Total evaluation runs |
| meepleai_evaluation_recall_at5 | Histogram | configuration, dataset | Recall@5 distribution |
| meepleai_evaluation_recall_at10 | Histogram | configuration, dataset | Recall@10 distribution |
| meepleai_evaluation_ndcg_at10 | Histogram | configuration, dataset | nDCG@10 distribution |
| meepleai_evaluation_mrr | Histogram | configuration, dataset | MRR distribution |
| meepleai_evaluation_p95_latency | Histogram | configuration, dataset | P95 latency in ms |
| meepleai_evaluation_target_met_total | Counter | configuration, dataset | Configs meeting Phase 5 target |
| meepleai_grid_search_duration | Histogram | dataset | Grid search total duration |
| meepleai_grid_search_configs_evaluated | Counter | dataset | Configurations evaluated |

---

## Escalation Path

1. **L1 - Self-Service**
   - Use this runbook for common issues
   - Check Grafana dashboard for metrics
   - Review recent benchmark reports

2. **L2 - Engineering Support**
   - Escalate if no resolution after 2 hours
   - Create GitHub issue with:
     - Configuration used
     - Metrics observed
     - Steps taken
     - Logs (sanitized)

3. **L3 - Architecture Review**
   - Required when:
     - Phase 5 targets consistently not met
     - Major pipeline changes needed
     - Performance regression >20%

---

## Related Documentation

- **ADR-016**: [Advanced PDF Embedding Pipeline](../../01-architecture/adr/adr-016-advanced-pdf-embedding-pipeline.md)
- **Phase 4 Implementation**: Cross-Encoder Reranking
- **Observability Guide**: [docs/05-operations/observability.md](../observability.md)
- **RAG Architecture**: [docs/01-architecture/overview/rag-architecture.md](../../01-architecture/overview/rag-architecture.md)

---

**Last Updated**: 2025-12-03 (ADR-016 Phase 5 implementation)
**Maintained By**: MeepleAI Engineering Team
**Feedback**: Submit improvements via GitHub issues
