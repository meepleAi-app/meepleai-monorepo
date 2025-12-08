# Runbook: [ALERT_NAME]

**Alert**: `[AlertName]`
**Severity**: [CRITICAL|WARNING|INFO]
**Threshold**: [Threshold description]
**Expected Response Time**: [< X minutes]

## Symptoms

**Observable indicators when this alert fires:**
- Alert: "[AlertName]" firing in Alertmanager
- Dashboard shows [specific metric/panel] anomaly
- Users experiencing [specific symptoms]
- [Additional observable symptoms]

## Impact

**Effect on system and users:**
- **User Experience**: [How users are affected]
- **Data Integrity**: [Potential data issues]
- **Business Impact**: [Revenue/reputation effects]
- **System Health**: [Infrastructure/service impact]

## Investigation Steps

### 1. Verify Alert (30 seconds)

**Dashboard Check**:
```
http://localhost:3001/d/[dashboard-id]
```

**Verification checklist**:
- ✅ Is the metric actually above threshold?
- ✅ Which components are affected?
- ✅ What is the current value vs threshold?
- ✅ Is this a false positive?

**Prometheus Query**:
```promql
[metric_name]
```

**If false alarm**:
- Silence alert for 30 minutes
- Document why it's a false positive
- Create issue to tune alert threshold

### 2. Identify Scope (1 minute)

**Questions to answer**:
1. **When did it start?** (check dashboard timeline)
2. **Which services/endpoints affected?** (check specific panels)
3. **All users or specific users?** (check HyperDX logs)
4. **Error types/patterns?** (check metrics breakdown)

**Prometheus queries**:
```promql
# [Query 1 description]
[promql_query_1]

# [Query 2 description]
[promql_query_2]
```

### 3. Check Recent Changes (1 minute)

**Recent deployment?**
```bash
# Check recent commits
git log --oneline -10

# Check deployed version
curl http://localhost:8080/health | jq '.version'
```

**Recent configuration changes?**
- [Config file 1]
- [Config file 2]
- Environment variables

**Recent infrastructure changes?**
- Docker container restarts
- Volume changes
- Network policy changes

### 4. Check Dependencies (2 minutes)

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
Filter: level:[error|warn] AND @timestamp:[now-10m TO now]
```

**Look for**:
- Common exception types
- Stack traces pointing to specific code
- Error messages indicating root cause
- Correlation patterns

**Get correlation IDs**:
```
RequestId: [example-format]
```

### 6. Check Traces (1 minute)

**HyperDX - Distributed tracing**:
```
http://localhost:8180
Service: meepleai-api
Operation: [affected_operation]
Tags: error=true
```

**Look for**:
- Slow operations (> threshold)
- Failed dependency calls
- Timeouts in specific operations
- Unusual latency patterns

### 7. Check System Resources (1 minute)

**Prometheus resource queries**:
```promql
# CPU usage (should be < 80%)
rate(process_cpu_seconds_total{job="meepleai-api"}[5m]) * 100

# Memory usage (should be < 80%)
process_working_set_bytes{job="meepleai-api"} / process_memory_limit_bytes * 100

# Active requests (check for spikes)
http_server_active_requests

# [Additional resource metrics]
```

**Docker stats**:
```bash
docker stats --no-stream api
docker stats --no-stream postgres redis qdrant
```

## Common Root Causes & Fixes

### Cause 1: [Most Common Cause Name]

**Symptoms**:
- [Symptom 1]
- [Symptom 2]
- [Symptom 3]

**Fix**:
```bash
# Option A: [Primary fix approach]
[commands]

# Option B: [Alternative fix approach]
[commands]

# Option C: [Emergency fix approach]
[commands]
```

**Verification**:
```bash
# Verify fix worked
[verification commands]
```

**Prevention**:
- [Prevention measure 1]
- [Prevention measure 2]
- [Prevention measure 3]

**Resolution time**: [X-Y minutes]

### Cause 2: [Second Most Common Cause]

**Symptoms**:
- [Symptom 1]
- [Symptom 2]

**Fix**:
```bash
# [Fix commands with explanation]
```

**Resolution time**: [X-Y minutes]

### Cause 3: [Third Most Common Cause]

**Symptoms**:
- [Symptom 1]
- [Symptom 2]

**Fix**:
```bash
# [Fix commands with explanation]
```

**Resolution time**: [X-Y minutes]

## Mitigation Steps

### Immediate (< 2 minutes)

1. **[Action 1]**:
   ```bash
   [commands]
   ```

2. **Silence alert** (if actively working on it):
   ```
   http://localhost:9093
   Silences → New Silence
   alertname=[AlertName], duration=30m
   Comment: "[Brief explanation of action being taken]"
   ```

3. **Notify team**:
   ```
   #incidents: "[Brief status update]"
   ```

### Short-term (< 10 minutes)

1. **Identify root cause** (use investigation steps above)

2. **Apply fix** (use appropriate fix from Common Root Causes section)

3. **Verify fix**:
   - Check metric returns to normal in dashboard
   - Test affected functionality manually
   - Check HyperDX logs for no new errors

4. **Update incident channel**:
   - Post resolution: "[Brief resolution summary]"
   - Post ETA if not yet resolved

### Medium-term (< 1 hour)

1. **Monitor for recurrence**:
   - Watch dashboard for 15-30 minutes
   - Ensure metric stays within acceptable range

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
- ✅ Cannot identify root cause after [X] minutes
- ✅ Fix attempts don't resolve issue
- ✅ Metric worsening despite mitigation
- ✅ Critical business functionality affected
- ✅ Data integrity concerns

### Escalation Contacts

**On-call rotation** (check Grafana OnCall):
```
http://localhost:8082
```

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

1. **Code improvements**:
   - Review code that caused issue
   - Add/improve error handling
   - Add/improve input validation

2. **Testing**:
   - Add test for failure scenario
   - Add integration test for affected component
   - Add load test if traffic spike caused issue

3. **Monitoring**:
   - Add custom metric if needed
   - Adjust alert threshold if false positive
   - Add new alert if gap discovered

## Testing This Runbook

**Simulate alert condition**:
```bash
# [Commands to trigger the alert for testing]
```

**Expected behavior**:
- Alert fires in Alertmanager
- Dashboard shows anomaly
- Logs show expected patterns
- Trace shows expected behavior

**Cleanup**:
- [Steps to return system to normal]
- Alert auto-resolves after [X] minutes
- [Any manual cleanup needed]

## Related Runbooks

- [Related Runbook 1](./related-runbook-1.md)
- [Related Runbook 2](./related-runbook-2.md)

## Related Dashboards

- [Dashboard 1](http://localhost:3001/d/dashboard-1): [Purpose]
- [Dashboard 2](http://localhost:3001/d/dashboard-2): [Purpose]

## Changelog

- **YYYY-MM-DD**: Initial version
- **YYYY-MM-DD**: [Update description]