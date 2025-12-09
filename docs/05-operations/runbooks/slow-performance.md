# Runbook: Slow Performance

**Alert**: `SlowResponseTime`
**Severity**: WARNING
**Threshold**: P95 response time > 5 seconds for 5 minutes
**Expected Response Time**: < 30 minutes

## Symptoms

**Observable indicators when this alert fires:**
- Alert: "SlowResponseTime" firing in Alertmanager
- API response times degraded (P95 > 5 seconds)
- Users experiencing slow page loads and timeouts
- Dashboard shows latency spike in response time metrics
- Possible timeout errors (504 Gateway Timeout)

## Impact

**Effect on system and users:**
- **User Experience**: Slow page loads, poor responsiveness, frustration, potential timeouts
- **Data Integrity**: Generally not affected (operations complete, just slowly)
- **Business Impact**: Poor user experience, increased bounce rate, negative reviews
- **System Health**: Performance degradation may indicate resource exhaustion or bottleneck

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/api-performance
```

**Verification checklist**:
- ✅ Is P95 response time actually > 5 seconds?
- ✅ Which endpoints are slow? (check Top Slowest Endpoints panel)
- ✅ Is slowness affecting all requests or specific endpoints?
- ✅ When did slowness start? (correlate with events)

**Prometheus Query**:
```promql
meepleai:api:response_time_p95:5m
```

**If false alarm**:
- Silence alert for 30 minutes
- Document if slowness was temporary spike
- Create issue to adjust alert threshold if recurring

### 2. Identify Scope (1 minute)

**Questions to answer**:
1. **Which endpoints slow?** (all endpoints, specific routes)
2. **How slow?** (5s, 10s, 30s+ - severity of slowness)
3. **When did it start?** (deployment, config change, traffic spike)
4. **All users or specific users?** (geography, auth state, data size)

**Prometheus queries**:
```promql
# P95 response time by endpoint (top 5 slowest)
topk(5, meepleai:api:response_time_p95:5m)

# P99 response time (extreme slow requests)
histogram_quantile(0.99, rate(http_server_request_duration_bucket[5m]))

# Request rate (check if traffic spike correlates)
rate(http_server_request_duration_count[5m])
```

### 3. Check System Resources (2 minutes)

**Docker stats**:
```bash
docker compose stats --no-stream

# Look for:
# - CPU > 80% (CPU bottleneck)
# - Memory > 80% (memory pressure, possible swapping)
# - High I/O wait (disk bottleneck)
```

**Prometheus resource queries**:
```promql
# API CPU usage (should be <80%)
rate(process_cpu_seconds_total{job="meepleai-api"}[5m]) * 100

# API memory usage (should be <80%)
process_working_set_bytes{job="meepleai-api"} / process_memory_limit_bytes * 100

# Active HTTP requests (check for spike)
http_server_active_requests

# Thread pool queue length (if metric available)
thread_pool_queue_length
```

**System-level checks**:
```bash
# Check system load average
uptime

# Check disk I/O
iostat -x 1 5

# Check network connections
netstat -an | grep ESTABLISHED | wc -l
```

### 4. Check Database Performance (2 minutes)

**PostgreSQL slow queries**:
```bash
# Connect to database
docker compose exec postgres psql -U meeple -d meepleai

# Check slow queries (>1 second)
SELECT pid, now() - query_start AS duration, query
FROM pg_stat_activity
WHERE state = 'active'
  AND now() - query_start > interval '1 second'
ORDER BY duration DESC;

# Check database connections
SELECT count(*) FROM pg_stat_activity;

# Check locks/blocking queries
SELECT * FROM pg_locks WHERE NOT granted;
```

**PostgreSQL logs**:
```bash
# Check for slow query logs
docker compose logs postgres --tail 100 | grep -i "slow\|duration"

# Check for connection issues
docker compose logs postgres --tail 100 | grep -i "connection\|timeout"
```

### 5. Check Cache Performance (Redis) (1 minute)

**Redis performance**:
```bash
# Set password from Docker secret (run once)
export REDIS_PASS=$(cat infra/secrets/redis-password.txt)

# Check Redis stats
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning info stats | grep -E "hits|misses"

# Cache hit rate (should be >80%)
# hit_rate = hits / (hits + misses)

