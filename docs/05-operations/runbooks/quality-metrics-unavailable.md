# Runbook: Quality Metrics Unavailable

**Alert**: `QualityMetricsUnavailable`
**Severity**: CRITICAL
**Threshold**: No quality metrics recorded for 15 minutes
**Expected Response Time**: Immediate (< 15 minutes)

## Symptoms

**Observable indicators when this alert fires:**
- Alert: "QualityMetricsUnavailable" firing in Alertmanager
- Quality metrics dashboard shows no data or stale data
- No quality scores being recorded for AI responses
- Either no AI requests processed OR quality scoring service failing

## Impact

**Effect on system and users:**
- **User Experience**: Users may receive responses (if AI working) but quality not tracked
- **Data Integrity**: No quality validation, potentially poor responses going undetected
- **Business Impact**: Quality blind spot - cannot detect or alert on poor AI performance
- **System Health**: Quality scoring pipeline broken, monitoring gap, no quality assurance

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/quality-metrics
```

**Verification checklist**:
- ✅ Are quality metrics actually missing? (no data points in last 15 min)
- ✅ Is AI functionality working? (are chat requests being processed)
- ✅ When was last quality metric recorded? (check timestamp)
- ✅ Is this complete failure or partial? (all dimensions missing or just some)

**Prometheus Query**:
```promql
# Check if quality metrics exist
meepleai_quality_score{dimension="overall_confidence"}

# Check if AI requests happening
rate(http_server_request_duration_count{http_route="/api/v1/chat"}[5m])
```

**Interpretation**:
- **No quality metrics + no chat requests** → No AI activity (normal if no users)
- **No quality metrics + chat requests happening** → Quality scoring broken (CRITICAL)

**If false alarm**:
- Silence alert for 30 minutes
- Document if no AI requests expected (off-peak hours, maintenance)
- Create issue to add "AI requests" condition to alert

### 2. Determine Failure Mode (1 minute)

**Questions to answer**:
1. **Are AI requests being processed?** (check request rate)
2. **Is quality scoring running?** (check logs for quality score entries)
3. **Are metrics being exported?** (check /metrics endpoint)
4. **Is Prometheus scraping?** (check Prometheus targets)

**Check AI request rate**:
```promql
# Chat endpoint request rate
rate(http_server_request_duration_count{http_route="/api/v1/chat"}[5m])
```

**Check quality scoring logs**:
```bash
# HyperDX: Search for quality scoring events
http://localhost:8180
Filter: quality_score AND @timestamp:[now-30m TO now]

# Should see entries like:
# {
#   "quality_score": {
#     "overall_confidence": 0.75,
#     "rag_confidence": 0.80,
#     ...
#   }
# }
```

### 3. Check Quality Scoring Service (2 minutes)

**Verify quality scoring code**:
```bash
# Check if quality scoring is enabled
cat apps/api/src/Api/appsettings.json | grep -A 5 "QualityScoring"

# Should show:
# "QualityScoring": {
#   "Enabled": true,
#   ...
# }
```

**Check quality scoring logs**:
```bash
# API logs for quality scoring errors
docker compose logs api | grep -i "quality\|scoring"

# Look for:
# - "Quality scoring failed" (exceptions)
# - "Quality score calculated" (successful)
# - Exception stack traces related to quality
```

**HyperDX - Quality scoring errors**:
```
http://localhost:8180
Filter: quality AND error AND @timestamp:[now-30m TO now]
```

### 4. Check Metrics Export (1 minute)

**Test /metrics endpoint**:
```bash
# Check if metrics endpoint responding
curl http://localhost:8080/metrics | grep "meepleai_quality"

# Should see quality metrics like:
# meepleai_quality_score{dimension="overall_confidence"} 0.75
# meepleai_quality_score_count{dimension="overall_confidence"} 42
```

**If no quality metrics in output**:
- Quality scoring not running or not exporting
- Check quality scoring service code
- Check metrics configuration

### 5. Check Prometheus Scraping (1 minute)

**Verify Prometheus scraping API**:
```bash
# Check Prometheus targets
curl http://localhost:9090/targets | grep meepleai-api

# Should show:
# - State: UP
# - Last scrape: < 30 seconds ago
# - Scrape duration: < 1 second
```

**Prometheus UI**:
```
http://localhost:9090/targets
Find: meepleai-api target
Check: State should be "UP"
```

**If target down**:
- Prometheus cannot reach API /metrics endpoint
- Check network connectivity
- Check API health

### 6. Check Recent Changes (1 minute)

**Recent deployment?**
```bash
# Check recent commits affecting quality scoring
git log --oneline --since="4 hours ago" -- \
  "**/KnowledgeBase/**" \
  "**/Quality/**"

