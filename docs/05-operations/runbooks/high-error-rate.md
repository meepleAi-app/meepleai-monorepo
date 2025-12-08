# Runbook: High Error Rate

**Alert**: `HighErrorRate`
**Severity**: CRITICAL
**Threshold**: > 1 error/second for 2 minutes
**Expected Response Time**: Immediate (< 5 minutes)

## Symptoms

**Observable indicators when this alert fires:**
- Alert: "HighErrorRate" firing in Alertmanager
- Grafana dashboard shows error rate spike above 1 error/sec
- Users experiencing failures (500 Internal Server Error responses)
- Multiple requests failing across different endpoints
- HyperDX logs showing increased error-level entries

## Impact

**Effect on system and users:**
- **User Experience**: Users see error messages, cannot complete actions (login, game browsing, Q&A)
- **Data Integrity**: Failed operations may leave inconsistent state in database
- **Business Impact**: Revenue loss if payment/checkout endpoints affected, user churn risk
- **System Health**: Indicates major API malfunction, potential cascading failures

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/meepleai-error-monitoring
```

**Verification checklist**:
- ✅ Is error rate actually > 1 error/sec?
- ✅ Which endpoints are affected? (see "Top 10 Endpoints by Error Rate")
- ✅ What type of errors? (see "Status Code Distribution")
- ✅ Is this a false positive (temporary spike)?

**Prometheus Query**:
```promql
meepleai:api:error_rate:5m
```

**If false alarm**:
- Silence alert for 30 minutes
- Document why it's a false positive (short-lived spike < 2min)
- Create issue to adjust alert `for` duration if recurring

### 2. Identify Scope (1 minute)

**Questions to answer**:
1. **When did it start?** (check dashboard time range, note exact timestamp)
2. **Which endpoints affected?** (Top 10 Endpoints panel - all or specific routes?)
3. **All users or specific users?** (check HyperDX logs filtered by UserIds)
4. **Error types/patterns?** (500, 502, 503, 504? Exception types?)

**Prometheus queries**:
```promql
# Error rate by endpoint (top 5)
topk(5, sum by (http_route) (rate(meepleai_api_errors_total[5m])))

# Error rate by status code
sum by (http_status_code) (rate(meepleai_api_errors_total[5m]))

# Error rate by exception type
sum by (exception_type) (rate(meepleai_api_errors_total[5m]))
```

### 3. Check Recent Changes (1 minute)

**Recent deployment?**
```bash
# Check recent commits (last 2 hours)
git log --oneline --since="2 hours ago"

# Check deployed version
curl http://localhost:8080/health | jq '.version'
```

**Recent configuration changes?**
- Database connection strings (appsettings.json, env vars)
- Redis URL configuration
- Qdrant URL configuration
- Environment variables (.env files)
- Feature flags (SystemConfiguration context)

**Recent infrastructure changes?**
- Docker container restarts (check `docker compose ps` uptime)
- Volume deletions or corruption
- Network policy changes (firewall, Docker network)

### 3. Check Dependencies (2 minutes)

**Dependency Health Check**:
```bash
# PostgreSQL
docker compose ps postgres
docker compose logs postgres --tail 50
curl http://localhost:8080/health | jq '.checks.postgres'

# Redis
docker compose ps redis
export REDIS_PASS=$(cat infra/secrets/redis-password.txt)
docker compose exec redis redis-cli -a "$REDIS_PASS" --no-auth-warning ping
docker compose logs redis --tail 50

