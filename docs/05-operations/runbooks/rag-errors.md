# Runbook: RAG Errors

**Alert**: `RagErrorsDetected`
**Severity**: WARNING
**Threshold**: > 0.5 errors/second for 3 minutes
**Expected Response Time**: < 15 minutes

## Symptoms

**Observable indicators when this alert fires:**
- Alert: "RagErrorsDetected" firing in Alertmanager
- RAG operations experiencing errors (Q&A functionality degraded)
- Dashboard shows RAG error rate > 0.5/sec
- Users cannot ask questions about game rules (chat endpoint failing)
- Semantic search functionality unavailable or returning errors

## Impact

**Effect on system and users:**
- **User Experience**: Cannot ask questions about rules, Q&A feature unavailable, search may return no results
- **Data Integrity**: Vector search may return incorrect or no results
- **Business Impact**: Core differentiator feature (AI rules assistant) down, user frustration, potential churn
- **System Health**: RAG pipeline broken (retrieval, embedding, or generation failing)

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/ai-rag-operations
```

**Verification checklist**:
- ✅ Is RAG error rate actually > 0.5 errors/sec?
- ✅ Which RAG component failing? (retrieval, embedding, generation)
- ✅ What percentage of RAG requests failing?
- ✅ Is this affecting all users or specific queries?

**Prometheus Query**:
```promql
meepleai:rag:error_rate:5m
```

**If false alarm**:
- Silence alert for 30 minutes
- Document if error rate was temporary spike
- Create issue to adjust alert threshold if recurring false positives

### 2. Identify RAG Component (1 minute)

**Questions to answer**:
1. **Which component failing?** (Qdrant, embedding service, LLM API)
2. **What error types?** (connection, timeout, validation, API errors)
3. **When did it start?** (correlate with deployments, config changes)
4. **Which queries affected?** (all queries, specific topics, specific games)

**HyperDX - RAG error logs**:
```
http://localhost:8180
Filter: level:error AND rag AND @timestamp:[now-10m TO now]
```

**Prometheus queries**:
```promql
# RAG error rate breakdown (if custom metrics available)
rate(meepleai_rag_errors_total[5m])

# RAG request rate (compare to error rate)
rate(meepleai_rag_requests_total[5m])

# RAG error ratio
rate(meepleai_rag_errors_total[5m]) / rate(meepleai_rag_requests_total[5m])
```

### 3. Check Qdrant Health (1 minute)

**Qdrant status**:
```bash
# Check container status
docker compose ps qdrant

# Check Qdrant health endpoint
curl http://localhost:6333/healthz
# Should return: {"status":"ok"}

# Check collections
curl http://localhost:6333/collections
# Should list meepleai collection

# Check Qdrant logs
docker compose logs qdrant --tail 100
```

**Look for**:
- "Collection not found" (collection missing or corrupted)
- "Out of memory" (resource exhaustion)
- "Connection refused" (Qdrant down)
- "Timeout" (slow queries, performance issue)

### 4. Check Embedding Service (1 minute)

**OpenRouter API health** (if using for embeddings):
```bash
# Check API connectivity
curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  https://openrouter.ai/api/v1/models

# Check API logs in HyperDX
# Filter: embedding AND error
```

**Look for**:
- "401 Unauthorized" (API key invalid or expired)
- "429 Too Many Requests" (rate limit exceeded)
- "500 Internal Server Error" (provider issue)
- "Timeout" (slow embedding generation)

### 5. Check LLM API (OpenRouter) (1 minute)

**OpenRouter status**:
```bash
# Test OpenRouter API health
curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  -H "Content-Type: application/json" \
  https://openrouter.ai/api/v1/models
# Should list available models

