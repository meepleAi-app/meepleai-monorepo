# Runbook: Low AI Quality Alerts

**Issue**: AI response quality has dropped below acceptable thresholds
**Severity**: Critical or Warning (depending on alert)
**Related Dashboards**: [AI Quality Monitoring](http://localhost:3001/d/quality-metrics), [AI/RAG Operations](http://localhost:3001/d/ai-rag-operations)
**Related Docs**: [Observability Guide](../observability.md), [Quality Scoring Design](../issue/ai-11-quality-scoring-red-phase-summary.md)

---

## Overview

MeepleAI's quality scoring system tracks AI response quality across four dimensions:
- **RAG Confidence** (0.0-1.0): Relevance of retrieved rulebook context
- **LLM Confidence** (0.0-1.0): Language model's confidence in its response
- **Citation Quality** (0.0-1.0): Accuracy and completeness of rulebook citations
- **Overall Confidence** (0.0-1.0): Weighted average of all dimensions

Quality tiers:
- **High**: ≥0.80 overall confidence
- **Medium**: 0.60-0.80 overall confidence
- **Low**: <0.60 overall confidence

---

## Alerts Reference

### Critical Alerts (Immediate Action Required)

#### 🔴 LowOverallConfidence
**Trigger**: Average overall confidence < 0.60 for 1 hour
**Impact**: AI responses are consistently failing quality standards
**SLA**: Investigate within 30 minutes

#### 🔴 HighLowQualityRate
**Trigger**: >30% of responses are low quality for 1 hour
**Impact**: More than 1 in 3 AI responses failing quality checks
**SLA**: Investigate within 30 minutes

#### 🔴 LowRagConfidence
**Trigger**: RAG confidence < 0.50 for 30 minutes
**Impact**: Semantic search not finding relevant rulebook context
**SLA**: Investigate within 30 minutes

#### 🔴 LowLlmConfidence
**Trigger**: LLM confidence < 0.50 for 30 minutes
**Impact**: Language model producing low-quality responses
**SLA**: Investigate within 30 minutes

#### 🔴 QualityMetricsUnavailable
**Trigger**: No quality metrics recorded for 15 minutes
**Impact**: Quality scoring pipeline is broken or no AI requests
**SLA**: Investigate within 15 minutes

### Warning Alerts (Monitor & Investigate)

#### 🟡 DegradedOverallConfidence
**Trigger**: Average overall confidence < 0.70 for 30 minutes
**Impact**: Response quality degraded but not critical
**SLA**: Investigate within 2 hours

#### 🟡 ElevatedLowQualityRate
**Trigger**: >15% of responses are low quality for 30 minutes
**Impact**: Higher than normal rate of quality failures
**SLA**: Investigate within 2 hours

---

## Troubleshooting Guide

### Step 1: Identify the Root Cause

Access the dashboards and logs:
```bash
# View quality dashboard
open http://localhost:3001/d/quality-metrics

# Check Seq logs for quality scoring events
open http://localhost:8081
# Filter: QualityScores, IsLowQuality=true

# View Prometheus alerts
open http://localhost:9090/alerts
```

Check which dimension is failing:
- **RAG Confidence Low** → RAG retrieval issue
- **LLM Confidence Low** → Language model issue
- **Citation Quality Low** → Citation extraction issue
- **All Dimensions Low** → Systemic problem

### Step 2: RAG Confidence Issues (LowRagConfidence alert)

**Symptoms**: Semantic search not finding relevant rulebook context

**Common Causes**:
1. **Qdrant vector DB down or unhealthy**
2. **Embedding service failing** (OpenRouter API issues)
3. **Poor vector index quality** (outdated or corrupted embeddings)
4. **Query expansion not working** (PERF-08 implementation issues)

**Diagnostic Steps**:
```bash
# 1. Check Qdrant health
curl http://localhost:6333/healthz
# Expected: 200 OK

# 2. Check Qdrant collection status
curl http://localhost:6333/collections/meepleai-documents | jq .
# Expected: "status": "green", "points_count" > 0

# 3. Check embedding service logs
docker compose logs api | grep -i "embedding"

# 4. Test vector search manually
curl -X POST http://localhost:8080/api/v1/rag/search \
  -H "Content-Type: application/json" \
  -d '{"query":"How do I win in Chess?","gameId":"<chess-game-id>","topK":5}'
# Expected: 200 OK with relevant results
```

**Resolution Actions**:
- **Qdrant down**: Restart Qdrant container
  ```bash
  cd infra && docker compose restart qdrant
  ```
- **Embedding service failing**: Check OpenRouter API key and quota
  ```bash
  # Check API logs for OpenRouter errors
  docker compose logs api | grep -i "openrouter"
  ```
- **Poor index quality**: Re-index PDFs
  ```bash
  # Trigger re-indexing via admin API (future feature)
  # For now: Check PDF upload logs, verify chunks were created
  ```

### Step 3: LLM Confidence Issues (LowLlmConfidence alert)

**Symptoms**: Language model producing low-confidence responses

**Common Causes**:
1. **OpenRouter API issues** (rate limits, outages)
2. **Prompt quality degraded** (recent changes to prompts)
3. **Model selection issues** (wrong model for task)
4. **Input context truncation** (exceeding token limits)

**Diagnostic Steps**:
```bash
# 1. Check OpenRouter API health
curl https://openrouter.ai/api/v1/models \
  -H "Authorization: Bearer $OPENROUTER_API_KEY"
# Expected: 200 OK with model list

# 2. Check LLM service logs for errors
docker compose logs api | grep -i "llm\|openrouter"

# 3. Check token usage metrics
open http://localhost:3001/d/ai-rag-operations
# Panel: "AI Tokens Used"

# 4. Test LLM directly (manual QA request)
curl -X POST http://localhost:8080/api/v1/agents/qa \
  -H "Content-Type: application/json" \
  -H "Cookie: session=<your-session-cookie>" \
  -d '{"gameId":"<game-id>","query":"Test question"}'
```

**Resolution Actions**:
- **API rate limits**: Check OpenRouter dashboard, consider upgrading tier
- **Prompt quality**: Review recent prompt changes, rollback if needed
- **Model selection**: Verify `LlmService` configuration, consider switching models
- **Token limits**: Review context window usage, implement better truncation

### Step 4: Citation Quality Issues

**Symptoms**: Citations are inaccurate or missing

**Common Causes**:
1. **Citation extraction failing** (parsing errors)
2. **Source metadata missing** (PDF processing issues)
3. **Page number mapping incorrect** (chunking issues)

**Diagnostic Steps**:
```bash
# 1. Check citation extraction logs
docker compose logs api | grep -i "citation"

# 2. Verify PDF metadata in database
# (Use database query tool to check pdf_documents table)

# 3. Test full QA flow with citations
curl -X POST http://localhost:8080/api/v1/agents/qa \
  -H "Content-Type: application/json" \
  -H "Cookie: session=<your-session-cookie>" \
  -d '{"gameId":"<game-id>","query":"Test with citations"}'
# Check response.citations array
```

**Resolution Actions**:
- **Extraction failing**: Review `CitationExtractor` implementation, check logs
- **Missing metadata**: Re-process affected PDFs
- **Page mapping issues**: Review `TextChunkingService` (PERF-07)

### Step 5: Systemic Quality Issues (LowOverallConfidence alert)

**Symptoms**: All quality dimensions degraded simultaneously

**Common Causes**:
1. **Recent deployment broke quality scoring**
2. **External dependencies all failing** (Qdrant + OpenRouter + Redis)
3. **Database performance degraded** (slow queries affecting RAG)
4. **Memory/resource exhaustion** (API container under stress)

**Diagnostic Steps**:
```bash
# 1. Check all health endpoints
curl http://localhost:8080/health/ready | jq .
# Check status of: Postgres, Redis, Qdrant

# 2. Check resource usage
docker stats api qdrant postgres redis

# 3. Review recent deployments
git log --oneline -n 10
# Look for recent changes to quality scoring, RAG, or LLM code

# 4. Check error logs
docker compose logs api | grep -E "ERROR|CRITICAL" | tail -50
```

**Resolution Actions**:
- **Recent deployment issue**: Rollback to previous version
  ```bash
  git checkout <previous-commit>
  cd infra && docker compose up --build -d api
  ```
- **Dependency failures**: Restart all services
  ```bash
  cd infra && docker compose restart
  ```
- **Database performance**: Run VACUUM, check slow queries
- **Resource exhaustion**: Scale API container, increase limits

### Step 6: QualityMetricsUnavailable (No Metrics Recorded)

**Symptoms**: No quality metrics for 15+ minutes

**Possible Causes**:
1. **No AI requests being processed** (low traffic)
2. **Quality scoring service broken** (`QualityMetrics` class failing)
3. **OpenTelemetry metrics export broken** (Prometheus scraping failed)

**Diagnostic Steps**:
```bash
# 1. Check if AI endpoints are receiving traffic
docker compose logs api | grep -E "POST /api/v1/agents" | tail -20

# 2. Check Prometheus scraping
curl http://localhost:8080/metrics | grep meepleai_quality
# Expected: meepleai_quality_score_bucket, meepleai_quality_low_quality_responses_total

# 3. Check Prometheus targets
open http://localhost:9090/targets
# meepleai-api should be UP

# 4. Test quality scoring manually
# (Send a QA request and verify metrics are recorded)
```

**Resolution Actions**:
- **No traffic**: Wait or generate test traffic
- **Quality scoring broken**: Check `QualityMetrics` initialization in `Program.cs`
- **Metrics export broken**: Restart API container, verify OpenTelemetry config

---

## Quick Reference Commands

### View Current Quality Metrics
```bash
# Overall confidence (last 5 minutes)
curl -s http://localhost:9090/api/v1/query \
  --data-urlencode 'query=avg(meepleai_quality_score{dimension="overall_confidence"})' | jq .

# Low-quality rate (percentage)
curl -s http://localhost:9090/api/v1/query \
  --data-urlencode 'query=meepleai:quality:low_quality_rate:5m' | jq .
```

### Restart Affected Services
```bash
# RAG issues → Restart Qdrant
cd infra && docker compose restart qdrant

# LLM issues → Restart API (if config changes needed)
cd infra && docker compose restart api

# All issues → Full restart
cd infra && docker compose restart
```

### Generate Test Traffic for Debugging
```bash
# Send test QA request
curl -X POST http://localhost:8080/api/v1/agents/qa \
  -H "Content-Type: application/json" \
  -H "Cookie: session=<session-cookie>" \
  -d '{
    "gameId":"<game-id>",
    "query":"How do I set up the game?"
  }' | jq .
```

---

## Escalation Path

1. **Immediate** (Severity: Critical, no resolution within 1 hour)
   - Escalate to on-call engineer
   - Create incident in incident management system
   - Notify product manager if user-facing impact

2. **Follow-up** (Resolved but recurring)
   - Create GitHub issue to track root cause analysis
   - Schedule postmortem if systemic issue
   - Update runbook with lessons learned

---

## Prevention & Monitoring

### Proactive Monitoring
- Review quality dashboard daily
- Set up Alertmanager notifications (future: Slack, PagerDuty)
- Track quality trends in weekly engineering review

### Quality Improvement
- Monitor p50/p95/p99 confidence percentiles
- Analyze low-quality responses for patterns
- Improve RAG retrieval with better embeddings (AI-07)
- Fine-tune LLM prompts for higher confidence
- Enhance citation extraction accuracy

### Testing
- Add quality scoring to integration tests
- Simulate low-quality scenarios in staging
- Verify alert thresholds are appropriate

---

## Related Documentation

- **Quality Scoring Design**: [AI-11 Quality Scoring Implementation](../issue/ai-11-quality-scoring-red-phase-summary.md)
- **Observability Guide**: [docs/observability.md](../observability.md)
- **OpenTelemetry Design**: [docs/technic/ops-02-opentelemetry-design.md](../technic/ops-02-opentelemetry-design.md)
- **RAG Optimization**: [docs/technic/ai-07-rag-optimization-phase1.md](../technic/ai-07-rag-optimization-phase1.md)
- **Performance Optimizations**: [docs/technic/performance-optimization-summary.md](../technic/performance-optimization-summary.md)

---

**Last Updated**: 2025-10-25 (AI-11.2 implementation)
**Maintained By**: MeepleAI Engineering Team
**Feedback**: Submit improvements via GitHub issues