# Qdrant
docker compose ps qdrant
curl http://localhost:6333/healthz
docker compose logs qdrant --tail 50
```

**Dashboard dependency panel**:
- Navigate to Error Monitoring dashboard
- Scroll to "Dependency Health" section
- Verify all dependencies show green (Up)

### 5. Analyze Logs (2 minutes)

**HyperDX - Error/Warning logs**:
```
http://localhost:8180
Filter: level:error AND @timestamp:[now-10m TO now]
```

**Look for**:
- Common exception types (most frequent errors)
- Stack traces pointing to specific code files/lines
- Error messages indicating root cause (connection refused, timeout, null reference)
- Correlation patterns (same error across multiple users/endpoints?)

**Get correlation IDs**:
```
RequestId: 0HN6G8QJ9KL0M:00000001
```

**HyperDX - By endpoint filter**:
```
http_route:"<affected_endpoint>" AND level:error
Example: http_route:"/api/v1/games" AND level:error
```

### 6. Check Traces (1 minute)

**HyperDX - Distributed tracing**:
```
http://localhost:8180
Service: meepleai-api
Operation: <affected_endpoint>
Tags: error=true
```

**Look for**:
- Slow operations (> 1s duration, indicating bottlenecks)
- Failed dependency calls (Postgres, Redis, Qdrant)
- Timeouts in specific operations (database queries, external API calls)
- Unusual latency patterns (sudden spikes in P95/P99)

### 7. Check System Resources (1 minute)

**Prometheus resource queries**:
```promql
# CPU usage (should be < 80%)
rate(process_cpu_seconds_total{job="meepleai-api"}[5m]) * 100

# Memory usage (should be < 80%)
process_working_set_bytes{job="meepleai-api"} / process_memory_limit_bytes * 100

# Active requests (check for spikes indicating bottleneck)
http_server_active_requests

# Database connection pool usage (if metric available)
npgsql_connection_pool_active_connections / npgsql_connection_pool_size * 100
```

**Docker stats**:
```bash
docker compose stats --no-stream
```

## Common Root Causes & Fixes

### Cause 1: Recent Deployment Introduced Bug

**Symptoms**:
- Error spike immediately after deployment (< 5 minutes)
- Specific endpoint showing 100% error rate
- New exception type in logs (not seen before)
- Error rate correlates exactly with deployment timestamp

**Fix**:
```bash
# Option A: Quick rollback (RECOMMENDED)
git revert <commit-sha>
git push origin main
# Wait for CI/CD to redeploy (typically 5-10 minutes)

# Option B: Hot fix (if bug is trivial and fix is known)
# 1. Fix bug in code locally
# 2. Test fix locally
# 3. git commit -m "hotfix: fix <issue>"
# 4. git push origin main
# 5. Wait for CI/CD redeploy

# Option C: Rollback Docker image (if versioned images available)
docker compose down
docker compose pull meepleai/api:previous-stable-version
docker compose up -d
```

**Verification**:
```bash
# Error rate drops to baseline
curl http://localhost:9090/api/v1/query?query=meepleai:api:error_rate:5m

# Affected endpoint returns 200 OK
curl -f http://localhost:8080/api/v1/games

# No new errors in logs
# Check HyperDX: level:error AND @timestamp:[now-5m TO now]
```

**Prevention**:
- Improve test coverage for affected endpoint
- Add integration tests for critical paths
- Implement canary deployment (deploy to 10% traffic first)
- Monitor error rate for 10 minutes post-deployment

**Resolution time**: 5-15 minutes

### Cause 2: Database Connection Pool Exhausted

**Symptoms**:
- 500 errors across all endpoints (not isolated to one route)
- Error message: "Connection pool exhausted" or "Timeout acquiring connection from pool"
- Database health check failing (`/health` endpoint red)
- Logs show: "Npgsql.NpgsqlException: The connection pool has been exhausted"

**Fix**:
```bash
# Option A: Restart API (releases all connections)
docker compose restart api
# Wait 30 seconds for health check to pass

# Option B: Restart database (if database itself is unhealthy)
docker compose restart postgres
# Wait for database to fully start (check logs)
docker compose logs postgres --tail 20

# Option C: Increase connection pool size (if recurring issue)
# Edit apps/api/src/Api/appsettings.json:
# "ConnectionStrings": {
#   "Postgres": "...;Maximum Pool Size=100;Minimum Pool Size=10"
# }
docker compose restart api
```

**Verification**:
```bash
# API health check passes
curl http://localhost:8080/health | jq '.status'