# Check recent LLM errors in HyperDX
# Filter: openrouter OR llm AND error
```

**Look for**:
- API key issues (invalid, expired, quota exceeded)
- Model availability (model deprecated or unavailable)
- Rate limiting (too many requests)
- Response errors (invalid format, generation failures)

### 6. Analyze RAG Logs (2 minutes)

**HyperDX - RAG pipeline errors**:
```
http://localhost:8180
Filter: level:error AND (rag OR vector OR embedding OR qdrant) AND @timestamp:[now-10m TO now]
```

**Look for patterns**:
- **Retrieval errors**: "Qdrant search failed", "No vectors found"
- **Embedding errors**: "Embedding generation failed", "OpenRouter API error"
- **Generation errors**: "LLM generation failed", "Invalid response format"
- **Validation errors**: "Low confidence score", "No citations found"

**Group by error type**:
```
exception_type is not null
Group by: exception_type
Sort by: count desc
```

### 7. Check RAG Configuration (1 minute)

**Verify RAG settings**:
```bash
# Check environment variables
docker compose exec api printenv | grep -i "qdrant\|openrouter\|embedding"

# Check appsettings.json RAG configuration
cat apps/api/src/Api/appsettings.json | grep -A 20 "Rag"

# Check Qdrant connection string
cat infra/env/api.env.dev | grep QDRANT_URL
```

**Configuration checklist**:
- ✅ Qdrant URL correct? (http://qdrant:6333)
- ✅ OpenRouter API key set? (OPENROUTER_API_KEY)
- ✅ Collection name correct? (meepleai)
- ✅ Embedding model configured? (text-embedding-3-small)

## Common Root Causes & Fixes

### Cause 1: Qdrant Down or Unhealthy

**Symptoms**:
- RAG errors: "Connection refused" or "Qdrant unavailable"
- All RAG requests failing (100% error rate)
- Qdrant health check failing
- Logs show: "Qdrant.Client.QdrantException"

**Investigation**:
```bash
# Check Qdrant status
docker compose ps qdrant

# Check Qdrant logs for errors
docker compose logs qdrant --tail 100

# Test Qdrant connectivity
curl http://localhost:6333/healthz
```

**Fix**:
```bash
# Option A: Restart Qdrant
docker compose restart qdrant
# Wait 15-30 seconds for startup

# Option B: Check Qdrant resources
docker stats --no-stream qdrant
# If memory at limit, increase memory allocation

# Option C: Verify collection exists
curl http://localhost:6333/collections
# If collection missing, re-index documents
```

**Verification**:
```bash
# Qdrant health check passes
curl http://localhost:6333/healthz

# Collection exists and has vectors
curl http://localhost:6333/collections/meepleai

# RAG endpoint works
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"How do I win?","gameId":1}'
```

**Prevention**:
- Monitor Qdrant health with Prometheus
- Set resource limits in docker-compose.yml
- Implement automatic collection recreation
- Add Qdrant health check to API health endpoint

**Resolution time**: 2-5 minutes

### Cause 2: OpenRouter API Issues

**Symptoms**:
- RAG errors: "OpenRouter API error" or "Embedding generation failed"
- Some RAG requests failing (partial error rate)
- Logs show: "401 Unauthorized" or "429 Too Many Requests"
- LLM generation or embedding errors

**Investigation**:
```bash
# Check OpenRouter API key
docker compose exec api printenv | grep OPENROUTER_API_KEY

# Test OpenRouter API directly
curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  https://openrouter.ai/api/v1/models

# Check API usage/quota (visit OpenRouter dashboard)
```

**Fix**:
```bash
# Option A: Verify API key is correct
# Check infra/env/api.env.dev
# Ensure OPENROUTER_API_KEY is set and valid
docker compose restart api

# Option B: If rate limited, wait or upgrade plan
# Check OpenRouter dashboard for quota
# Consider implementing exponential backoff

# Option C: If API down, check OpenRouter status page
# https://status.openrouter.ai
# Wait for service restoration or switch model
```

**Verification**:
```bash
# OpenRouter API responds
curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  https://openrouter.ai/api/v1/models

