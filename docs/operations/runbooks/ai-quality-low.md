# AI Quality Low Runbook

**Alerts**: `LowOverallConfidence`, `HighLowQualityRate`, `LowRagConfidence`, `LowLlmConfidence`, `QualityMetricsUnavailable`, `DegradedOverallConfidence`, `ElevatedLowQualityRate`
**Dashboard**: [Quality Metrics](http://localhost:3001/d/quality-metrics)

## Symptom

AI response quality metrics have dropped below acceptable thresholds:

| Alert | Threshold | Duration | Severity |
|-------|-----------|----------|----------|
| `LowOverallConfidence` | < 60% | 1 hour | Critical |
| `HighLowQualityRate` | > 30% low quality | 1 hour | Critical |
| `LowRagConfidence` | < 50% | 30 min | Critical |
| `LowLlmConfidence` | < 50% | 30 min | Critical |
| `QualityMetricsUnavailable` | No metrics | 15 min | Critical |
| `DegradedOverallConfidence` | < 70% | 30 min | Warning |
| `ElevatedLowQualityRate` | > 15% low quality | 30 min | Warning |

## Impact

- **Users affected**: AI chat responses are inaccurate, irrelevant, or low confidence
- **Trust**: Users lose confidence in the AI assistant
- **Cascading**: Low RAG confidence → bad context → bad LLM response → low overall confidence

## Diagnosis Steps

### 1. Identify which quality dimension is failing

```bash
# Overall confidence
curl -s 'http://localhost:9090/api/v1/query?query=meepleai:quality:overall_confidence:5m' | jq '.data.result[0].value[1]'

# RAG confidence (retrieval quality)
curl -s 'http://localhost:9090/api/v1/query?query=meepleai:quality:rag_confidence:5m' | jq '.data.result[0].value[1]'

# LLM confidence (generation quality)
curl -s 'http://localhost:9090/api/v1/query?query=meepleai:quality:llm_confidence:5m' | jq '.data.result[0].value[1]'

# Low quality rate
curl -s 'http://localhost:9090/api/v1/query?query=meepleai:quality:low_quality_rate:5m' | jq '.data.result[0].value[1]'
```

### 2. If RAG confidence is low — check retrieval pipeline

```bash
# Qdrant health and collection status
curl -s http://localhost:6333/collections/meepleai_documents | jq '{status, vectors_count: .result.vectors_count, points_count: .result.points_count}'

# Embedding service health
curl -s http://localhost:8000/health

# Test embedding generation
curl -s -X POST http://localhost:8000/embeddings -H 'Content-Type: application/json' -d '{"texts": ["test query"], "language": "en"}' | jq '{embedding_count: (.embeddings | length), dimension: (.embeddings[0] | length)}'
```

### 3. If LLM confidence is low — check LLM provider

```bash
# Check which LLM model is being used
pwsh -c "docker logs meepleai-api --tail=100 2>&1" | grep -i "model\|provider" | tail -5

# Check if fallback model is active (may explain lower quality)
pwsh -c "docker logs meepleai-api --tail=200 2>&1" | grep -i "fallback\|downgrade\|ModelDowngrade"

# Check OpenRouter API errors
pwsh -c "docker logs meepleai-api --tail=200 2>&1" | grep -i "openrouter\|429\|rate.limit\|api.error"
```

### 4. If metrics unavailable — check instrumentation

```bash
# Are quality metrics being emitted?
curl -s http://localhost:8080/metrics | grep meepleai_quality

# Is the API processing any AI requests?
curl -s 'http://localhost:9090/api/v1/query?query=rate(http_server_request_duration_count{http_route=~"/api/v1/chat.*"}[5m])' | jq '.data.result[0].value[1]'

# API container health
curl -s http://localhost:8080/health/ready | jq .
```

### 5. Check recent changes to AI configuration

```bash
# Recent agent configuration changes
pwsh -c "docker logs meepleai-api --tail=500 2>&1" | grep -i "agent.*config\|temperature\|topk\|model.*change"

# Recent commits to KB/AI code
git log --oneline --since="24 hours ago" -- apps/api/src/Api/BoundedContexts/KnowledgeBase/
```

## Remediation

### Quick Fix by Root Cause

**RAG Confidence Low**:
1. Check Qdrant is healthy and collection has expected vector count
2. Restart embedding service if embeddings are incorrect
   ```bash
   pwsh -c "docker compose restart embedding-service"
   ```
3. Verify document index is not corrupted — check recent indexing operations

**LLM Confidence Low**:
1. Check if fallback model is active — primary model rate limited
2. If on Ollama fallback (`llama3:8b`), quality is expected to be lower
3. Check OpenRouter API key validity and quota

**Metrics Unavailable**:
1. Restart API if quality scoring pipeline is stuck
   ```bash
   pwsh -c "docker compose restart api"
   ```
2. Check Prometheus scrape target health at http://localhost:9090/targets

### Root Cause Fix

1. **Bad embeddings**: Model may need updating, or embedding service misconfigured
2. **Index degradation**: Re-index documents if vector collection has stale/corrupt data
3. **Prompt regression**: Recent code change may have degraded system prompt quality
4. **Model change**: New LLM model may perform worse — evaluate and rollback if needed
5. **Data quality**: New documents indexed with poor text extraction → low RAG relevance

## Escalation

- **Warning (degraded, <70%)**: Monitor for 1 hour, check if self-resolving
- **Critical (low, <60%)**: Active investigation — check all pipeline components
- **Metrics unavailable**: Immediate — indicates broken instrumentation or dead AI pipeline
- **Sustained >2 hours**: Escalate — consider disabling AI chat temporarily with maintenance message

## Prevention

- Quality metrics monitoring with early warning at 70% threshold
- A/B testing for prompt changes before production rollout
- Embedding model version pinning
- Regular vector index health checks
- LLM fallback chain ensures availability even if quality degrades