# Check slow commands
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning slowlog get 10

# Check memory usage
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning info memory | grep used_memory_human
```

**Redis latency**:
```bash
# Check Redis latency
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning --latency
# Typical: <1ms, Warning: >5ms, Critical: >10ms
```

### 6. Check External Dependencies (1 minute)

**OpenRouter API latency** (if slow):
```bash
# Check OpenRouter API response time
time curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  https://openrouter.ai/api/v1/models
# Should complete in <1 second

# Check HyperDX for OpenRouter slow logs
# Filter: openrouter AND (slow OR timeout)
```

**Qdrant performance**:
```bash
# Check Qdrant search latency
time curl -X POST http://localhost:6333/collections/meepleai/points/search \
  -H "Content-Type: application/json" \
  -d '{"vector":[0.1,0.2,...], "limit":5}'
# Should complete in <200ms

# Check Qdrant stats
curl http://localhost:6333/collections/meepleai | jq '.result.status'
```

### 7. Analyze Slow Traces (2 minutes)

**HyperDX - Slow traces**:
```
http://localhost:8180
Traces → Filter by duration > 5000ms
Service: meepleai-api
```

**Look for patterns**:
- Which operation is slow? (database query, external API, computation)
- Where is time spent? (DB: 80%, LLM: 15%, etc.)
- Are there N+1 queries? (multiple small queries instead of one)
- Is there blocking I/O? (synchronous operations)

**Check for slow operations**:
```
HyperDX Search:
duration_ms > 5000 AND @timestamp:[now-10m TO now]
Group by: operation_name
Sort by: avg(duration_ms) desc
```

## Common Root Causes & Fixes

### Cause 1: Database Slow Queries

**Symptoms**:
- Specific endpoints extremely slow (10-30+ seconds)
- Database CPU high (>70%)
- Traces show 80%+ time in database operations
- Logs show slow query warnings

**Investigation**:
```bash
# Find slow queries
docker compose exec postgres psql -U meeple -d meepleai -c "
SELECT pid, now() - query_start AS duration, query
FROM pg_stat_activity
WHERE state = 'active'
  AND now() - query_start > interval '1 second'
ORDER BY duration DESC;"

# Check for missing indexes
docker compose exec postgres psql -U meeple -d meepleai -c "
SELECT schemaname, tablename, attname, n_distinct, correlation
FROM pg_stats
WHERE schemaname = 'public'
ORDER BY correlation;"
```

**Fix**:
```bash
# Option A: Kill long-running queries (emergency)
docker compose exec postgres psql -U meeple -d meepleai -c "
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE state = 'active'
  AND now() - query_start > interval '30 seconds';"

# Option B: Add missing index (if identified)
# Example: Index on frequently queried column
docker compose exec postgres psql -U meeple -d meepleai -c "
CREATE INDEX CONCURRENTLY idx_games_name ON games(name);"

# Option C: Optimize query (code change)
# Review query in code, add .Include() for related data
# Example: .Include(g => g.Rules) to avoid N+1

# Option D: Increase database connection pool
# Edit appsettings.json:
# "ConnectionStrings": {
#   "Postgres": "...;Maximum Pool Size=50"
# }
docker compose restart api
```

**Verification**:
```bash
# No slow queries
docker compose exec postgres psql -U meeple -d meepleai -c "
SELECT count(*) FROM pg_stat_activity
WHERE state = 'active'
  AND now() - query_start > interval '1 second';"
# Should return: 0

# Response times normalized
curl http://localhost:9090/api/v1/query?query=meepleai:api:response_time_p95:5m
# Should return: <5 seconds

# Test affected endpoint
time curl -f http://localhost:8080/<slow-endpoint>
# Should complete in <2 seconds
```

**Prevention**:
- Add indexes proactively (analyze query plans)
- Monitor slow query log (enable pg_stat_statements)
- Use AsNoTracking() for read-only queries
- Implement query timeouts (command timeout: 30s)

**Resolution time**: 5-30 minutes

### Cause 2: Cache Miss Storm (Redis)

**Symptoms**:
- All endpoints slow (not just specific ones)
- Cache hit rate dropped significantly (<50%)
- Redis recently restarted or cache cleared
- Database load increased (all requests hit DB)

**Investigation**:
```bash
# Check cache hit rate
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning info stats | grep -E "keyspace_hits|keyspace_misses"
# Calculate: hits / (hits + misses)
# Should be >80%, problem if <50%