# Check if quality scoring code was modified
git diff HEAD~1 HEAD -- "**/QualityScoring/**"
```

**Configuration changes?**:
```bash
# Check quality config changes
git log --oneline --since="4 hours ago" -- \
  apps/api/src/Api/appsettings.json

# Check if QualityScoring section modified
git diff HEAD~1 HEAD -- apps/api/src/Api/appsettings.json | grep -A 10 "Quality"
```

### 7. Test Quality Scoring Manually (2 minutes)

**Trigger chat request**:
```bash
# Send test question
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"How do I win in Catan?","gameId":1}' \
  | jq '.'

# Check response for quality scores
# Should include: confidence, quality_tier, etc.
```

**Check if quality was scored**:
```bash
# Immediately after request, check metrics
curl http://localhost:8080/metrics | grep "meepleai_quality_score_count"

# Count should increment by 1
```

**Check logs for quality calculation**:
```bash
# API logs after test request
docker compose logs api --tail 50 | grep -i "quality"

# Should see:
# "Quality score calculated: 0.75" (or similar)
```

## Common Root Causes & Fixes

### Cause 1: Quality Scoring Disabled

**Symptoms**:
- No quality metrics at all (complete absence)
- Chat requests working fine (responses generated)
- No quality-related logs (no errors, no success messages)
- Configuration shows quality scoring disabled

**Investigation**:
```bash
# Check if quality scoring enabled
cat apps/api/src/Api/appsettings.json | grep -A 5 "QualityScoring"

# Should show:
# "Enabled": true
```

**Fix**:
```bash
# Option A: Enable quality scoring in config
# Edit apps/api/src/Api/appsettings.json:
# "QualityScoring": {
#   "Enabled": true,
#   "MinOverallConfidence": 0.70
# }
docker compose restart api

# Option B: Check if disabled via feature flag
# Query SystemConfiguration database
docker compose exec postgres psql -U meeple -d meepleai -c "
SELECT * FROM system_configurations
WHERE key = 'Features.QualityScoring.Enabled';"
# If false, enable via admin UI or database

# Option C: Verify feature flag not overriding config
# Check runtime configuration
curl http://localhost:8080/api/v1/admin/configuration | grep -i quality
```

**Verification**:
```bash
# Quality scoring logs appear
docker compose logs api --tail 50 | grep -i "quality"

# Quality metrics exported
curl http://localhost:8080/metrics | grep "meepleai_quality"

# New chat requests produce quality scores
# Send test request, check response for quality fields
```

**Resolution time**: 2-5 minutes

### Cause 2: Quality Scoring Crashing

**Symptoms**:
- Quality metrics stopped abruptly (were working, now missing)
- Logs show exceptions in quality scoring code
- Chat requests still work (responses generated) but no quality calculated
- Error: "Quality scoring failed" or similar exceptions

**Investigation**:
```bash
# Check for quality scoring exceptions
docker compose logs api | grep -i "quality.*exception\|quality.*error"

# HyperDX: Quality scoring errors
http://localhost:8180
Filter: quality AND exception AND @timestamp:[now-30m TO now]

# Look for stack traces pointing to quality scoring code
```

**Fix**:
```bash
# Option A: Restart API (if temporary exception)
docker compose restart api
# Wait for API to start
docker compose ps api

# Option B: Rollback deployment (if recent change broke it)
git log --oneline --since="4 hours ago" -- "**/Quality/**"
git revert <commit-sha>
git push origin main

# Option C: Disable quality scoring temporarily (emergency)
# Edit appsettings.json:
# "QualityScoring": {
#   "Enabled": false
# }
docker compose restart api
# This allows AI to work while quality issue is fixed

# Option D: Fix bug in quality scoring code
# Identify exception root cause from logs
# Fix bug, test locally, deploy
```

**Verification**:
```bash
# No quality scoring exceptions
docker compose logs api --tail 100 | grep -i "quality.*error"

# Quality metrics appear
curl http://localhost:8080/metrics | grep "meepleai_quality_score"

# Test chat request produces quality score
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"Test","gameId":1}' | jq '.confidence'
```

**Resolution time**: 5-30 minutes

### Cause 3: Prometheus Not Scraping

**Symptoms**:
- Quality scoring working (logs show scores calculated)
- /metrics endpoint has quality metrics
- But Prometheus not recording them (queries return no data)
- Prometheus target shows DOWN or scrape errors

**Investigation**:
```bash
# Check Prometheus targets
curl http://localhost:9090/targets | jq '.activeTargets[] | select(.scrapeUrl | contains("meepleai-api"))'