# RAG endpoint works (full pipeline)
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"Test question","gameId":1}'

# No more OpenRouter errors in logs
# Check HyperDX: openrouter AND error AND @timestamp:[now-5m TO now]
```

**Prevention**:
- Monitor OpenRouter API health
- Implement circuit breaker for API calls
- Add exponential backoff for rate limits
- Set up quota alerts (90% usage warning)

**Resolution time**: 2-10 minutes

### Cause 3: Collection Missing or Corrupted

**Symptoms**:
- RAG errors: "Collection 'meepleai' not found"
- All RAG requests failing immediately
- Qdrant is healthy but collection missing
- Logs show: "Collection does not exist"

**Investigation**:
```bash
# Check collections in Qdrant
curl http://localhost:6333/collections

# Check collection details
curl http://localhost:6333/collections/meepleai

# Check vector count
curl http://localhost:6333/collections/meepleai | jq '.result.vectors_count'
```

**Fix**:
```bash
# Option A: Re-create collection (if completely missing)
# Create collection via API endpoint or script
curl -X PUT http://localhost:6333/collections/meepleai \
  -H "Content-Type: application/json" \
  -d '{
    "vectors": {
      "size": 1536,
      "distance": "Cosine"
    }
  }'

# Then re-index all documents from PostgreSQL
# (Requires admin API endpoint or manual script)

# Option B: If corrupted, delete and recreate
curl -X DELETE http://localhost:6333/collections/meepleai
# Then follow Option A to recreate and re-index

# Option C: Restore from Qdrant backup (if available)
# Check backup location: /var/lib/qdrant/backups
# Restore collection from snapshot
```

**Verification**:
```bash
# Collection exists
curl http://localhost:6333/collections/meepleai | jq '.result.status'
# Should return: "green"

# Collection has vectors
curl http://localhost:6333/collections/meepleai | jq '.result.vectors_count'
# Should return: > 0

# Search works
curl -X POST http://localhost:6333/collections/meepleai/points/search \
  -H "Content-Type: application/json" \
  -d '{"vector":[0.1,0.2,...], "limit":5}'
```

**Prevention**:
- Implement automated collection backups
- Add collection existence check on startup
- Monitor vector count (alert if drops to 0)
- Add collection health check to API

**Resolution time**: 5-30 minutes (depending on re-indexing size)

### Cause 4: Query Expansion or RRF Issues

**Symptoms**:
- RAG errors: "Query expansion failed" or "RRF fusion error"
- Some queries work, others fail (query-dependent)
- Logs show: "Invalid query format" or "Reranking failed"
- Performance degradation (slow RAG responses)

**Investigation**:
```bash
# Check query expansion configuration
cat apps/api/src/Api/appsettings.json | grep -A 10 "QueryExpansion"

# Check RRF (Reciprocal Rank Fusion) settings
cat apps/api/src/Api/appsettings.json | grep -A 10 "RRF"

# Check logs for specific query errors
# HyperDX: query_expansion OR rrf AND error
```

**Fix**:
```bash
# Option A: Disable query expansion temporarily
# Edit appsettings.json:
# "Rag": {
#   "QueryExpansion": {
#     "Enabled": false
#   }
# }
docker compose restart api

# Option B: Adjust RRF weights (if fusion failing)
# Edit appsettings.json:
# "RRF": {
#   "VectorWeight": 0.7,
#   "KeywordWeight": 0.3
# }
docker compose restart api

# Option C: Check for invalid query characters
# Review query sanitization in code
# Add input validation for special characters
```

**Verification**:
```bash
# RAG endpoint works without errors
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"How do I move pieces?","gameId":1}'

# Check RAG response quality
# Verify citations are present
# Verify confidence scores are reasonable (>0.6)

