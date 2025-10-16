# Runbook: Error Spike

**Alert**: `ErrorSpike`
**Severity**: CRITICAL
**Threshold**: Current error rate > 3x historical 1-hour average
**Expected Response Time**: Immediate (< 5 minutes)

## Symptoms

- Alert: "ErrorSpike" firing in Alertmanager
- Sudden increase in error rate (3x+ normal)
- May occur even if absolute error rate is low (e.g., 0.3/sec → 0.9/sec)
- Often indicates recent change or external event

## Difference from HighErrorRate

| Metric | HighErrorRate | ErrorSpike |
|--------|---------------|------------|
| **Threshold** | Absolute (> 1 error/sec) | Relative (3x baseline) |
| **Trigger** | Sustained high errors | Sudden increase |
| **Cause** | Major outage | Recent change/deployment |
| **Baseline** | Fixed threshold | Dynamic (1-hour average) |

**Example**:
- Normal: 0.1 errors/sec
- Spike to: 0.4 errors/sec
- **ErrorSpike fires** (4x > 3x threshold)
- **HighErrorRate does NOT fire** (0.4 < 1 threshold)

## Impact

- **Early Warning**: Catches issues before they become major outages
- **Deployment Validation**: Quickly identifies problematic releases
- **User Experience**: May indicate degraded service quality
- **Data Integrity**: New bugs may corrupt data

## Investigation Steps

### 1. Confirm Spike (30 seconds)

**Dashboard**:
```
http://localhost:3001/d/meepleai-error-monitoring
```

**Error Trend panel**:
- ✅ Visual spike visible in chart?
- ✅ When did spike start? (timestamp)
- ✅ Is spike ongoing or resolved?

**Prometheus**:
```
http://localhost:9090/graph
Query: meepleai:api:error_spike_ratio
```

Result > 3 confirms spike (e.g., 4.2 means 4.2x baseline).

### 2. Identify Trigger Event (1 minute)

**Check timeline of events**:

**Deployment**:
```bash
# Recent commits (last 2 hours)
git log --oneline --since="2 hours ago"

# Check deployment time
# Compare with spike start time from dashboard
```

**Configuration change**:
```bash
# Check git history for config files
git log --oneline --since="2 hours ago" -- \
  infra/env/*.env.dev \
  appsettings.json \
  docker-compose.yml
```

**Infrastructure change**:
```bash
# Check Docker events
docker events --since 2h --until now

# Check container restarts
docker compose ps
# Look for "Up X minutes" (recent restarts)
```

**External event**:
- Marketing campaign launched? (traffic spike)
- External API changed? (integration broken)
- DDoS attack? (malicious traffic)

### 3. Identify Affected Area (1 minute)

**Which endpoints**:
```
Dashboard → Top 10 Endpoints by Error Rate
```

**Which error types**:
```
Dashboard → Error Distribution by Status Code
Dashboard → Exception Type Distribution
```

**Which users**:
```
Seq: @Level = 'Error' and @Timestamp > DateTimeOffset.Now.AddMinutes(-10)
Group by: UserId
```

### 4. Compare Before/After (2 minutes)

**Error rate before spike**:
```promql
# Prometheus - 2 hours ago
meepleai:api:error_rate:5m offset 2h
```

**Error rate during spike**:
```promql
# Prometheus - now
meepleai:api:error_rate:5m
```

**What changed**:
- New exception types?
- Different endpoints affected?
- Different error categories (validation, system, dependency)?

### 5. Analyze Recent Code Changes (2 minutes)

**If deployment triggered spike**:

```bash
# Get diff of recent deployment
git diff HEAD~1 HEAD

# Focus on:
# - New endpoints
# - Modified business logic
# - Database queries
# - External API calls
# - Configuration changes
```

**Quick code review**:
- Any new exceptions thrown?
- Any new validation logic?
- Any changes to error handling?
- Any database schema changes?

### 6. Check Logs for New Errors (2 minutes)

**Seq - New exception types**:
```
@Level = 'Error'
and @Timestamp > DateTimeOffset.Now.AddMinutes(-10)
```

**Group by Exception type**:
```
Exception is not null
Group by: Substring(Exception, 0, 100)
```

**Look for patterns**:
- Same error on multiple endpoints?
- Same error for specific user?
- Same error at specific time (hourly cron job?)?

## Common Root Causes & Fixes

### Cause 1: New Deployment Introduced Bug

**Symptoms**:
- Spike starts 0-5 minutes after deployment
- New exception type in logs
- Specific endpoint has 100% error rate