# Check scrape status
# http://localhost:9090/targets
# Look for meepleai-api target state

# Check Prometheus logs
docker compose logs prometheus --tail 100
```

**Fix**:
```bash
# Option A: Restart Prometheus
docker compose restart prometheus
# Wait 30 seconds for Prometheus to start scraping

# Option B: Fix Prometheus config (if scrape config broken)
cat infra/prometheus/prometheus.yml
# Verify meepleai-api job configured correctly

# Option C: Fix network connectivity (Prometheus can't reach API)
docker compose exec prometheus wget -O- http://api:8080/metrics
# If fails, check Docker network

# Option D: Restart both API and Prometheus
docker compose restart api prometheus
```

**Verification**:
```bash
# Prometheus target shows UP
curl http://localhost:9090/targets | jq '.activeTargets[] | select(.scrapeUrl | contains("meepleai-api")) | .health'
# Should return: "up"

# Quality metrics in Prometheus
curl http://localhost:9090/api/v1/query?query=meepleai_quality_score
# Should return data points

# Alert auto-resolves
curl http://localhost:9093 | jq '.data.alerts[] | select(.labels.alertname=="QualityMetricsUnavailable")'
# Should return empty (alert resolved)
```

**Resolution time**: 2-10 minutes

### Cause 4: No AI Requests (False Positive)

**Symptoms**:
- No quality metrics because no AI requests
- No chat requests in last 15 minutes (legitimate silence)
- System healthy, just no user activity
- Typically occurs: off-peak hours, weekends, maintenance windows

**Investigation**:
```promql
# Check chat request rate
rate(http_server_request_duration_count{http_route="/api/v1/chat"}[15m])
# If 0, no AI requests happening
```

**Fix**:
```bash
# Option A: Adjust alert to require AI requests
# Edit infra/prometheus/alerts/quality-metrics.yml:
# - alert: QualityMetricsUnavailable
#   expr: |
#     (
#       absent(meepleai_quality_score{dimension="overall_confidence"})
#       or
#       rate(meepleai_quality_score_count{dimension="overall_confidence"}[5m]) == 0
#     )
#     and
#     rate(http_server_request_duration_count{http_route="/api/v1/chat"}[5m]) > 0
#   # Only alert if chat requests happening but no quality metrics

# Option B: Silence alert during expected quiet periods
# Create recurring silence for off-peak hours
# Or: Adjust alert for duration (15m → 30m)

# Option C: Accept false positives (no action)
# If rare, manually resolve alert
# Document in incident notes
```

**Verification**:
```bash
# Alert stops firing when no requests (after config change)
# Or: Alert fires only when requests happening but no metrics

# Test by sending chat request
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"Test","gameId":1}'

# Quality metrics should appear
curl http://localhost:8080/metrics | grep "meepleai_quality_score"
```

**Resolution time**: 5-15 minutes (config change)

## Mitigation Steps

### Immediate (< 2 minutes)

1. **Check if AI requests happening**:
   ```promql
   rate(http_server_request_duration_count{http_route="/api/v1/chat"}[15m])
   ```

2. **Silence alert** (if false positive or actively working):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=QualityMetricsUnavailable, duration=30m
   Comment: "No AI requests happening - off-peak hours"
   # Or: "Quality scoring service restarted - monitoring"
   ```

3. **Notify team** (if CRITICAL - quality scoring broken):
   ```
   #incidents: "🚨 Quality metrics unavailable - quality scoring service down - restarting API"
   ```

### Short-term (< 15 minutes)

1. **Identify failure mode** (use investigation steps 1-7 above):
   - No AI requests (false positive)
   - Quality scoring disabled (config issue)
   - Quality scoring crashing (code bug)
   - Prometheus not scraping (monitoring issue)

2. **Apply fix** (based on failure mode):
   - Restart API (if crashing)
   - Enable quality scoring (if disabled)
   - Fix Prometheus scraping (if scrape failure)
   - Adjust alert (if false positive)

3. **Verify fix**:
   - Quality metrics appear in /metrics endpoint
   - Prometheus recording quality metrics (query returns data)
   - Quality dashboard shows recent data

4. **Update incident channel**:
   - Post resolution: "✅ Quality metrics restored - scoring service working"
   - Or post status: "ℹ️ No AI requests happening - normal off-peak silence"

