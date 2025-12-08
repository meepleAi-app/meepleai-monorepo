# Runbook: Error Spike

**Alert**: `ErrorSpike`
**Severity**: CRITICAL
**Threshold**: Current error rate > 3x historical 1-hour average
**Expected Response Time**: Immediate (< 5 minutes)

## Symptoms

**Observable indicators when this alert fires:**
- Alert: "ErrorSpike" firing in Alertmanager
- Sudden increase in error rate (3x+ normal baseline)
- May occur even if absolute error rate is low (e.g., 0.3/sec → 0.9/sec triggers alert)
- Often indicates recent change (deployment, config, external event)
- Dashboard shows clear spike in error trend graph

## Impact

**Effect on system and users:**
- **User Experience**: Early warning before major outage, potential degraded service quality
- **Data Integrity**: New bugs may corrupt data if not caught early
- **Business Impact**: Deployment validation failure, user trust erosion if unchecked
- **System Health**: Indicates recent change introduced errors, requires immediate investigation

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/meepleai-error-monitoring
```

**Verification checklist**:
- ✅ Visual spike visible in Error Trend panel?
- ✅ When did spike start? (note exact timestamp)
- ✅ Is spike ongoing or already resolved?
- ✅ What is current spike ratio vs baseline?

**Prometheus Query**:
```promql
meepleai:api:error_spike_ratio
```

**Interpretation**:
- Result > 3 confirms spike (e.g., 4.2 means 4.2x baseline)
- Compare current error rate vs 1-hour historical average
- Spike may be significant even if absolute rate is low

**If false alarm**:
- Silence alert for 30 minutes
- Document if baseline was unusually low (causing false spike detection)
- Create issue to adjust alert threshold or averaging window

### 2. Identify Trigger Event (1 minute)

**Questions to answer**:
1. **When did spike start?** (check dashboard timeline, correlate with events)
2. **What changed?** (deployment, config, infrastructure, external dependency)
3. **Which components affected?** (specific endpoints, all routes, specific users)
4. **Error patterns?** (new exception types, known errors increasing)

**Check timeline of events**:

**Recent deployment?**
```bash
# Recent commits (last 2 hours, correlate with spike timestamp)
git log --oneline --since="2 hours ago"