**Investigation**:
```bash
# Find commit that caused spike
git log --oneline --since="30 minutes ago"

# Review changes
git show <commit-sha>
```

**Fix**:
```bash
# Option A: Quick rollback
git revert <commit-sha>
git push
# Wait for CI/CD redeploy (usually 5-10 min)

# Option B: Hot fix (if trivial bug)
# 1. Fix bug locally
# 2. git commit -m "hotfix: fix <issue>"
# 3. git push
# 4. Wait for CI/CD redeploy

# Option C: Rollback Docker image
# If you have versioned images:
docker compose down
docker compose pull meepleai/api:previous-version
docker compose up -d
```

**Verification**:
- Error rate drops to baseline within 1-2 minutes
- Affected endpoint returns 200 OK
- No new errors in Seq

**Resolution time**: 5-15 minutes

### Cause 2: Database Migration Issue

**Symptoms**:
- Spike after deployment with DB migration
- Error: "Invalid column" / "Table not found"
- All database operations failing

**Investigation**:
```bash
# Check migration status
cd apps/api/src/Api
dotnet ef migrations list
# Look for (Pending) migrations
```

**Fix**:
```bash
# Option A: Apply missing migration
dotnet ef database update

# Option B: Rollback migration
dotnet ef database update <PreviousMigrationName>

# Restart API
docker compose restart api
```

**Verification**:
```bash
# Check database schema
docker compose exec postgres psql -U meeple -d meepleai -c "\dt"

# Verify table exists
docker compose exec postgres psql -U meeple -d meepleai -c "\d <table_name>"
```

**Resolution time**: 5-10 minutes

### Cause 3: Configuration Change

**Symptoms**:
- Spike after config file change
- Error: "Connection string invalid" / "Missing configuration"
- Specific feature failing (e.g., all Redis operations)

**Investigation**:
```bash
# Check recent config changes
git log --oneline --since="1 hour ago" -- \
  infra/env/*.env.dev \
  appsettings.json

# Show diff
git diff HEAD~1 HEAD -- infra/env/api.env.dev
```

**Fix**:
```bash
# Option A: Revert config change
git checkout HEAD~1 -- infra/env/api.env.dev
git commit -m "revert: rollback config change"
git push

# Option B: Fix config manually
vi infra/env/api.env.dev
# Fix the incorrect value
docker compose restart api

# Option C: Use environment variable override
docker compose up -d \
  -e REDIS_URL=redis:6379 \
  api
```

**Verification**:
- Check API logs for successful connection: `docker compose logs api`
- Test affected feature manually

**Resolution time**: 2-5 minutes

### Cause 4: External Dependency Changed

**Symptoms**:
- Spike without internal changes
- Error: "HTTP 400 Bad Request" / "Unexpected response"
- All calls to external API failing

**Investigation**:
```bash
# Check external API health
curl -i https://api.external-service.com/health

# Check API version/changes
# Review external service's changelog or status page
```

**Fix**:
```bash
# Option A: Update integration code
# 1. Review external API docs for changes
# 2. Update code to match new API contract
# 3. Deploy fix

# Option B: Temporary fallback
# 1. Disable feature using external API
# 2. Return cached/default values
# 3. Fix integration later

# Option C: Contact external provider
# If their API changed unexpectedly, report bug
```

**Resolution time**: 15 minutes to several hours (depends on external provider)

### Cause 5: Traffic Spike (Legitimate or Attack)

**Symptoms**:
- Spike correlates with request rate spike
- No code/config changes
- Timeout errors / resource exhaustion

**Investigation**:
```promql
# Prometheus - Request rate
rate(http_server_request_duration_count[5m])

# Compare to baseline
rate(http_server_request_duration_count[5m] offset 1h)
```

**Fix**:
```bash
# Option A: Scale horizontally (if k8s/swarm)
kubectl scale deployment meepleai-api --replicas=3

# Option B: Enable rate limiting
# Ensure rate limiting is configured in appsettings.json

# Option C: Temporary: Block suspicious IPs
# Add to nginx/firewall if DDoS attack
```

**Resolution time**: 10-30 minutes

### Cause 6: Scheduled Job / Cron

**Symptoms**:
- Spike occurs at regular intervals (e.g., every hour)
- Specific endpoint called from background job
- Error in background task logs

**Investigation**:
```bash
# Check cron jobs / scheduled tasks
# Review code for:
# - IHostedService implementations
# - Hangfire/Quartz jobs
# - n8n workflows

# Check n8n workflows
curl http://localhost:5678/workflows
```