### Medium-term (< 1 hour)

1. **Monitor quality metrics**:
   - Watch Quality Metrics dashboard for 30 minutes
   - Ensure metrics continue being recorded
   - Test multiple AI requests (verify consistency)

2. **Root cause analysis**:
   - Why did metrics stop? (crash, config, scrape failure)
   - Is fix permanent? (code fix vs restart)
   - How to detect earlier? (better monitoring, alerts)

3. **Create follow-up tasks**:
   - GitHub issue for quality scoring robustness (if crashed)
   - Update alert to reduce false positives
   - Add quality scoring health check

## Escalation

### When to Escalate

Escalate if:
- ✅ Quality scoring service keeps crashing (>3 restarts)
- ✅ Cannot identify why quality scoring failing (need code review)
- ✅ Prometheus scraping broken (infrastructure issue)
- ✅ Requires AI/ML team expertise (quality calculation logic)

### Escalation Contacts

**AI/ML team**:
- #ai-engineering Slack channel
- ML engineer (for quality scoring logic)

**DevOps/Infrastructure**:
- #ops Slack channel
- On-call DevOps (for Prometheus, monitoring issues)

**Emergency contacts**:
- Team Lead: [name] - [phone]

## Prevention

### Monitoring

1. **Quality scoring health**:
   ```promql
   # Quality metrics being recorded
   rate(meepleai_quality_score_count[5m]) > 0

   # Quality scoring errors
   rate(meepleai_quality_scoring_errors_total[5m]) == 0
   ```

2. **Alert improvements**:
   - Add condition: Only alert if chat requests happening
   - Reduce false positives from off-peak hours
   - Current alert (15min) is appropriate

3. **Prometheus health**:
   - Monitor Prometheus scrape failures
   - Alert on Prometheus target down
   - Monitor Prometheus memory/disk usage

### Configuration

1. **Robust quality scoring** (appsettings.json):
   ```json
   "QualityScoring": {
     "Enabled": true,
     "FallbackOnError": true,
     "RetryPolicy": {
       "MaxRetries": 3,
       "BackoffSeconds": 2
     },
     "Timeouts": {
       "CalculationTimeoutMs": 5000
     }
   }
   ```

2. **Prometheus scrape config** (prometheus.yml):
   ```yaml
   scrape_configs:
     - job_name: 'meepleai-api'
       scrape_interval: 15s
       scrape_timeout: 10s
       metrics_path: '/metrics'
       static_configs:
         - targets: ['api:8080']
   ```

3. **Error handling**:
   - Quality scoring failures should not block chat responses
   - Log errors but allow request to complete
   - Retry quality calculation on next request

### Code Quality

1. **Quality scoring robustness**:
   - Wrap quality calculation in try-catch
   - Log exceptions but don't throw
   - Implement circuit breaker (disable after N failures)

2. **Metrics export**:
   - Ensure all quality dimensions exported
   - Add counter for quality scoring errors
   - Add histogram for calculation time

3. **Testing**:
   - Add integration tests for quality scoring
   - Test quality calculation with various inputs
   - Test metrics export in CI/CD

## Testing This Runbook

**Simulate metrics unavailable**:
```bash
# Option A: Disable quality scoring
# Edit appsettings.json:
# "QualityScoring": { "Enabled": false }
docker compose restart api

# Send chat requests (will have no quality metrics)
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{"question":"Test","gameId":1}'

# Wait 15 minutes for alert to fire

# Option B: Stop Prometheus (metrics not scraped)
docker compose stop prometheus
# Wait 15 minutes for alert

# Follow runbook investigation steps
```

**Expected behavior**:
- QualityMetricsUnavailable alert fires after 15 minutes
- Dashboard shows no quality data
- Alert resolves when quality scoring restored

## Related Runbooks

- [AI Quality Low](./ai-quality-low.md): For actual quality degradation (not missing metrics)
- [RAG Errors](./rag-errors.md): RAG failures may prevent quality calculation
- [High Error Rate](./high-error-rate.md): Quality scoring crashes may cause errors

## Related Dashboards

- [Quality Metrics](http://localhost:3001/d/quality-metrics): Primary dashboard (will be empty if metrics unavailable)
- [AI/RAG Operations](http://localhost:3001/d/ai-rag-operations): AI pipeline health
- [Error Monitoring](http://localhost:3001/d/meepleai-error-monitoring): Quality scoring errors

## Changelog

- **2025-12-08**: Initial version (Issue #706)