# No query expansion errors in logs
# Check HyperDX: query_expansion AND error AND @timestamp:[now-5m TO now]
```

**Prevention**:
- Add comprehensive query input validation
- Monitor query expansion success rate
- Add unit tests for edge cases (special chars, long queries)
- Implement graceful fallback (disable expansion on error)

**Resolution time**: 5-15 minutes

### Cause 5: Low Quality Responses (Confidence Issues)

**Symptoms**:
- RAG errors: "Low confidence score" or "No citations found"
- RAG requests complete but quality validation fails
- Logs show: "Response confidence < 0.70"
- Users report poor answer quality

**Investigation**:
```bash
# Check quality metrics in dashboard
# http://localhost:3001/d/quality-metrics

# Check confidence thresholds
cat apps/api/src/Api/appsettings.json | grep -A 5 "QualityThreshold"

# Check recent RAG responses in HyperDX
# Filter: rag AND confidence AND @timestamp:[now-10m TO now]
# Look for low confidence patterns
```

**Fix**:
```bash
# Option A: Adjust quality threshold (temporarily)
# Edit appsettings.json:
# "Rag": {
#   "QualityThreshold": 0.60  # Reduced from 0.70
# }
docker compose restart api

# Option B: Improve retrieval (more context)
# Increase top_k (retrieve more vectors)
# Edit appsettings.json:
# "Rag": {
#   "TopK": 10  # Increased from 5
# }
docker compose restart api

# Option C: Improve embeddings quality
# Re-index with better chunking strategy
# (Requires re-indexing all documents)
```

**Verification**:
```bash
# RAG responses have acceptable confidence
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"How do I win?","gameId":1}' | jq '.confidence'
# Should return: > 0.60

# Check quality metrics dashboard
# http://localhost:3001/d/quality-metrics
# Verify overall confidence > 0.70

# No low quality errors in logs
# Check HyperDX: low_quality OR confidence AND error
```

**Prevention**:
- Monitor RAG quality metrics continuously
- Add A/B testing for retrieval parameters
- Implement feedback loop (user ratings)
- Regular re-indexing with improved strategies

**Resolution time**: 5-30 minutes (immediate for threshold change, longer for re-indexing)

## Mitigation Steps

### Immediate (< 2 minutes)

1. **Check Qdrant health**:
   ```bash
   docker compose ps qdrant
   curl http://localhost:6333/healthz
   ```

2. **Silence alert** (if actively working):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=RagErrorsDetected, duration=30m
   Comment: "Investigating Qdrant connectivity issue"
   ```

3. **Notify team**:
   ```
   #incidents: "🚨 RAG errors detected - Q&A functionality degraded - investigating Qdrant"
   ```

### Short-term (< 15 minutes)

1. **Identify root cause** (use investigation steps 1-7 above)

2. **Apply fix** (use appropriate fix from Common Root Causes section)

3. **Verify fix**:
   - RAG error rate drops to < 0.1/sec
   - Test Q&A endpoint manually (curl or UI)
   - Check HyperDX logs for no new RAG errors

4. **Update incident channel**:
   - Post resolution: "✅ Fixed by restarting Qdrant - RAG operational"
   - Or post ETA: "⏳ Re-indexing collection - ETA 20 min"

### Medium-term (< 1 hour)

1. **Monitor for recurrence**:
   - Watch AI/RAG Operations dashboard for 15-30 minutes
   - Ensure error rate stays < 0.1/sec
   - Test various query types (simple, complex, edge cases)

2. **Root cause analysis**:
   - Why did RAG fail? (Qdrant crash, API issue, config problem)
   - How to prevent recurrence? (monitoring, redundancy, better config)
   - Any patterns in failures? (specific queries, time of day, load)

3. **Create follow-up tasks**:
   - GitHub issue for permanent fix (if workaround applied)
   - Monitoring improvements (add new metrics, adjust thresholds)
   - Documentation updates (new troubleshooting steps)

## Escalation

### When to Escalate