# Check deployment time vs spike start time
# Compare timestamps in dashboard
```

**Configuration change?**
```bash
# Check git history for config files (last 2 hours)
git log --oneline --since="2 hours ago" -- \
  infra/env/*.env.dev \
  apps/api/src/Api/appsettings.json \
  docker-compose.yml
```

**Infrastructure change?**
```bash
# Check Docker events (last 2 hours)
docker events --since 2h --until now

# Check container restarts (look for recent "Up X minutes")
docker compose ps
```

**External event?**
- Marketing campaign launched? (traffic spike causing errors)
- External API changed? (integration broken, provider API update)
- DDoS attack? (malicious traffic pattern)
- Database maintenance window? (performance degradation)

### 3. Identify Affected Area (1 minute)

**Which endpoints affected?**
```
Dashboard → Top 10 Endpoints by Error Rate panel
```

**Which error types?**
```
Dashboard → Error Distribution by Status Code
Dashboard → Exception Type Distribution
```

**Which users?**
```
HyperDX: level:error AND @timestamp:[now-10m TO now]
Group by: UserId
# Check if specific users or all users affected
```

**Prometheus queries for detailed breakdown**:
```promql
# Error rate by endpoint (top 5)
topk(5, sum by (http_route) (rate(meepleai_api_errors_total[5m])))

# Error rate by status code
sum by (http_status_code) (rate(meepleai_api_errors_total[5m]))

# Error rate by exception type
sum by (exception_type) (rate(meepleai_api_errors_total[5m]))
```

### 4. Compare Before/After (2 minutes)

**Error rate before spike**:
```promql
# Prometheus - 2 hours ago (before spike)
meepleai:api:error_rate:5m offset 2h
```

**Error rate during spike**:
```promql
# Prometheus - now (during spike)
meepleai:api:error_rate:5m
```

**What changed between before/after?**
- New exception types appearing?
- Different endpoints affected?
- Different error categories (validation, system, dependency)?
- User distribution changed (new users, specific cohort)?

### 5. Analyze Recent Code Changes (2 minutes)

**If deployment triggered spike**:

```bash
# Get diff of recent deployment
git diff HEAD~1 HEAD

# Focus review on:
# - New endpoints (potential bugs)
# - Modified business logic (regression risks)
# - Database queries (performance, correctness)
# - External API calls (integration changes)
# - Configuration changes (connection strings, feature flags)
```

**Quick code review checklist**:
- Any new exceptions thrown? (check for `throw new` statements)
- Any new validation logic? (may reject previously valid input)
- Any changes to error handling? (catch blocks, error propagation)
- Any database schema changes? (migrations may have issues)
- Any dependency version updates? (breaking changes in libraries)

### 6. Check Logs for New Errors (2 minutes)

**HyperDX - New exception types**:
```
http://localhost:8180
Filter: level:error AND @timestamp:[now-10m TO now]
```

**Group by Exception type**:
```
exception_type is not null
Group by: exception_type
Sort by: count desc
```

**Look for patterns**:
- Same error on multiple endpoints? (systemic issue)
- Same error for specific user? (user-specific data problem)
- Same error at specific time? (hourly cron job failing?)
- Correlation with specific input data? (validation edge case)

**HyperDX - Compare error types before/after spike**:
```
# Before spike (2 hours ago)
level:error AND @timestamp:[now-2h-10m TO now-2h]

# During spike (now)
level:error AND @timestamp:[now-10m TO now]

# Identify NEW error types that appeared with spike
```

## Common Root Causes & Fixes

### Cause 1: New Deployment Introduced Bug

**Symptoms**:
- Spike starts 0-5 minutes after deployment
- New exception type in logs (not seen before spike)
- Specific endpoint has 100% error rate (all requests fail)
- Error rate correlates precisely with deployment timestamp

**Investigation**:
```bash
# Find commit that caused spike (compare timestamp)
git log --oneline --since="30 minutes ago"

# Review specific commit changes
git show <commit-sha>

# Check which files were modified
git diff HEAD~1 HEAD --name-only
```

**Fix**:
```bash
# Option A: Quick rollback (RECOMMENDED for critical issues)
git revert <commit-sha>
git push origin main
# Wait for CI/CD redeploy (usually 5-10 min)

# Option B: Hot fix (if trivial bug and fix is obvious)
# 1. Fix bug locally (e.g., typo, logic error)
# 2. Test fix locally: dotnet test
# 3. git commit -m "hotfix: fix <specific issue>"
# 4. git push origin main
# 5. Wait for CI/CD redeploy

# Option C: Rollback Docker image (if versioned images)
docker compose down
docker compose pull meepleai/api:previous-stable-tag
docker compose up -d
```

**Verification**:
```bash
# Error rate drops to baseline within 1-2 minutes
curl http://localhost:9090/api/v1/query?query=meepleai:api:error_rate:5m

# Affected endpoint returns 200 OK
curl -f http://localhost:8080/<affected-endpoint>

# No new errors in HyperDX
# Check: level:error AND @timestamp:[now-5m TO now]
```

**Prevention**:
- Add test for failure scenario (e.g., test edge case that triggered bug)
- Improve code review process (catch bugs before merge)
- Implement canary deployment (deploy to 10% traffic first, monitor)
- Add smoke tests post-deployment (verify critical paths work)

**Resolution time**: 5-15 minutes

### Cause 2: Database Migration Issue

**Symptoms**:
- Spike after deployment with DB migration
- Error: "Invalid column name 'X'" or "Table 'Y' does not exist"
- All database operations failing (not isolated to one endpoint)
- Logs show: "Npgsql.PostgresException" with schema-related errors

**Investigation**:
```bash
# Check migration status (are all migrations applied?)
cd apps/api/src/Api
dotnet ef migrations list
# Look for (Pending) migrations that failed to apply

# Check database schema matches expectations
docker compose exec postgres psql -U meeple -d meepleai
\dt  # List all tables
\d <table_name>  # Describe specific table structure
```

**Fix**:
```bash
# Option A: Apply missing migration (if migration not applied)
dotnet ef database update
docker compose restart api
# Wait for API to start with correct schema

# Option B: Rollback migration (if new migration is broken)
dotnet ef database update <PreviousMigrationName>
docker compose restart api
# Then fix migration code and redeploy

# Option C: Manual schema fix (emergency only)
docker compose exec postgres psql -U meeple -d meepleai
# Run SQL to fix schema (add missing column, table, etc.)
# Then restart API
```

**Verification**:
```bash
# Migration status shows all applied
dotnet ef migrations list | grep -v "(Pending)"

# Database schema correct
docker compose exec postgres psql -U meeple -d meepleai -c "\d <table_name>"

# API works with database
curl -f http://localhost:8080/api/v1/games
```

**Prevention**:
- Test migrations on copy of production database before deploying
- Add migration tests to CI/CD pipeline (verify schema after migration)
- Create rollback migration for every forward migration
- Document breaking changes in migration comments

**Resolution time**: 5-10 minutes

### Cause 3: Configuration Change

**Symptoms**:
- Spike after config file change (appsettings.json, .env files)
- Error: "Connection string invalid" or "Missing required configuration key"
- Specific feature failing (e.g., all Redis operations, all Qdrant operations)
- Logs show: "Configuration value '<key>' not found"

**Investigation**:
```bash
# Check recent config changes (last 1 hour)
git log --oneline --since="1 hour ago" -- \
  infra/env/*.env.dev \
  apps/api/src/Api/appsettings.json

# Show diff of config changes
git diff HEAD~1 HEAD -- infra/env/api.env.dev
git diff HEAD~1 HEAD -- apps/api/src/Api/appsettings.json
```

**Fix**:
```bash
# Option A: Revert config change (safest)
git checkout HEAD~1 -- infra/env/api.env.dev
git commit -m "revert: rollback config change causing errors"
git push origin main
# Wait for CI/CD redeploy

# Option B: Fix config manually (if you know correct value)
vi infra/env/api.env.dev
# Correct the incorrect value
docker compose restart api

# Option C: Use environment variable override (temporary)
docker compose up -d \
  -e REDIS_URL=redis:6379 \
  -e QDRANT_URL=http://qdrant:6333 \
  api
```

**Verification**:
```bash
# API logs show successful connection
docker compose logs api | grep -i "connected\|initialized"

# Test affected feature manually
curl -f http://localhost:8080/<affected-endpoint>

# Check health endpoint shows all dependencies up
curl http://localhost:8080/health | jq '.checks'
```

**Prevention**:
- Validate config files before deploying (schema validation)
- Use environment-specific config files (dev, staging, prod)
- Add integration tests that verify config is loaded correctly
- Document required config values and their formats

**Resolution time**: 2-5 minutes

### Cause 4: External Dependency Changed

**Symptoms**:
- Spike without any internal changes (no deployment, no config change)
- Error: "HTTP 400 Bad Request" or "Unexpected response format from <external-service>"
- All calls to external API failing (100% error rate for integration)
- Logs show: "HttpRequestException" or API-specific error responses

**Investigation**:
```bash
# Check external API health directly
curl -i https://api.external-service.com/health

# Check external API documentation for recent changes
# Visit provider's status page, changelog, or developer portal

# Check API version in use
grep -r "external-service" apps/api/src/Api/
# Look for version numbers in API client code
```

**Fix**:
```bash
# Option A: Update integration code (if API contract changed)
# 1. Review external API docs for breaking changes
# 2. Update code to match new API contract (request/response format)
# 3. Test integration locally
# 4. Deploy fix

# Option B: Temporary fallback (disable feature gracefully)
# 1. Add feature flag to disable external API integration
# 2. Return cached/default values or graceful degradation
# 3. Fix integration properly later when ready

# Option C: Contact external provider (if unexpected change)
# If their API changed without notice, report bug
# Request rollback or advance notice for future changes
```

**Verification**:
```bash
# External API responds correctly
curl https://api.external-service.com/<test-endpoint>

# Integration endpoint works
curl -f http://localhost:8080/<integration-endpoint>

# No more errors in logs
# Check HyperDX: level:error AND "external-service" AND @timestamp:[now-5m TO now]
```

**Prevention**:
- Pin external API versions (use versioned endpoints like /v1/)
- Add integration tests with external API mocks (catch contract changes)
- Monitor external API status page (subscribe to notifications)
- Implement circuit breaker pattern (fail gracefully when external API down)

**Resolution time**: 15 minutes to several hours (depends on external provider)

### Cause 5: Traffic Spike (Legitimate or Attack)

**Symptoms**:
- Spike correlates with request rate spike (both increase together)
- No code/config changes recently
- Timeout errors (504) or resource exhaustion errors (503)
- CPU/memory usage elevated (> 70%)

**Investigation**:
```promql
# Prometheus - Request rate (check for spike)
rate(http_server_request_duration_count[5m])

# Compare to baseline (1 hour ago)
rate(http_server_request_duration_count[5m] offset 1h)

# Request rate by endpoint (identify which endpoint receiving traffic)
topk(5, sum by (http_route) (rate(http_server_request_duration_count[5m])))
```

**Fix**:
```bash
# Option A: Scale horizontally (if Kubernetes/Docker Swarm)
kubectl scale deployment meepleai-api --replicas=3
# Or: docker service scale meepleai_api=3

# Option B: Enable rate limiting (if not already enabled)
# Check apps/api/src/Api/appsettings.json:
# "RateLimiting": { "Enabled": true, "RequestsPerMinute": 100 }
docker compose restart api

# Option C: Block suspicious IPs (if DDoS attack detected)
# Add to nginx/Traefik config or firewall rules
# Identify suspicious IPs from access logs first

# Option D: Enable maintenance mode (emergency)
# Add feature flag to return 503 for non-critical endpoints
# Requires code change or runtime feature flag
```

**Verification**:
```bash
# Request rate stabilized or within capacity
curl http://localhost:9090/api/v1/query?query=rate(http_server_request_duration_count[5m])

# Error rate decreased to baseline
curl http://localhost:9090/api/v1/query?query=meepleai:api:error_rate:5m

# CPU/memory usage normalized (< 60%)
docker compose stats --no-stream
```

**Prevention**:
- Configure rate limiting (already available in SystemConfiguration)
- Set up CDN for static assets (reduce load on API)
- Implement request throttling per user/IP (prevent abuse)
- Add DDoS protection service (Cloudflare, AWS Shield)
- Load test system to understand capacity limits (k6 load tests)

**Resolution time**: 10-30 minutes

### Cause 6: Scheduled Job / Cron Failure

**Symptoms**:
- Spike occurs at regular intervals (e.g., every hour, every day)
- Specific endpoint called from background job showing errors
- Error in background task logs (IHostedService, Hangfire, n8n)
- Spike duration matches job execution time

**Investigation**:
```bash
# Check for scheduled tasks in code
# Look for: IHostedService implementations, Hangfire jobs, n8n workflows

# Check n8n workflows (if using automation)
curl http://localhost:5678/workflows
# Review workflow executions and error logs

# Check cron job logs
docker compose logs api | grep -i "background\|scheduled\|cron"
```

**Fix**:
```bash
# Option A: Disable problematic job (temporary)
# Update configuration to disable job
# Or: Comment out job registration in code
# Restart API
docker compose restart api

# Option B: Fix job logic (permanent)
# Review and fix the failing scheduled task
# Test job manually before re-enabling:
# curl -X POST http://localhost:8080/api/internal/trigger-job

# Option C: Adjust job timing (if causing load issues)
# Change cron schedule to off-peak hours
# Reduce frequency if too aggressive (e.g., hourly → daily)
```

**Verification**:
```bash
# Job no longer causes errors
docker compose logs api | grep -i "scheduled\|cron" | grep -i "error"

# Next job execution completes successfully
# Wait for next scheduled run, monitor dashboard

# Error spike does not recur at next interval
```

**Prevention**:
- Add error handling in background jobs (try-catch, retry logic)
- Add monitoring for background job health (custom metrics)
- Test scheduled jobs in staging before production
- Implement exponential backoff for job retries

**Resolution time**: 5-30 minutes

## Mitigation Steps

### Immediate (< 2 minutes)

1. **Identify trigger** (deployment, config, external):
   ```bash
   # Check recent commits
   git log --oneline --since="30 minutes ago"

   # Check Docker events
   docker events --since 30m
   ```

2. **Silence alert** (if actively working on it):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=ErrorSpike, duration=30m
   Comment: "Investigating deployment rollback for v1.2.0"
   ```

3. **Notify team**:
   ```
   #incidents: "🚨 ErrorSpike alert - error rate 4.2x baseline after v1.2.0 deployment - rolling back"
   ```

### Short-term (< 10 minutes)

1. **Rollback** (if deployment caused spike):
   ```bash
   git revert <commit-sha>
   git push origin main
   # Wait for CI/CD redeploy
   ```

2. **Or apply fix** (if trivial bug identified):
   ```bash
   # Make fix, test locally
   git commit -m "hotfix: <specific fix>"
   git push origin main
   ```

3. **Verify fix**:
   - Error rate returns to baseline (< 3x average)
   - No new errors in HyperDX logs (last 5 min)
   - Affected endpoints respond correctly (curl test)

4. **Update incident channel**:
   - Post resolution: "✅ Rolled back v1.2.0 - error rate back to baseline"
   - Or post ETA: "⏳ Applying hotfix for validation bug - ETA 5 min"

### Medium-term (< 1 hour)

1. **Monitor for recurrence**:
   - Watch dashboard Error Monitoring for 15-30 minutes
   - Ensure spike ratio stays < 3 (no new spike)
   - Check for any new error patterns emerging

2. **Root cause analysis**:
   - Why did deployment cause errors? (missed test case, code review gap)
   - Why didn't tests catch this? (test coverage gap, integration test missing)
   - How to prevent in future? (improve testing, code review, deployment process)

3. **Create follow-up tasks**:
   - GitHub issue for proper fix (if rolled back temporarily)
   - GitHub issue for test coverage improvement
   - GitHub issue for alert tuning (if false positive)

## Prevention

### Before Deployment

1. **Canary deployment** (if infrastructure supports):
   - Deploy to 10% of traffic first
   - Monitor error rate for 10 minutes
   - If stable (< 3x baseline), deploy to 100%
   - If error spike, rollback canary immediately

2. **Smoke tests** after deployment (automated):
   ```bash
   # Test critical endpoints post-deploy
   curl -f http://localhost:8080/api/v1/health
   curl -f http://localhost:8080/api/v1/games?page=1
   curl -f http://localhost:8080/api/v1/auth/me
   ```

3. **Monitor deployment** (manual):
   - Keep Error Monitoring dashboard open during deployment
   - Watch error rate for 10 minutes post-deploy
   - Be ready to rollback quickly if spike detected

### Test Coverage

1. **Integration tests** for critical paths:
   - Test full user journeys (login → browse → chat)
   - Test with real database (Testcontainers)
   - Test external API integrations (with mocks/stubs)

2. **Error scenario tests** (negative testing):
   - Test invalid input handling (validation edge cases)
   - Test timeout scenarios (slow external API)
   - Test dependency failure handling (database down)

3. **Migration tests** (for database changes):
   - Test migration on copy of production database
   - Verify schema matches expectations after migration
   - Test rollback migration works correctly

### Monitoring

1. **Adjust alert threshold** (if too sensitive):
   ```yaml
   # In infra/prometheus/alerts/api-performance.yml
   expr: meepleai:api:error_spike_ratio > 5  # Changed from > 3
   # Or adjust averaging window from 1h to 2h
   ```

2. **Add deployment annotations** to Grafana:
   - Show deployment time on dashboard as vertical line
   - Easier to correlate spikes with deployments visually
   - Include commit SHA and deployer in annotation

3. **Improve correlation** (alert enrichment):
   - Include deployment SHA in alert payload
   - Link to commit diff in alert notification
   - Link to relevant dashboard panel in alert

## Escalation

### When to Escalate

Escalate if:
- ✅ Cannot rollback (database migration prevents rollback)
- ✅ Spike affects critical business endpoint (payments, auth)
- ✅ External dependency issue (out of our control, provider must fix)
- ✅ Spike persists despite rollback (underlying systemic issue)
- ✅ Cannot identify trigger after 10 minutes (need senior help)

### Escalation Process

1. **Post in #incidents** with context:
   ```
   🆘 Need help with ErrorSpike alert
   - Alert details: error rate 6.2x baseline
   - What I've tried: rolled back deployment, still spiking
   - What I need: help identifying root cause, no obvious trigger
   ```

2. **@ mention on-call engineer** (if not already involved)

3. **If critical**: Call team lead directly (phone, not Slack)

## Post-Incident

### Required

1. **Document incident** (GitHub issue):
   - Create issue with label `incident`
   - What caused spike (deployment bug, config change, external API)
   - How it was fixed (rollback, hotfix, config revert)
   - User impact (duration, affected users, error types)

2. **Improve tests**:
   - Add test for failure scenario (edge case that caused spike)
   - Ensure similar bug caught by CI before merge

3. **Update monitoring**:
   - Add alert if gap found (e.g., external API health alert)
   - Tune alert if false positive (adjust threshold or window)

### Optional (if major incident)

1. **Post-mortem meeting**:
   - Blameless discussion (focus on systems, not people)
   - Identify systemic issues (process gaps, tool limitations)
   - Create action items (improvements, preventive measures)

2. **Runbook update**:
   - Add new scenario if not covered (e.g., new external API failure mode)
   - Improve investigation steps (add new query, clarify steps)
   - Update common causes (add new root cause discovered)

## Testing This Runbook

**Simulate error spike**:
```bash
# Baseline: assume normal error rate ~0.1/sec
# Trigger spike: 10 errors/sec for 2 minutes (100x baseline > 3x threshold)

for i in {1..120}; do
  curl -X POST http://localhost:8080/api/v1/test-error &
  sleep 1
done

# Wait 2 minutes for ErrorSpike alert to fire
# Follow runbook investigation steps
```

**Expected behavior**:
- ErrorSpike alert fires in Alertmanager after 2 minutes
- Dashboard shows spike ratio > 3 in Error Spike Ratio panel
- Error Trend panel shows visual spike
- Alert auto-resolves 2 minutes after errors stop

**Cleanup**:
- Errors stop automatically after loop completes
- Alert auto-resolves when error rate returns to < 3x baseline
- No manual cleanup needed

## Related Runbooks

- [High Error Rate](./high-error-rate.md): For absolute high error rate (> 1/sec sustained)
- [Dependency Down](./dependency-down.md): For PostgreSQL, Redis, Qdrant outages
- [RAG Errors](./rag-errors.md): For RAG-specific error patterns

## Related Dashboards

- [Error Monitoring](http://localhost:3001/d/meepleai-error-monitoring): Primary dashboard, Error Spike Ratio panel
- [API Performance](http://localhost:3001/d/api-performance): For deployment correlation and performance metrics
- [Infrastructure](http://localhost:3001/d/infrastructure): For resource utilization during spike

## Changelog

- **2025-12-08**: Rewritten for uniform template compliance (Issue #706)
- **2025-10-16**: Initial version