# Check cache size
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning info memory | grep used_memory_human

# Check key count
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning info keyspace
```

**Fix**:
```bash
# Option A: Wait for cache to warm up naturally
# Cache will rebuild over 10-30 minutes as requests come in
# Monitor cache hit rate improvement

# Option B: Pre-warm cache (if script available)
# Run cache warming script to populate frequently accessed data
# Example: curl http://localhost:8080/api/internal/warm-cache

# Option C: Increase cache TTL (if expiring too fast)
# Edit appsettings.json:
# "Caching": {
#   "DefaultTTLMinutes": 10  # Increased from 5
# }
docker compose restart api

# Option D: Increase Redis memory (if evicting aggressively)
# Edit docker-compose.yml:
# redis:
#   command: redis-server --maxmemory 512mb --maxmemory-policy allkeys-lru
docker compose restart redis
```

**Verification**:
```bash
# Cache hit rate improved (>70%)
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning info stats | grep -E "keyspace_hits|keyspace_misses"

# Response times improved
curl http://localhost:9090/api/v1/query?query=meepleai:api:response_time_p95:5m

# Database load decreased
docker compose stats --no-stream postgres
# CPU should be <50%
```

**Prevention**:
- Monitor cache hit rate continuously
- Alert on cache hit rate < 70%
- Implement cache warming on startup
- Use appropriate cache TTLs (balance freshness vs performance)

**Resolution time**: 10-30 minutes (cache warm-up time)

### Cause 3: High Traffic Spike

**Symptoms**:
- Slowness correlates with request rate spike
- All resources elevated (CPU, memory, database)
- Response times proportional to load (more load = slower)
- No specific slow query or bottleneck identified

**Investigation**:
```promql
# Check request rate spike
rate(http_server_request_duration_count[5m])

# Compare to baseline (1 hour ago)
rate(http_server_request_duration_count[5m] offset 1h)

# Check concurrent requests
http_server_active_requests

# Check by endpoint (identify hot endpoint)
topk(5, sum by (http_route) (rate(http_server_request_duration_count[5m])))
```

**Fix**:
```bash
# Option A: Scale horizontally (if supported)
docker service scale meepleai_api=3

# Option B: Enable rate limiting (if not already)
# Edit appsettings.json:
# "RateLimiting": {
#   "Enabled": true,
#   "RequestsPerMinute": 100
# }
docker compose restart api

# Option C: Add CDN/caching layer
# Configure Traefik or nginx caching for static assets
# Cache API responses where appropriate

# Option D: Optimize hot endpoint (if one endpoint causing load)
# Identify from Prometheus: which endpoint has highest rate
# Add caching, optimize query, or throttle that endpoint
```

**Verification**:
```bash
# Request rate stabilized or within capacity
curl http://localhost:9090/api/v1/query?query=rate(http_server_request_duration_count[5m])

# Response times improved
curl http://localhost:9090/api/v1/query?query=meepleai:api:response_time_p95:5m

# Resource usage normalized
docker compose stats --no-stream
# CPU <70%, Memory <70%
```

**Prevention**:
- Load test to understand capacity limits
- Implement auto-scaling (if infrastructure supports)
- Use CDN for static assets
- Add aggressive caching for read-heavy endpoints

**Resolution time**: 10-30 minutes

### Cause 4: External API Slow (OpenRouter)

**Symptoms**:
- RAG/chat endpoints very slow (10-30 seconds)
- Other endpoints normal speed
- Traces show most time in OpenRouter API calls
- Logs show OpenRouter timeouts or slow responses

**Investigation**:
```bash
# Test OpenRouter API latency
time curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"model":"anthropic/claude-3-sonnet","messages":[{"role":"user","content":"Hi"}]}' \
  https://openrouter.ai/api/v1/chat/completions
# Typical: 1-3 seconds, Warning: >5 seconds, Critical: >10 seconds

# Check OpenRouter status
curl https://openrouter.ai/api/v1/status

