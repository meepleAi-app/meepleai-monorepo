# Runbook: High Error Rate

**Alert**: `HighErrorRate`
**Severity**: CRITICAL
**Threshold**: > 1 error/second for 2 minutes
**Expected Response Time**: Immediate (< 5 minutes)

## Symptoms

- Alert: "HighErrorRate" firing in Alertmanager
- Grafana dashboard shows error rate spike
- Users experiencing failures (500 errors)
- Multiple requests failing across different endpoints

## Impact

- **User Experience**: Users see error messages, cannot complete actions
- **Data Integrity**: Failed operations may leave inconsistent state
- **Business**: Revenue loss if payment/checkout endpoints affected
- **Reputation**: User trust decreases with prolonged errors

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard**:
```json
http://localhost:3001/d/meepleai-error-monitoring
```

Check **Error Rate** panel:
- ✅ Is error rate actually > 1 error/sec?
- ✅ Which endpoints are affected? (see "Top 10 Endpoints")
- ✅ What type of errors? (see "Status Code Distribution")

**Prometheus**:
```json
http://localhost:9090/graph
Query: meepleai:api:error_rate:5m
```

If error rate is < 1/sec:
- **False alarm**: Silence alert, investigate alert tuning later
- **Resolution time**: 1 minute

### 2. Identify Scope (1 minute)

**Questions to answer**:
1. **When did it start?** (check dashboard time range)
2. **Which endpoints?** (Top 10 Endpoints panel)
3. **All users or specific users?** (check Seq logs for UserIds)
4. **Error types?** (500, 502, 503, 504?)

**Prometheus queries**:
```promql
# Error rate by endpoint
topk(5, sum by (http_route) (rate(meepleai_api_errors_total[5m])))

# Error rate by status code
sum by (http_status_code) (rate(meepleai_api_errors_total[5m]))

# Error rate by exception type
sum by (exception_type) (rate(meepleai_api_errors_total[5m]))
```

### 3. Check Recent Changes (1 minute)

**Recent deployment?**
```bash
# Check recent commits
git log --oneline -10

# Check deployed version (if available)
curl http://localhost:8080/health | jq '.version'
```

**Recent configuration changes?**
- Database connection strings
- Redis URL
- Qdrant URL
- Environment variables

**Recent infrastructure changes?**
- Docker container restarts
- Volume deletions
- Network policy changes

### 4. Check Dependencies (2 minutes)

**Database (PostgreSQL)**:
```bash
# Check status
docker compose ps postgres

# Check logs (last 50 lines)
docker compose logs postgres --tail 50

# Check connection from API
curl http://localhost:8080/health | jq '.checks.postgres'
```

**Cache (Redis)**:
```bash
# Check status
docker compose ps redis

# Test connection
docker compose exec redis redis-cli ping
# Should return: PONG

# Check logs
docker compose logs redis --tail 50
```

**Vector DB (Qdrant)**:
```bash
# Check status
docker compose ps qdrant

# Check health endpoint
curl http://localhost:6333/healthz

# Check logs
docker compose logs qdrant --tail 50
```

**Dashboard check**:
- Go to Error Monitoring dashboard
- Scroll to "Dependency Health" section
- All should show green (Up)

### 5. Analyze Logs (2 minutes)

**Seq - Error logs**:
```
http://localhost:8081
Filter: @Level = 'Error' and @Timestamp > DateTimeOffset.Now.AddMinutes(-10)
```

**Look for**:
- Common exception types
- Stack traces pointing to specific code
- Error messages indicating root cause

**Seq - By endpoint**:
```
RequestPath = '<affected_endpoint>' and @Level = 'Error'
Example: RequestPath = '/api/v1/games' and @Level = 'Error'
```sql
**Get correlation IDs** from error logs, example:
```
RequestId: 0HN6G8QJ9KL0M:00000001
```

### 6. Check Traces (1 minute)

**Jaeger**:
```
http://localhost:16686
Service: meepleai-api
Operation: <affected_endpoint>
Tags: error=true
```