# Error rate drops to zero
curl http://localhost:9090/api/v1/query?query=meepleai:api:error_rate:5m

# Test database query works
curl -f http://localhost:8080/api/v1/games?page=1&pageSize=10
```

**Prevention**:
- Add proper `using` statements for DbContext in all repository methods
- Ensure all database connections are disposed after use
- Monitor connection pool metrics (add custom Prometheus metric)
- Review code for long-running transactions holding connections

**Resolution time**: 2-5 minutes

### Cause 3: External Dependency Down (Qdrant, Redis)

**Symptoms**:
- Errors only on endpoints using RAG/caching functionality
- Error message: "Connection refused" or "Timeout connecting to <service>"
- Dependency health check shows red status in dashboard
- Logs show: "Qdrant.Client.QdrantException" or "StackExchange.Redis.RedisConnectionException"

**Fix**:
```bash
# Option A: Restart affected service
docker compose restart qdrant
docker compose restart redis

# Wait 10-15 seconds for service to initialize
docker compose ps

# Option B: Check if service is actually down
docker compose ps  # Look for "Exit X" or "Restarting"

# Check service logs for crash reason
docker compose logs qdrant --tail 100
docker compose logs redis --tail 100

# Option C: Nuclear option (restart all services)
docker compose down
docker compose up -d
# Wait for all health checks to pass (30-60 seconds)
```

**Verification**:
```bash
# Service is up and healthy
docker compose ps | grep "qdrant\|redis"

# Service responds to health check
curl http://localhost:6333/healthz  # Qdrant
docker compose exec redis redis-cli -a "$(docker compose exec -T redis cat /run/secrets/redis-password 2>/dev/null)" --no-auth-warning ping  # Redis

# Affected endpoints work again
curl -f http://localhost:8080/api/v1/chat  # RAG endpoint
```

**Prevention**:
- Enable Docker restart policy: `restart: unless-stopped` (already configured)
- Add resource limits to prevent OOM kills
- Monitor dependency health metrics (add alerts for dependency down)
- Implement circuit breaker pattern for external dependencies

**Resolution time**: 2-5 minutes

### Cause 4: Database Migration Failed

**Symptoms**:
- Errors immediately after deployment with DB migration
- Error message: "Invalid column name 'X'" or "Relation 'table_name' does not exist"
- All database queries failing (not just specific endpoints)
- Logs show: "Npgsql.PostgresException"

**Fix**:
```bash
# Check migration status
cd apps/api/src/Api
dotnet ef migrations list
# Look for (Pending) migrations

# Option A: Apply missing migration
dotnet ef database update
docker compose restart api

# Option B: Rollback to previous migration (if new migration is broken)
dotnet ef database update <PreviousMigrationName>
docker compose restart api

# Option C: Inspect database schema directly
docker compose exec postgres psql -U meeple -d meepleai
\dt  # List tables
\d <table_name>  # Describe specific table
```

**Verification**:
```bash
# Migration status shows all applied
dotnet ef migrations list

# Database schema matches expected
docker compose exec postgres psql -U meeple -d meepleai -c "\d users"

# API works with database
curl -f http://localhost:8080/api/v1/games
```

**Prevention**:
- Test migrations on copy of production database before deploying
- Add migration tests to CI/CD pipeline
- Create rollback migration for every forward migration
- Document schema changes in migration comments

**Resolution time**: 5-10 minutes

### Cause 5: Memory Leak / Resource Exhaustion

**Symptoms**:
- Error rate increasing gradually over hours (not sudden spike)
- Memory usage > 80% and climbing
- Eventually: OutOfMemoryException in logs
- Container may restart automatically (OOMKilled)

**Fix**:
```bash
# Immediate fix: Restart API to free memory
docker compose restart api
# This is a temporary fix, root cause must be investigated

# Check memory usage
docker compose stats --no-stream