**Fix**:
```bash
# Option A: Disable problematic job
# Update configuration or code to disable job
# Restart API

# Option B: Fix job logic
# Review and fix the failing scheduled task
# Test manually before re-enabling

# Option C: Adjust job timing
# Change cron schedule to off-peak hours
# Reduce frequency if too aggressive
```

**Resolution time**: 5-30 minutes

## Mitigation Steps

### Immediate (< 2 minutes)

1. **Identify trigger** (deployment, config, external):
   ```bash
   git log --oneline --since="30 minutes ago"
   ```

2. **Silence alert** (if actively working):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=ErrorSpike, duration=30m
   Comment: "Investigating deployment rollback"
   ```

3. **Notify team**:
   ```
   #incidents: "Error spike detected after v1.2.0 deployment, rolling back"
   ```

### Short-term (< 10 minutes)

1. **Rollback** (if deployment caused spike):
   ```bash
   git revert <commit-sha>
   git push
   ```

2. **Or fix** (if trivial bug):
   ```bash
   # Make fix
   git commit -m "hotfix: ..."
   git push
   ```

3. **Verify fix**:
   - Error rate returns to baseline
   - No new errors in Seq
   - Affected endpoints respond correctly

### Medium-term (< 1 hour)

1. **Monitor for recurrence**:
   - Watch dashboard for 15-30 minutes
   - Ensure spike doesn't recur

2. **Root cause analysis**:
   - Why did deployment cause errors?
   - Why didn't tests catch this?
   - How to prevent in future?

3. **Create follow-up tasks**:
   - GitHub issue for proper fix (if rolled back)
   - Test coverage improvements
   - Alert tuning (if false positive)

## Prevention

### Before Deployment

1. **Canary deployment** (if possible):
   - Deploy to 10% of traffic first
   - Monitor error rate for 10 minutes
   - If stable, deploy to 100%

2. **Smoke tests** after deployment:
   ```bash
   # Test critical endpoints
   curl -f http://localhost:8080/api/v1/games
   curl -f http://localhost:8080/api/v1/health
   ```

3. **Monitor deployment**:
   - Keep dashboard open during deployment
   - Watch error rate for 10 minutes post-deploy
   - Be ready to rollback quickly

### Test Coverage

1. **Integration tests** for critical paths:
   - Test endpoint with real database
   - Test external API integrations (with mocks)

2. **Error scenario tests**:
   - Test invalid input handling
   - Test timeout scenarios
   - Test dependency failure handling

3. **Migration tests**:
   - Test migration on copy of production DB
   - Verify schema matches expectations
   - Test rollback migration

### Monitoring

1. **Adjust alert threshold** (if too sensitive):
   ```yaml
   # In prometheus-rules.yml
   expr: meepleai:api:error_spike_ratio > 5  # Changed from > 3
   ```

2. **Add deployment annotations** to Grafana:
   - Show deployment time on dashboard
   - Easier to correlate spikes with deployments

3. **Improve correlation**:
   - Include deployment SHA in alert payload
   - Link to commit diff in notification

## Escalation

### When to Escalate

Escalate if:
- ✅ Cannot rollback (database migration prevents it)
- ✅ Spike affects critical business endpoint
- ✅ External dependency issue (out of our control)
- ✅ Spike persists despite rollback

### Escalation Process

1. Post in #incidents with:
   - Alert details
   - What you've tried
   - What you need help with

2. @ mention on-call engineer if not already involved

3. If critical: Call team lead directly

## Post-Incident

### Required

1. **Document incident** (GitHub issue):
   - What caused spike
   - How it was fixed
   - Impact (users affected, duration)

2. **Improve tests**:
   - Add test for failure scenario
   - Ensure similar bug caught by CI

3. **Update monitoring**:
   - Add alert if gap found
   - Tune alert if false positive

### Optional (if major incident)

1. **Post-mortem meeting**:
   - Blameless discussion
   - Identify systemic issues
   - Create action items

2. **Runbook update**:
   - Add new scenario if not covered
   - Improve investigation steps

## Testing This Runbook

**Simulate error spike**:
```bash
# Normal error rate: ~0/sec
# Trigger 10 errors/sec for 2 minutes

for i in {1..120}; do
  curl -X POST http://localhost:8080/api/v1/test-error
  sleep 1
done
```

**Expected**:
- ErrorSpike alert fires after 2 minutes
- Dashboard shows spike in Error Rate panel
- Alert auto-resolves after errors stop

## Changelog

- **2025-10-16**: Initial version