**Look for**:
- Slow operations (> 1s)
- Failed dependency calls
- Timeouts in specific operations

### 7. Check System Resources (1 minute)

**Prometheus queries**:
```promql
# CPU usage (should be < 80%)
rate(process_cpu_seconds_total{job="meepleai-api"}[5m]) * 100

# Memory usage (should be < 80%)
process_working_set_bytes{job="meepleai-api"} / process_memory_limit_bytes * 100

# Active requests (spikes may indicate bottleneck)
http_server_active_requests

# Database connection pool
# (if available - would need custom metric)
```

**Docker stats**:
```bash
docker stats --no-stream api
```

## Common Root Causes & Fixes

### Cause 1: Recent Deployment Introduced Bug

**Symptoms**:
- Error spike immediately after deployment (< 5 minutes)
- Specific endpoint showing 100% error rate
- New exception type in logs

**Fix**:
```bash
# Option A: Rollback deployment
git revert <commit-sha>
git push
# Wait for CI/CD to redeploy

# Option B: Hot fix if minor
# 1. Fix bug in code
# 2. Commit & push
# 3. Deploy fix

# Option C: Rollback Docker image (if available)
docker compose down
docker compose up -d --force-recreate api
```json
**Resolution time**: 5-10 minutes

### Cause 2: Database Connection Pool Exhausted

**Symptoms**:
- 500 errors across all endpoints
- Error message: "Connection pool exhausted" or "Timeout acquiring connection"
- Database health check failing

**Fix**:
```bash
# Option A: Restart API (releases connections)
docker compose restart api

# Option B: Restart database (if corrupted)
docker compose restart postgres

# Option C: Increase connection pool size (if recurring)
# Edit appsettings.json or environment variable:
# ConnectionStrings__Postgres="...;Maximum Pool Size=50"
# (default is usually 100)
```json
**Prevention**:
- Add proper `using` statements for DbContext
- Ensure connections are disposed after use
- Monitor connection pool metrics

**Resolution time**: 2-5 minutes

### Cause 3: External Dependency Down (Qdrant, Redis)

**Symptoms**:
- Errors only on endpoints using RAG/caching
- Error message: "Connection refused" or "Timeout"
- Dependency health check red

**Fix**:
```bash
# Restart affected service
docker compose restart qdrant
docker compose restart redis

# Check if service is actually down
docker compose ps

# Check service logs for crash reason
docker compose logs qdrant --tail 100
docker compose logs redis --tail 100

# Nuclear option: restart all services
docker compose down
docker compose up -d
```json
**Resolution time**: 2-5 minutes

### Cause 4: Database Migration Failed

**Symptoms**:
- Errors immediately after deployment
- Error message: "Invalid column name" or "Table does not exist"
- Database queries failing

**Fix**:
```bash
# Check migration status
cd apps/api/src/Api
dotnet ef migrations list

# Apply missing migrations
dotnet ef database update

# Or rollback to previous migration
dotnet ef database update <PreviousMigrationName>

# Restart API
docker compose restart api
```json
**Resolution time**: 5-10 minutes

### Cause 5: Memory Leak / Resource Exhaustion

**Symptoms**:
- Error rate increasing gradually over hours
- Memory usage > 80%
- Eventually: OutOfMemoryException

**Immediate fix**:
```bash
# Restart API to free memory
docker compose restart api
```json
**Long-term investigation**:
- Check for memory leaks in code
- Review object disposal (IDisposable)
- Monitor memory growth over time
- Use memory profiler (dotMemory, PerfView)

**Resolution time**: 2 minutes (restart), hours to days (fix leak)

### Cause 6: High Traffic Spike (DDoS or legitimate)

**Symptoms**:
- Error rate correlates with request rate spike
- Errors: "Too many requests" or timeout errors
- CPU/memory usage high

**Fix**:
```bash
# Option A: Enable rate limiting (if not already)
# Check appsettings.json: RateLimiting section

# Option B: Scale horizontally (if Docker Swarm/K8s)
docker service scale meepleai_api=3

# Option C: Temporary: block suspicious IPs (if DDoS)
# Add to nginx/firewall rules

# Option D: Emergency: enable maintenance mode
# Return 503 Service Unavailable for non-critical endpoints
```json
**Resolution time**: 10-30 minutes

