# High Error Rate Runbook

**Alerts**: `HighErrorRate` (critical), `HighErrorRatio` (warning)
**Dashboard**: [Error Monitoring](http://localhost:3001/d/error-monitoring)

## Symptom

- `HighErrorRate`: API error rate exceeds 1 error/sec for 2+ minutes
- `HighErrorRatio`: More than 5% of all requests return 5xx for 5+ minutes

## Impact

- **Users affected**: All API consumers — requests failing with 500 errors
- **Severity**: Critical if sustained — indicates systemic failure
- **Cascading risk**: Frontend shows error states, chat/RAG unavailable

## Diagnosis Steps

### 1. Check API health and recent errors

```bash
# API health check
curl -s http://localhost:8080/health/ready | jq .

# Recent API logs (last 100 lines, errors only)
pwsh -c "docker logs meepleai-api --tail=100 2>&1" | grep -i "error\|exception\|500"

# Error rate in Prometheus
curl -s 'http://localhost:9090/api/v1/query?query=meepleai:api:error_rate:5m' | jq '.data.result[0].value[1]'
```

### 2. Identify error pattern

```bash
# Top error endpoints
curl -s 'http://localhost:9090/api/v1/query?query=topk(5,rate(http_server_request_duration_count{http_response_status_code=~"5.."}[5m]))' | jq '.data.result[] | {endpoint: .metric.http_route, rate: .value[1]}'

# Check if a specific endpoint is causing most errors
pwsh -c "docker logs meepleai-api --tail=500 2>&1" | grep "StatusCode: 500" | awk '{print $NF}' | sort | uniq -c | sort -rn | head -10
```

### 3. Check dependencies

```bash
# PostgreSQL
pwsh -c "docker exec meepleai-postgres pg_isready"

# Redis
pwsh -c "docker exec meepleai-redis redis-cli ping"

# Qdrant
curl -s http://localhost:6333/healthz
```

### 4. Check recent deployments

```bash
# Last 5 commits deployed
git log --oneline -5

# Container restart times
pwsh -c "docker ps --format 'table {{.Names}}\t{{.Status}}'" | grep meepleai
```

## Remediation

### Quick Fix

1. **If dependency down**: Restart the failed dependency
   ```bash
   pwsh -c "docker compose restart postgres"  # or redis, qdrant
   ```

2. **If recent deployment caused it**: Rollback
   ```bash
   pwsh -c "docker compose pull api && docker compose up -d api"
   ```

3. **If memory/resource exhaustion**: Restart API
   ```bash
   pwsh -c "docker compose restart api"
   ```

### Root Cause Fix

1. Check error logs for stack traces — identify the failing code path
2. If database-related: Check connection pool exhaustion, slow queries
3. If external API: Check OpenRouter/embedding service status
4. If code bug: Hotfix branch → test → deploy

## Escalation

- **Warning (>5% errors)**: Monitor for 15 minutes, investigate if not self-resolving
- **Critical (>1 err/sec)**: Immediate investigation required
- **Sustained >30 min**: Escalate to on-call engineer, consider rollback

## Prevention

- Pre-merge validation: `dotnet test` + k6 smoke test in CI
- Circuit breakers on external dependencies
- Rate limiting to prevent cascade failures
- Health check endpoints monitored every 30s