# If container was OOMKilled (exit code 137):
docker compose ps api  # Check exit code
docker compose logs api --tail 100 | grep "OutOfMemory"
```

**Verification**:
```bash
# Error rate returns to baseline
curl http://localhost:9090/api/v1/query?query=meepleai:api:error_rate:5m

# Memory usage normalized (< 60%)
docker compose stats --no-stream

# API responds normally
curl -f http://localhost:8080/health
```

**Prevention**:
- Review code for memory leaks (unclosed streams, large object retention)
- Implement proper IDisposable pattern for all resources
- Monitor memory growth over time (add Prometheus alert for memory trends)
- Use memory profiler (dotMemory, PerfView) to identify leak sources
- Add load tests to catch memory leaks before production

**Resolution time**: 2 minutes (restart), hours to days (fix leak)

### Cause 6: High Traffic Spike (DDoS or Legitimate)

**Symptoms**:
- Error rate correlates with request rate spike (both increase together)
- Errors: "Too many requests" (429) or timeout errors (504)
- CPU/memory usage high (> 80%)
- Many requests queued or timing out

**Fix**:
```bash
# Option A: Enable rate limiting (if not already enabled)
# Check apps/api/src/Api/appsettings.json:
# "RateLimiting": { "Enabled": true, "RequestsPerMinute": 100 }
docker compose restart api

# Option B: Scale horizontally (if Docker Swarm/Kubernetes)
docker service scale meepleai_api=3

# Option C: Temporary block suspicious IPs (if DDoS attack)
# Add to nginx/firewall rules (if Traefik is configured)

# Option D: Emergency maintenance mode
# Return 503 Service Unavailable for non-critical endpoints
# Requires code change or feature flag
```

**Verification**:
```bash
# Request rate stabilized
curl http://localhost:9090/api/v1/query?query=rate(http_server_request_duration_count[5m])

# Error rate decreased
curl http://localhost:9090/api/v1/query?query=meepleai:api:error_rate:5m

# CPU/memory usage normalized
docker compose stats --no-stream
```

**Prevention**:
- Configure rate limiting (already in SystemConfiguration context)
- Set up CDN for static assets (if applicable)
- Implement request throttling per user/IP
- Add DDoS protection (Cloudflare, AWS Shield)
- Load test system to understand capacity limits

**Resolution time**: 10-30 minutes

## Mitigation Steps

### Immediate (< 2 minutes)

1. **Restart API** (if safe to do so and root cause unknown):
   ```bash
   docker compose restart api
   ```
   Wait 30 seconds for health check to pass.

2. **Silence alert** (if actively working on it):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=HighErrorRate, duration=30m
   Comment: "Investigating, restarted API, monitoring"
   ```

3. **Notify team**:
   ```
   #incidents: "🚨 HighErrorRate alert firing - error rate 2.5/sec on /api/v1/games - investigating"
   ```

### Short-term (< 10 minutes)

1. **Identify root cause** (use investigation steps 1-7 above systematically)

2. **Apply fix** (use appropriate fix from Common Root Causes section based on symptoms)

3. **Verify fix**:
   - Check error rate drops to < 0.1/sec in dashboard
   - Test affected endpoint manually with curl
   - Check HyperDX logs for no new errors in last 5 minutes

4. **Update incident channel**:
   - Post resolution: "✅ Fixed by restarting Redis - error rate back to baseline"
   - Post ETA if not yet resolved: "⏳ Identified DB migration issue - rolling back, ETA 5 min"

### Medium-term (< 1 hour)

1. **Monitor for recurrence**:
   - Watch dashboard Error Monitoring for 15-30 minutes
   - Ensure error rate stays < 0.1/sec (baseline)
   - Check for any new error patterns