## Mitigation Steps

### Immediate (< 2 minutes)

1. **Restart API** (if safe to do so):
   ```bash
   docker compose restart api
   ```
   Wait 30 seconds for health check to pass.

2. **Silence alert** (if you're actively working on it):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=HighErrorRate, duration=30m
   Comment: "Investigating, restarting API"
   ```

3. **Notify team**:
   - Post in #incidents Slack channel
   - Brief summary: "High error rate on /api/v1/games, investigating"

### Short-term (< 10 minutes)

1. **Identify root cause** (use investigation steps above)
2. **Apply fix** (restart, rollback, or hot fix)
3. **Verify fix**:
   - Check error rate drops in dashboard
   - Test affected endpoint manually
   - Check Seq logs for no new errors

4. **Update incident channel**:
   - Post resolution: "Fixed by restarting Redis, errors resolved"
   - Post ETA if not yet resolved

### Medium-term (< 1 hour)

1. **Monitor for recurrence**:
   - Watch dashboard for 15-30 minutes
   - Ensure error rate stays < 0.1/sec

2. **Resolve alert** (if auto-resolve didn't work):
   ```bash
   # Alert should auto-resolve when condition clears
   # If not, check Alertmanager UI
   ```

3. **Post-incident tasks**:
   - Create GitHub issue for root cause fix (if needed)
   - Update runbook if new scenario encountered
   - Schedule post-mortem (if major incident)

## Escalation

### When to Escalate

Escalate to senior engineer or manager if:
- ✅ Cannot identify root cause after 10 minutes
- ✅ Fix attempts don't resolve issue
- ✅ Error rate increasing despite mitigation
- ✅ Critical business endpoint affected (payments, auth)
- ✅ Data integrity concerns (corrupted data)

### Escalation Contacts

**On-call rotation** (check Grafana OnCall):
```
http://localhost:8082
```json
**Slack channels**:
- **#incidents**: For active incident coordination
- **#engineering**: For technical questions
- **#ops**: For infrastructure issues

**Emergency contacts**:
- Team Lead: [name] - [phone]
- DevOps Lead: [name] - [phone]
- CTO: [name] - [phone] (critical only)

## Post-Incident

### Immediate (< 1 hour after resolution)

1. **Document incident**:
   - Create GitHub issue with label `incident`
   - Template: `.github/ISSUE_TEMPLATE/incident-report.md`
   - Include: timeline, root cause, fix, impact

2. **Notify stakeholders**:
   - Post resolution in #incidents
   - Update status page (if public-facing)

### Follow-up (within 48 hours)

1. **Post-mortem** (if major incident):
   - Schedule 30-minute meeting
   - Blameless culture: focus on systems, not people
   - Document: what happened, why, how to prevent

2. **Action items**:
   - Create GitHub issues for preventive measures
   - Update monitoring/alerting if gaps found
   - Update runbook with lessons learned

### Prevention

1. **Code review**:
   - Review code that caused issue
   - Add/improve error handling
   - Add/improve input validation

2. **Testing**:
   - Add test for failure scenario
   - Add integration test for affected endpoint
   - Add load test if traffic spike caused issue

3. **Monitoring**:
   - Add custom metric if needed
   - Adjust alert threshold if false positive
   - Add new alert if gap discovered

## Testing This Runbook

**Simulate high error rate**:
```bash
# Trigger multiple errors (requires test endpoint)
for i in {1..100}; do
  curl -X POST http://localhost:8080/api/v1/test-error &
done

# Wait 2 minutes for alert to fire
# Follow runbook steps to investigate and resolve
```

**Expected**:
- Alert fires in Alertmanager
- Dashboard shows error spike
- Logs show test errors
- Trace shows test endpoint failures

**Cleanup**:
- Errors stop automatically
- Alert auto-resolves after 5 minutes
- No action needed

## Changelog

- **2025-10-16**: Initial version
