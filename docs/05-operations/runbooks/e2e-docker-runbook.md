# E2E Docker Testing Runbook

**Issue**: #2009 - Phase 3: Production-Grade Infrastructure
**Owner**: QA Team
**Last Updated**: 2025-12-12

## Overview

This runbook provides troubleshooting procedures for the Docker-based E2E testing infrastructure with full observability (Prometheus + Grafana).

## Quick Reference

| Issue | Command | Expected Fix Time |
|-------|---------|-------------------|
| Tests failing | `./scripts/docker-e2e.sh logs e2e-shard-N` | 5-15 min |
| Container won't start | `./scripts/docker-e2e.sh clean && build && run` | 2-5 min |
| Metrics not appearing | Check Prometheus remote write URL | 1-2 min |
| Dashboard not loading | Restart Grafana container | 30 sec |
| Out of memory | Increase Docker memory limit | 5 min |

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                 Docker E2E Infrastructure                │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐  │
│  │ Shard 1  │ │ Shard 2  │ │ Shard 3  │ │ Shard 4  │  │
│  │ (4GB RAM)│ │ (4GB RAM)│ │ (4GB RAM)│ │ (4GB RAM)│  │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘  │
│       │            │            │            │         │
│       └────────────┴────────────┴────────────┘         │
│                         │                              │
│                    [Metrics Push]                      │
│                         │                              │
│                         ▼                              │
│                 ┌──────────────┐                       │
│                 │  Prometheus  │                       │
│                 │  (Port 9090) │                       │
│                 └───────┬──────┘                       │
│                         │                              │
│                    [Query Metrics]                     │
│                         │                              │
│                         ▼                              │
│                 ┌──────────────┐                       │
│                 │   Grafana    │                       │
│                 │  (Port 3001) │                       │
│                 └──────────────┘                       │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

## Common Issues

### 1. No Tests Running Alert

**Symptom**: `E2E_NoTestsRunning` alert triggered
**Cause**: Test containers failed to start or crashed

**Diagnosis**:
```bash
# Check container status
./scripts/docker-e2e.sh status

# View logs for all shards
./scripts/docker-e2e.sh logs
```

**Resolution**:
1. Check Docker daemon is running
2. Verify Docker has sufficient resources (16GB RAM minimum)
3. Restart infrastructure:
   ```bash
   ./scripts/docker-e2e.sh stop
   ./scripts/docker-e2e.sh clean
   ./scripts/docker-e2e.sh build
   ./scripts/docker-e2e.sh run
   ```

**Prevention**:
- Set Docker memory limit to at least 20GB
- Monitor Docker daemon health

---

### 2. High Failure Rate Alert

**Symptom**: `E2E_CriticalFailureRate` or `E2E_HighFailureRate` alert triggered
**Cause**: Multiple test failures across shards

**Diagnosis**:
```bash
# View failed test reports
ls -la apps/web/test-results/shard-*/

# Check Grafana dashboard for failure patterns
./scripts/docker-e2e.sh dashboard

# View logs for specific failing shard
./scripts/docker-e2e.sh logs e2e-shard-N
```

**Resolution**:
1. **Identify failure pattern**:
   - All shards failing → Infrastructure issue
   - Single shard failing → Test distribution or flaky test issue
   - Specific test files failing → Code regression

2. **For infrastructure issues**:
   ```bash
   # Check Docker resources
   docker stats

   # Restart with increased resources
   # Edit docker-compose.e2e.yml: mem_limit: 6g
   ./scripts/docker-e2e.sh stop
   ./scripts/docker-e2e.sh run
   ```

3. **For test issues**:
   - Review Playwright reports: `apps/web/playwright-report/shard-N/index.html`
   - Run failing shard locally for debugging:
     ```bash
     cd apps/web
     pnpm test:e2e:shardN
     ```

**Prevention**:
- Implement pre-deployment test validation
- Monitor test pass rate trends in Grafana
- Tag flaky tests and track via Grafana table

---

### 3. Slow Test Execution Alert

**Symptom**: `E2E_SlowTestExecution` alert triggered
**Cause**: P95 test duration > 30 seconds