2. **Resolve alert** (if auto-resolve didn't work):
   ```bash
   # Alert should auto-resolve when error rate < 1/sec for 2 minutes
   # If not, check Alertmanager UI: http://localhost:9093
   ```

3. **Post-incident tasks**:
   - Create GitHub issue for permanent fix (if rollback/workaround was applied)
   - Update runbook if new scenario encountered
   - Schedule post-mortem (if major incident >30 min outage)

## Escalation

### When to Escalate

Escalate to senior engineer or manager if:
- ✅ Cannot identify root cause after 10 minutes of investigation
- ✅ Fix attempts don't resolve issue (error rate still high after multiple fixes)
- ✅ Error rate increasing despite mitigation efforts
- ✅ Critical business endpoint affected (payments, authentication, core game features)
- ✅ Data integrity concerns (corrupted data, inconsistent state)

### Escalation Contacts

**On-call rotation** (check Grafana OnCall):
```
http://localhost:8082
```

**Slack channels**:
- **#incidents**: For active incident coordination and real-time updates
- **#engineering**: For technical questions and code-related issues
- **#ops**: For infrastructure and deployment issues

**Emergency contacts**:
- Team Lead: [name] - [phone]
- DevOps Lead: [name] - [phone]
- CTO: [name] - [phone] (critical incidents only, business impact)

## Post-Incident

### Immediate (< 1 hour after resolution)

1. **Document incident**:
   - Create GitHub issue with label `incident`
   - Template: `.github/ISSUE_TEMPLATE/incident-report.md`
   - Include: timeline, root cause, fix applied, user impact, duration

2. **Notify stakeholders**:
   - Post resolution in #incidents: "✅ HighErrorRate incident resolved - root cause: DB connection pool - duration: 15 min"
   - Update status page (if public-facing status page exists)

### Follow-up (within 48 hours)

1. **Post-mortem** (if major incident >30 min or high user impact):
   - Schedule 30-minute meeting with involved team members
   - Blameless culture: focus on systems, not people
   - Document: what happened, why it happened, how to prevent recurrence

2. **Action items**:
   - Create GitHub issues for preventive measures (fix root cause, improve monitoring)
   - Update monitoring/alerting if gaps found (e.g., add connection pool metric)
   - Update runbook with lessons learned (add new scenario or improve existing)

### Prevention

1. **Code improvements**:
   - Review code that caused issue (if deployment-related)
   - Add/improve error handling (try-catch, graceful degradation)
   - Add/improve input validation (prevent invalid data from causing errors)

2. **Testing**:
   - Add test for failure scenario (e.g., test with connection pool exhausted)
   - Add integration test for affected endpoint
   - Add load test if traffic spike caused issue

3. **Monitoring**:
   - Add custom metric if needed (e.g., connection pool usage)
   - Adjust alert threshold if false positive (e.g., increase `for` duration)
   - Add new alert if gap discovered (e.g., dependency health alert)

## Testing This Runbook

**Simulate alert condition**:
```bash
# Trigger multiple errors (requires test endpoint or load test)
for i in {1..200}; do
  curl -X POST http://localhost:8080/api/v1/test-error &
done

# Wait 2 minutes for alert to fire
# Follow runbook investigation steps 1-7
# Expected: Alert fires, dashboard shows spike, logs show errors
```

**Expected behavior**:
- Alert fires in Alertmanager after 2 minutes
- Dashboard shows error rate > 1/sec
- Logs show test errors with correlation IDs
- Trace shows test endpoint failures

**Cleanup**:
- Errors stop automatically after loop completes
- Alert auto-resolves after 2 minutes (when error rate drops < 1/sec)
- No manual cleanup needed

## Related Runbooks

- [Error Spike](./error-spike.md): For sudden 3x error rate increase detection
- [Dependency Down](./dependency-down.md): For PostgreSQL, Redis, Qdrant outages
- [Slow Performance](./slow-performance.md): For high latency issues that may precede errors

## Related Dashboards

- [Error Monitoring](http://localhost:3001/d/meepleai-error-monitoring): Primary dashboard for error metrics
- [API Performance](http://localhost:3001/d/api-performance): For response time and throughput metrics
- [Infrastructure](http://localhost:3001/d/infrastructure): For CPU, memory, resource metrics

## Changelog

- **2025-12-08**: Rewritten for uniform template compliance (Issue #706)
- **2025-10-16**: Initial version