Escalate if:
- ✅ Cannot identify root cause after 15 minutes
- ✅ Qdrant corruption requires DBA-level intervention
- ✅ OpenRouter API down for extended period (>1 hour)
- ✅ Re-indexing required but unsure of procedure
- ✅ Quality degradation persists despite fixes

### Escalation Contacts

**AI/ML team**:
- #ai-engineering Slack channel
- ML engineer (for embedding/quality issues)

**DevOps/Infrastructure**:
- #ops Slack channel
- On-call DevOps engineer (for Qdrant/infrastructure)

**Emergency contacts**:
- Team Lead: [name] - [phone]
- AI Lead: [name] - [phone]

## Prevention

### Monitoring

1. **RAG-specific metrics**:
   ```promql
   # RAG error rate (should be <0.1/sec)
   rate(meepleai_rag_errors_total[5m]) < 0.1

   # RAG latency P95 (should be <1.5s)
   histogram_quantile(0.95, rate(meepleai_rag_duration_bucket[5m])) < 1.5

   # RAG confidence (should be >0.70)
   avg(meepleai_rag_confidence) > 0.70
   ```

2. **Qdrant health checks**:
   - Check collection health every 5 minutes
   - Alert if vector count drops by >10%
   - Monitor Qdrant memory/CPU usage

3. **Quality monitoring**:
   - Track average confidence scores
   - Monitor citation extraction success rate
   - Alert on quality degradation trends

### Configuration

1. **Robust RAG config** (appsettings.json):
   ```json
   "Rag": {
     "RetryPolicy": {
       "MaxRetries": 3,
       "BackoffMultiplier": 2
     },
     "CircuitBreaker": {
       "Enabled": true,
       "FailureThreshold": 5,
       "TimeoutSeconds": 30
     },
     "Fallback": {
       "Enabled": true,
       "DisableQueryExpansion": true
     }
   }
   ```

2. **Qdrant resource limits**:
   ```yaml
   # docker-compose.yml
   qdrant:
     deploy:
       resources:
         limits:
           memory: 2G
           cpus: '2'
   ```

3. **OpenRouter rate limiting**:
   - Implement exponential backoff
   - Monitor quota usage (alert at 90%)
   - Cache embeddings aggressively

### Backup & Recovery

1. **Qdrant backups**:
   ```bash
   # Weekly backup script
   curl -X POST http://localhost:6333/collections/meepleai/snapshots
   # Store snapshot externally
   ```

2. **Re-indexing procedure**:
   - Document step-by-step re-indexing process
   - Test re-indexing in staging quarterly
   - Maintain re-indexing scripts in repository

3. **Quality baseline**:
   - Establish quality benchmarks (confidence, P@10)
   - Test against benchmark dataset monthly
   - Alert on quality regression

## Testing This Runbook

**Simulate RAG errors**:
```bash
# Stop Qdrant to simulate dependency failure
docker compose stop qdrant

# Try RAG endpoint (should fail)
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"Test","gameId":1}'

# Wait 3 minutes for alert to fire
# Follow runbook investigation steps

# Restart Qdrant
docker compose start qdrant

# Verify recovery
```

**Expected behavior**:
- RagErrorsDetected alert fires after 3 minutes
- Dashboard shows RAG error rate spike
- Alert auto-resolves when Qdrant restored

## Related Runbooks

- [AI Quality Low](./ai-quality-low.md): For overall AI quality degradation
- [Dependency Down](./dependency-down.md): For Qdrant outage scenarios
- [Slow Performance](./slow-performance.md): For RAG latency issues

## Related Dashboards

- [AI/RAG Operations](http://localhost:3001/d/ai-rag-operations): Primary RAG monitoring
- [Quality Metrics](http://localhost:3001/d/quality-metrics): RAG quality scores
- [Error Monitoring](http://localhost:3001/d/meepleai-error-monitoring): Error rate tracking

## Changelog

- **2025-12-08**: Initial version (Issue #706)