# Check HyperDX for OpenRouter latency
# Filter: openrouter AND duration_ms > 5000
```

**Fix**:
```bash
# Option A: Add timeout and fallback
# Edit appsettings.json:
# "OpenRouter": {
#   "TimeoutSeconds": 15,
#   "FallbackModel": "anthropic/claude-3-haiku"  # Faster model
# }
docker compose restart api

# Option B: Switch to faster model temporarily
# Edit appsettings.json:
# "OpenRouter": {
#   "Model": "anthropic/claude-3-haiku"  # Faster than Sonnet
# }
docker compose restart api

# Option C: Implement caching for LLM responses
# Cache common questions/answers
# Reduces OpenRouter API calls for repeated queries

# Option D: Check OpenRouter status page and wait
# If provider issue, wait for resolution
# https://status.openrouter.ai
```

**Verification**:
```bash
# OpenRouter API latency improved
time curl -H "Authorization: Bearer $OPENROUTER_API_KEY" \
  https://openrouter.ai/api/v1/models
# Should complete in <2 seconds

# Chat endpoint faster
time curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"Test","gameId":1}'
# Should complete in <5 seconds

# No timeout errors in logs
# Check HyperDX: openrouter AND timeout
```

**Prevention**:
- Monitor OpenRouter API latency
- Implement circuit breaker (fail fast if slow)
- Cache LLM responses aggressively
- Use faster models for non-critical queries

**Resolution time**: 5-15 minutes (config change) or longer (wait for provider)

### Cause 5: Memory Pressure / GC Pauses

**Symptoms**:
- Intermittent slowness (not constant)
- Response time spikes every few minutes
- Memory usage high (>80%)
- Logs show garbage collection (GC) events

**Investigation**:
```bash
# Check memory usage
docker compose stats --no-stream

# Check GC events in logs
docker compose logs api | grep -i "gc\|garbage"

# Check heap size
docker compose exec api curl http://localhost:8080/metrics | grep "dotnet_gc"
```

**Fix**:
```bash
# Option A: Increase container memory
# Edit docker-compose.yml:
# api:
#   deploy:
#     resources:
#       limits:
#         memory: 2G  # Increased from 1G
docker compose down
docker compose up -d

# Option B: Tune GC settings (environment variable)
docker compose up -d \
  -e DOTNET_gcServer=true \
  -e DOTNET_GCHeapCount=2 \
  api

# Option C: Fix memory leaks in code
# Profile application with dotMemory
# Identify objects not being disposed
# Add proper using statements and IDisposable

# Option D: Restart API (temporary relief)
docker compose restart api
# This clears memory but doesn't fix root cause
```

**Verification**:
```bash
# Memory usage stable (<70%)
docker compose stats --no-stream

# No GC pause spikes in logs
docker compose logs api --tail 100 | grep -i gc

# Response times consistent (no intermittent spikes)
# Monitor dashboard for 15 minutes
```

**Prevention**:
- Monitor memory usage trends (alert on >70%)
- Profile application for memory leaks
- Implement proper IDisposable pattern
- Use memory profiler (dotMemory) regularly

**Resolution time**: 2 minutes (restart) to hours/days (fix leaks)

## Mitigation Steps

### Immediate (< 5 minutes)

1. **Identify slow component** (database, cache, external API):
   ```bash
   docker compose stats --no-stream
   ```

2. **Silence alert** (if actively working):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=SlowResponseTime, duration=30m
   Comment: "Investigating database slow queries"
   ```

3. **Notify team**:
   ```
   #incidents: "🚨 SlowResponseTime alert - P95 latency 8.5s - investigating database"
   ```

### Short-term (< 30 minutes)

1. **Identify root cause** (use investigation steps 1-7 above)

2. **Apply fix** (use appropriate fix from Common Root Causes section)

3. **Verify fix**:
   - Response times return to < 5s P95
   - Test slow endpoints manually (curl, measure time)
   - Check HyperDX traces show improved latency

4. **Update incident channel**:
   - Post resolution: "✅ Fixed by adding database index - P95 now 2.1s"
   - Or post ETA: "⏳ Cache warming in progress - ETA 15 min"

### Medium-term (< 2 hours)

1. **Monitor for recurrence**:
   - Watch API Performance dashboard for 30-60 minutes
   - Ensure P95 stays < 3s (normal baseline)
   - Test under various load conditions

