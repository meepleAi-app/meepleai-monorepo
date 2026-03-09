# Slow Performance Runbook

**Alerts**: `SlowResponseTime` (warning), `SlowRagChatEndpoint` (warning)
**Dashboard**: [API Performance](http://localhost:3001/d/api-performance)

## Symptom

- `SlowResponseTime`: Global API P95 response time exceeds 3 seconds for 5+ minutes
- `SlowRagChatEndpoint`: RAG chat endpoint P95 exceeds 3 seconds for 5+ minutes (BGAI-082 SLO target)

## Impact

- **Users affected**: All users experience slow responses
- **RAG-specific**: Chat responses take too long, users may abandon conversations
- **SLO**: BGAI-082 target (P95 < 3s) violated — error budget consumption accelerates

## Diagnosis Steps

### 1. Identify slow endpoints

```bash
# Global P95 latency
curl -s 'http://localhost:9090/api/v1/query?query=meepleai:api:response_time_p95:5m' | jq '.data.result[0].value[1]'

# RAG chat P95 specifically
curl -s 'http://localhost:9090/api/v1/query?query=meepleai:rag:chat:p95:5m' | jq '.data.result[0].value[1]'

# Top 5 slowest endpoints
curl -s 'http://localhost:9090/api/v1/query?query=topk(5,histogram_quantile(0.95,rate(http_server_request_duration_bucket[5m])))' | jq '.data.result[] | {route: .metric.http_route, p95_seconds: .value[1]}'
```

### 2. Check RAG pipeline components

```bash
# Embedding service latency
curl -s 'http://localhost:9090/api/v1/query?query=histogram_quantile(0.95,rate(http_server_request_duration_bucket{http_route=~".*embed.*"}[5m]))' | jq '.data.result[0].value[1]'

# Embedding service health
curl -s http://localhost:8000/health

# Qdrant search latency (via API metrics)
curl -s 'http://localhost:9090/api/v1/query?query=rate(meepleai_vector_search_duration_sum[5m])/rate(meepleai_vector_search_duration_count[5m])' | jq '.data.result[0].value[1]'
```

### 3. Check database performance

```bash
# PostgreSQL active queries
pwsh -c "docker exec meepleai-postgres psql -U meepleai -c \"SELECT pid, now()-query_start AS duration, query FROM pg_stat_activity WHERE state='active' AND query NOT LIKE '%pg_stat%' ORDER BY duration DESC LIMIT 10;\""

# Connection pool usage
pwsh -c "docker exec meepleai-postgres psql -U meepleai -c \"SELECT count(*) AS total, state FROM pg_stat_activity GROUP BY state;\""
```

### 4. Check LLM provider latency

```bash
# Recent LLM call latency from logs
pwsh -c "docker logs meepleai-api --tail=200 2>&1" | grep -i "llm\|completion\|openrouter" | grep -i "duration\|latency\|time"

# Check if fallback model is being used (slower)
pwsh -c "docker logs meepleai-api --tail=200 2>&1" | grep -i "fallback\|downgrade"
```

### 5. Check resource saturation

```bash
# API container CPU and memory
pwsh -c "docker stats --no-stream --format 'table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}' meepleai-api"

# Redis cache hit rate (low = more DB queries = slower)
pwsh -c "docker exec meepleai-redis redis-cli info stats" | grep -E "keyspace_hits|keyspace_misses"
```

## Remediation

### Quick Fix

1. **If LLM provider slow**: Check OpenRouter status page. If degraded, Ollama fallback should auto-activate.

2. **If database slow**: Kill long-running queries
   ```bash
   pwsh -c "docker exec meepleai-postgres psql -U meepleai -c \"SELECT pg_cancel_backend(pid) FROM pg_stat_activity WHERE state='active' AND now()-query_start > interval '30 seconds' AND query NOT LIKE '%pg_stat%';\""
   ```

3. **If cache miss rate high**: Restart Redis (may have been flushed)
   ```bash
   pwsh -c "docker compose restart redis"
   ```

4. **If embedding service slow**: Restart (model may need reload)
   ```bash
   pwsh -c "docker compose restart embedding-service"
   ```

### Root Cause Fix

1. **Database**: Add missing indexes, optimize slow queries (check `pg_stat_statements`)
2. **Embedding service**: Check GPU/CPU load, model size vs available resources
3. **LLM**: Reduce prompt size, optimize context window, consider faster models
4. **Cache**: Review cache TTLs, ensure hot paths are cached
5. **Qdrant**: Check collection size, optimize search parameters (limit, score threshold)

## Escalation

- **Warning sustained >15 min**: Active investigation
- **P95 > 5s sustained >10 min**: Consider scaling or traffic shedding
- **All RAG requests timing out**: Check embedding service + Qdrant + LLM — likely compound failure

## Prevention

- k6 load tests in CI catch regressions before deploy
- Database query monitoring with slow query log
- Cache warming on deployment
- LLM provider redundancy (OpenRouter → Ollama fallback)
- Resource limits and autoscaling policies
