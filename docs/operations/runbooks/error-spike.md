# Error Spike Runbook

**Alert**: `ErrorSpike` (critical)
**Dashboard**: [Error Monitoring](http://localhost:3001/d/error-monitoring)

## Symptom

Current 500 error rate is 3x higher than the 1-hour historical average, sustained for 2+ minutes.

This differs from `HighErrorRate` — a spike means a sudden relative increase, even if absolute numbers are low.

## Impact

- **Users affected**: Likely a subset — specific endpoint or feature degraded
- **Pattern**: Sudden change suggests a trigger event (deployment, config change, dependency failure)
- **Cascading risk**: May escalate to `HighErrorRate` if not addressed

## Diagnosis Steps

### 1. Identify the spike trigger time

```bash
# Error spike ratio (current vs 1h average)
curl -s 'http://localhost:9090/api/v1/query?query=meepleai:api:error_spike_ratio' | jq '.data.result[0].value[1]'

# Error rate over last 30 minutes (detect when spike started)
curl -s 'http://localhost:9090/api/v1/query_range?query=meepleai:api:error_rate:5m&start='$(date -d '30 minutes ago' +%s)'&end='$(date +%s)'&step=60' | jq '.data.result[0].values[-5:]'
```

### 2. Correlate with events

```bash
# Recent container restarts
pwsh -c "docker ps --format 'table {{.Names}}\t{{.Status}}'" | grep -E "Restarting|seconds ago"

# Recent deployments (last hour)
git log --oneline --since="1 hour ago"

# Config changes
pwsh -c "docker compose config --hash '*'" 2>/dev/null
```

### 3. Identify affected endpoints

```bash
# Which endpoints are failing NOW vs 1 hour ago
curl -s 'http://localhost:9090/api/v1/query?query=topk(5,rate(http_server_request_duration_count{http_response_status_code="500"}[5m]))' | jq '.data.result[] | {route: .metric.http_route, rate: .value[1]}'
```

### 4. Check external dependencies status

```bash
# Embedding service
curl -s http://localhost:8000/health

# Reranker service
curl -s http://localhost:8003/health

# OpenRouter (if LLM errors)
pwsh -c "docker logs meepleai-api --tail=200 2>&1" | grep -i "openrouter\|429\|rate.limit"
```

## Remediation

### Quick Fix

1. **If deployment-triggered**: Rollback to previous version
2. **If dependency failure**: Check and restart the failed service
3. **If rate limiting from external API**: Fallback system should activate automatically — verify Ollama is running:
   ```bash
   pwsh -c "docker exec meepleai-ollama ollama list"
   ```

### Root Cause Fix

1. Correlate spike start time with deployment/change events
2. If no deployment: check for external dependency outage
3. If periodic spikes: investigate scheduled jobs or batch operations
4. Add circuit breaker if missing for the failing path

## Escalation

- **Spike resolves within 5 min**: Log as incident, no further action
- **Spike sustained >10 min**: Investigate actively
- **Spike escalates to `HighErrorRate`**: Follow [high-error-rate runbook](./high-error-rate.md)

## Prevention

- Canary deployments to detect errors before full rollout
- Gradual traffic ramp-up after deployments
- Circuit breakers with fallback on all external dependencies
- Historical baseline comparison in CI (k6 regression gate)