2. **Root cause analysis**:
   - Why was endpoint slow? (missing index, N+1 query, etc.)
   - How to prevent? (query optimization, caching, monitoring)
   - Any code changes needed? (optimization, refactoring)

3. **Create follow-up tasks**:
   - GitHub issue for performance optimization
   - Add performance tests (prevent regression)
   - Update monitoring (add new metrics, adjust thresholds)

## Escalation

### When to Escalate

Escalate if:
- ✅ Cannot identify root cause after 30 minutes
- ✅ Performance degradation persists despite fixes
- ✅ External dependency issue (OpenRouter, provider outage)
- ✅ Database optimization requires DBA expertise
- ✅ Architectural change needed (caching layer, load balancer)

### Escalation Contacts

**Performance team**:
- #engineering Slack channel
- Backend lead (for database/API optimization)

**DevOps/Infrastructure**:
- #ops Slack channel
- On-call DevOps (for resource scaling, infrastructure)

**Emergency contacts**:
- Team Lead: [name] - [phone]
- CTO: [name] - [phone] (critical only)

## Prevention

### Monitoring

1. **Performance metrics**:
   ```promql
   # P95 latency (should be <3s)
   histogram_quantile(0.95, rate(http_server_request_duration_bucket[5m])) < 3

   # Database query time P95 (should be <500ms)
   histogram_quantile(0.95, rate(db_query_duration_bucket[5m])) < 0.5

   # Cache hit rate (should be >80%)
   redis_keyspace_hits / (redis_keyspace_hits + redis_keyspace_misses) > 0.8
   ```

2. **Alert tuning**:
   - P95 > 5s = WARNING (current)
   - P95 > 10s = CRITICAL (add new alert)
   - Sustained P95 > 3s for 30 min = INFO (early warning)

3. **Performance testing**:
   - Run k6 load tests weekly
   - Establish performance baselines
   - Alert on regression (>20% slower than baseline)

### Configuration

1. **Optimized settings** (appsettings.json):
   ```json
   "Performance": {
     "DatabaseCommandTimeout": 30,
     "HttpClientTimeout": 15,
     "CacheTTLMinutes": 10,
     "MaxConcurrentRequests": 100
   }
   ```

2. **Database optimization**:
   - Enable connection pooling (already configured)
   - Use AsNoTracking() for read-only queries
   - Add indexes for frequent queries
   - Monitor pg_stat_statements

3. **Caching strategy**:
   - Cache read-heavy endpoints aggressively
   - Use appropriate TTLs (balance freshness vs performance)
   - Monitor cache hit rate continuously

### Code Quality

1. **Performance reviews**:
   - Review slow query patterns in code reviews
   - Flag N+1 queries in PR reviews
   - Require performance tests for new endpoints

2. **Profiling**:
   - Profile application quarterly (dotTrace, dotMemory)
   - Identify hot paths and optimize
   - Monitor allocations and GC pressure

3. **Best practices**:
   - Use async/await consistently
   - Avoid blocking I/O operations
   - Implement pagination for large result sets
   - Use HybridCache for frequently accessed data

## Testing This Runbook

**Simulate slow performance**:
```bash
# Option A: Stress test with k6
k6 run --vus 50 --duration 5m scripts/load-test.js
# This creates load that may trigger slowness

# Option B: Add artificial delay (test endpoint)
curl -X POST http://localhost:8080/api/v1/test-slow?delayMs=8000

# Wait 5 minutes for alert to fire
# Follow runbook investigation steps
```

**Expected behavior**:
- SlowResponseTime alert fires after 5 minutes
- Dashboard shows P95 > 5s
- Alert auto-resolves when performance normalizes

## Related Runbooks

- [High Error Rate](./high-error-rate.md): Slowness may cause timeouts/errors
- [High Memory Usage](./high-memory-usage.md): Memory pressure causes slowness
- [RAG Errors](./rag-errors.md): RAG-specific performance issues

## Related Dashboards

- [API Performance](http://localhost:3001/d/api-performance): Primary latency monitoring
- [Infrastructure](http://localhost:3001/d/infrastructure): Resource usage (CPU, memory, disk)
- [Error Monitoring](http://localhost:3001/d/meepleai-error-monitoring): Errors from timeouts

## Changelog

- **2025-12-08**: Initial version (Issue #706)