**Diagnosis**:
```bash
# Query Prometheus for slow tests
curl "http://localhost:9090/api/v1/query?query=histogram_quantile(0.95,sum(rate(playwright_test_duration_bucket[5m]))by(le))"

# Check Grafana duration panel
./scripts/docker-e2e.sh dashboard
```

**Resolution**:
1. **Identify slow tests**: View Grafana duration panel, sort by P95
2. **Optimize tests**:
   - Review long-running tests for optimization opportunities
   - Add explicit waits instead of fixed delays
   - Reduce unnecessary navigation steps

3. **Increase parallelization**: If CPU is underutilized:
   ```yaml
   # In docker-compose.e2e.yml
   cpus: '4.0'  # Increase from 2.0
   ```

**Prevention**:
- Set test timeout thresholds
- Profile slow tests during development
- Monitor duration trends

---

### 4. Flaky Tests Detected Alert

**Symptom**: `E2E_FlakyTestsDetected` alert triggered
**Cause**: Tests requiring retries to pass

**Diagnosis**:
```bash
# Query Prometheus for flaky tests
curl "http://localhost:9090/api/v1/query?query=sum(playwright_test_retry_count)by(test_title)>0"

# View flaky tests table in Grafana
./scripts/docker-e2e.sh dashboard
```

**Resolution**:
1. **Identify flaky test**: Check Grafana "Flaky Tests" table
2. **Analyze failure patterns**:
   - Race conditions → Add explicit waits
   - Network timeouts → Increase timeout or mock API
   - Resource contention → Reduce parallel workers

3. **Fix or isolate**:
   ```typescript
   // Option 1: Fix race condition
   await page.waitForSelector('[data-testid="loaded"]', { state: 'visible' });

   // Option 2: Tag as known flaky (temporary)
   test.only('flaky test', async ({ page }) => {
     // Test code
   });
   ```

**Prevention**:
- Code review focus on test stability
- Mandatory local shard execution before PR
- Track flaky test count as quality metric

---

### 5. High Memory Usage Alert

**Symptom**: `E2E_HighMemoryUsage` alert triggered
**Cause**: Memory usage > 90% of container limit

**Diagnosis**:
```bash
# Check container memory usage
docker stats $(docker-compose -f apps/web/docker-compose.e2e.yml ps -q)

# Check Node.js heap usage (if accessible)
./scripts/docker-e2e.sh logs e2e-shard-N | grep "heap"
```

**Resolution**:
1. **Immediate**: Restart affected shard
   ```bash
   docker-compose -f apps/web/docker-compose.e2e.yml restart e2e-shard-N
   ```

2. **Short-term**: Increase memory limit
   ```yaml
   # In docker-compose.e2e.yml
   mem_limit: 6g  # Increase from 4g
   ```

3. **Long-term**: Investigate memory leaks
   - Review test cleanup (afterEach hooks)
   - Check for unclosed browser contexts
   - Profile memory usage:
     ```bash
     node --max-old-space-size=4096 --expose-gc ./node_modules/next/dist/bin/next dev
     ```

**Prevention**:
- Monitor heap usage trends in Grafana
- Enforce cleanup in test fixtures
- Set memory alerts at 80% threshold

---

### 6. Prometheus Not Receiving Metrics

**Symptom**: Grafana dashboard shows "No data"
**Cause**: Metrics not pushed from test containers

**Diagnosis**:
```bash
# Check Prometheus targets
curl http://localhost:9090/api/v1/targets | jq

# Verify remote write endpoint is accessible
docker-compose -f apps/web/docker-compose.e2e.yml exec e2e-shard-1 \
  curl -X POST http://prometheus:9090/api/v1/write

# Check container environment variables
docker-compose -f apps/web/docker-compose.e2e.yml exec e2e-shard-1 env | grep PROMETHEUS
```

**Resolution**:
1. **Verify environment variable**: `PROMETHEUS_REMOTE_WRITE_URL=http://prometheus:9090/api/v1/write`
2. **Check network connectivity**:
   ```bash
   docker network inspect meepleai-e2e_e2e-network
   ```
3. **Restart Prometheus**:
   ```bash
   docker-compose -f apps/web/docker-compose.e2e.yml restart prometheus
   ```

**Prevention**:
- Add startup health checks for Prometheus
- Validate environment variables in test setup

---

### 7. Grafana Dashboard Not Loading

**Symptom**: Grafana UI shows errors or blank dashboard
**Cause**: Datasource misconfiguration or provisioning failure

**Diagnosis**:
```bash
# Check Grafana logs
./scripts/docker-e2e.sh logs grafana

# Verify datasource configuration
curl -u admin:${GRAFANA_E2E_PASSWORD:-admin} http://localhost:3001/api/datasources | jq
```

**Resolution**:
1. **Restart Grafana**:
   ```bash
   docker-compose -f apps/web/docker-compose.e2e.yml restart grafana
   ```

2. **Re-provision datasources**:
   ```bash
   # Verify provisioning files exist
   ls -la infra/grafana/provisioning/datasources/

   # Recreate Grafana container
   docker-compose -f apps/web/docker-compose.e2e.yml up -d --force-recreate grafana
   ```

3. **Manual datasource setup** (if provisioning fails):
   - Navigate to http://localhost:3001
   - Login: admin / ${GRAFANA_E2E_PASSWORD:-admin} (default: admin, configure via .env.test)
   - Configuration → Data Sources → Add Prometheus
   - URL: http://prometheus:9090

**Prevention**:
- Version control provisioning files
- Test dashboard JSON syntax before deployment

---

## Monitoring Best Practices

### Daily Operations

```bash
# Morning: Check test health
./scripts/docker-e2e.sh dashboard

# After deployment: Run full suite
./scripts/docker-e2e.sh run

# Weekly: Review metrics trends
# - Pass rate should be ≥98%
# - P95 duration should be <25s
# - Flaky test count should be <3
```

### Metrics to Monitor

| Metric | Healthy Range | Warning Threshold | Critical Threshold |
|--------|---------------|-------------------|-------------------|
| Test Pass Rate | ≥98% | <95% | <90% |
| Test Duration (P95) | <20s | >25s | >30s |
| Flaky Test Count | 0-2 | 3-5 | >5 |
| Memory Usage | <70% | 70-85% | >85% |
| Retry Rate | <2% | 2-5% | >5% |

### Alert Response Times

| Severity | Response Time | Resolution Time |
|----------|---------------|-----------------|
| Critical | 15 minutes | 1 hour |
| Warning | 2 hours | 4 hours |
| Info | Next business day | 1 week |

---

## Escalation

### Level 1: Self-Service
- Review this runbook
- Check Grafana dashboard
- Review test logs

### Level 2: Team Lead
- Persistent failures (>3 consecutive)
- Unknown alert types
- Infrastructure issues

### Level 3: DevOps/Engineering
- Docker infrastructure failures
- Prometheus/Grafana issues
- Network connectivity problems

---

## Maintenance Tasks

### Weekly
- [ ] Review flaky test trends
- [ ] Check Prometheus storage usage
- [ ] Validate alert configurations

### Monthly
- [ ] Update Docker base images
- [ ] Review and archive old test results
- [ ] Performance optimization review

### Quarterly
- [ ] Alert threshold review
- [ ] Dashboard usability assessment
- [ ] Runbook accuracy verification

---

## Related Documentation

- [E2E Testing Guide](../../02-development/testing/e2e-testing-guide.md)
- [Docker Compose Configuration](../../../apps/web/docker-compose.e2e.yml)
- [Prometheus Alerts](../../../infra/prometheus/alerts/e2e-tests.yml)
- [Grafana Dashboard](../../../infra/grafana/dashboards/e2e-testing.json)
- [Issue #2009](https://github.com/meepleai/meepleai-monorepo/issues/2009)

---

## Contact

- **QA Team**: qa-team@meepleai.com
- **DevOps**: devops@meepleai.com
- **On-Call**: Slack `#e2e-testing-alerts`