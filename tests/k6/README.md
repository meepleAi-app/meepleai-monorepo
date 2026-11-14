# K6 Performance Tests

Load testing suite for MeepleAI API using [K6](https://k6.io/).

## Overview

This directory contains K6 performance tests for validating API performance under various load conditions. Tests are executed automatically in CI/CD pipelines and can be run manually for local testing.

## Directory Structure

```
tests/k6/
├── scenarios/
│   └── admin-polling.js    # ISSUE-2918: Admin dashboard polling test
├── utils/
│   └── generate-report.js  # HTML report generator
├── reports/                # Generated test reports (gitignored)
└── README.md              # This file
```

## Scenarios

### Admin Polling (Issue #2918)

**File**: `scenarios/admin-polling.js`

Simulates realistic admin dashboard usage with 100 concurrent administrators polling metrics every 30 seconds.

**Test Types**:
- **Smoke** (5 VUs, 1 min): Quick validation for CI/CD
- **Load** (100 VUs, 5 min): Realistic concurrent usage
- **Stress** (0→200 VUs, 10 min): Peak load testing

**Performance Targets**:
- Response time p95 < 500ms
- Response time p99 < 1s
- Error rate < 1%
- Cache hit rate > 70%

**Tested Endpoints**:
- `/api/v1/admin/llm/efficiency-report`
- `/api/v1/admin/llm/monthly-report`
- `/api/v1/admin/llm/model-recommendations`
- `/api/v1/admin/reports/scheduled`
- `/api/v1/admin/reports/executions?limit=20`
- `/api/v1/admin/ai-models`
- `/api/v1/admin/tier-routing`
- `/api/v1/admin/alert-configuration`
- `/api/v1/admin/alert-rules`

## Prerequisites

### Local Development

1. **Install K6**:
   ```bash
   # macOS (Homebrew)
   brew install k6

   # Windows (Chocolatey)
   choco install k6

   # Windows (Manual)
   # Download from https://github.com/grafana/k6/releases

   # Linux: the release tarball, as CI does (the version and sha256 are in the
   # "Install k6" step of .github/workflows/test-performance.yml)
   curl -fsSL -o k6.tar.gz https://github.com/grafana/k6/releases/download/v2.3.0/k6-v2.3.0-linux-amd64.tar.gz
   echo "<sha256 from the workflow>  k6.tar.gz" | sha256sum -c -
   tar xzf k6.tar.gz && sudo install -m 0755 k6-v2.3.0-linux-amd64/k6 /usr/local/bin/k6
   ```

   Not the apt repository: its GPG key changed without notice between two nightly runs (#4978).

2. **Install Node.js** (for report generation):
   ```bash
   # Verify Node.js installation
   node --version  # Should be v18+ or v20+
   ```

3. **Start API server**:
   ```bash
   cd apps/api/src/Api
   dotnet run --environment Development
   ```

## Running Tests

### Quick Smoke Test (Recommended for Local)

```bash
cd tests/k6
k6 run --env TEST_TYPE=smoke scenarios/admin-polling.js
```

**Duration**: ~1 minute | **VUs**: 5

### Load Test (Realistic Usage)

```bash
cd tests/k6
k6 run --env TEST_TYPE=load scenarios/admin-polling.js
```

**Duration**: ~5 minutes | **VUs**: 100

### Stress Test (Peak Load)

```bash
cd tests/k6
k6 run --env TEST_TYPE=stress scenarios/admin-polling.js
```

**Duration**: ~10 minutes | **VUs**: 0→200

### Custom Configuration

Override defaults with environment variables:

```bash
# Custom API URL
API_BASE_URL=http://localhost:5000 k6 run scenarios/admin-polling.js

# The accounts: TEST_USER_* for the user scenarios, TEST_ADMIN_* for the admin ones
# (admin-polling, admin-concurrent-actions). They must exist: the defaults of
# utils/shared-config.js are not created by any seed. CI logs in with the API's initial
# admin (INITIAL_ADMIN_EMAIL / INITIAL_ADMIN_PASSWORD) for both.
TEST_ADMIN_EMAIL=admin@test.com \
TEST_ADMIN_PASSWORD=MyPassword123! \
k6 run scenarios/admin-polling.js

# Specific scenario (bypassing TEST_TYPE)
k6 run --scenario smoke scenarios/admin-polling.js
```

## Report Generation

After running tests, generate an HTML report:

```bash
# Automatically generates reports/performance-report.html
node utils/generate-report.js reports/admin-polling-summary.json
```

Open `reports/performance-report.html` in your browser to view:
- Key performance metrics (p95, p99, error rate, cache hit rate)
- Detailed statistics (min, max, avg, median, percentiles)
- Test summary (total requests, failures, checks)
- Visual status indicators (pass/fail)

## CI/CD Integration

### GitHub Actions Workflow

**File**: `.github/workflows/test-performance.yml`, job `K6 Performance Tests`.

**Triggers**:
1. **Pull Request** to `main` that touches the API or `tests/k6/`, and to `main-dev` that touches `tests/k6/` or the workflow itself
2. **Schedule**: nightly at 2 AM UTC, on `main-dev`
3. **Manual**: workflow dispatch, with `test_type`, `k6_scenario` and `notify_on_failure`

**What it checks** (#4978): a smoke run is a correctness check, not a performance gate.
- The API starts with a fresh database and seeds its initial admin; a preflight login with that account must answer 200.
- `utils/run-scenarios.sh` runs the scenarios one by one and fails if any k6 run exits non-zero. With `TEST_TYPE=smoke` each file runs only its smoke scenario, and only `auth_errors` and `checks` are thresholds (`thresholdsFor` in `utils/shared-config.js`): latency stays in the summary, as information. The duration of each scenario goes to the step summary.
- `all` leaves out the two RAG scenarios: they need an agent with indexed documents and an LLM, which the job does not have.
- The baseline check fails when there is no `*-summary.json` to compare.

**Artifacts**:
- Performance reports (JSON summaries): 30-day retention
- API logs (debugging): 7-day retention

**Notifications**: on a red nightly run, or a dispatch with `notify_on_failure`, the workflow opens the issue "Performance Tests Failed - Investigation Required", or comments on it if it is open. On PRs the job is `continue-on-error`, and `.github/release-gates.yml` classifies it.

## Performance Baselines

### Admin Polling Scenario

Based on Issue #2918 DoD:

| Metric | Target | Baseline (Expected) |
|--------|--------|---------------------|
| Response Time (p95) | < 500ms | ~300ms |
| Response Time (p99) | < 1s | ~600ms |
| Error Rate | < 1% | ~0.1% |
| Cache Hit Rate | > 70% | ~85% |
| CPU Usage | < 70% | ~50% |
| Memory Usage | < 80% | ~60% |

**Note**: Baselines updated after first successful production run.

## Troubleshooting

### Common Issues

#### 1. API Not Ready

**Error**: `API health check failed`

**Solution**:
```bash
# Verify API is running
curl http://localhost:8080/health/live

# Check API logs
cd apps/api/src/Api
dotnet run --environment Development
```

#### 2. Authentication Failures

**Error**: `auth_errors > 0`

**Solution**:
- Verify the test accounts exist in the database (`TEST_USER_*`, `TEST_ADMIN_*`)
- The API authenticates with the `meepleai_session` cookie, not with a token: `/auth/login` answers `{ user, expiresAt }`, and an `Authorization: Bearer` header is ignored
- A login for an unknown email answers 400
- Review rate limiting settings (should be disabled for tests)

#### 3. High Error Rates

**Error**: `http_req_failed > 1%`

**Solution**:
- Check API logs for exceptions
- Verify database connectivity
- Ensure sufficient system resources (CPU, memory)
- Review endpoint-specific errors in K6 output

#### 4. Report Generation Fails

**Error**: `<scenario>-summary.json not found`

**Solution**:
```bash
# Ensure reports directory exists: each scenario's handleSummary writes
# reports/<scenario>-summary.json there
mkdir -p reports
k6 run --env TEST_TYPE=smoke scenarios/admin-polling.js
```

### Debug Mode

Enable verbose K6 output:

```bash
# HTTP debug (request/response details)
k6 run --http-debug="full" scenarios/admin-polling.js

# Console logs from test script
k6 run --console-output=stdout scenarios/admin-polling.js

# Verbose K6 logs
k6 run --verbose scenarios/admin-polling.js
```

## Best Practices

### 1. Rate Limiting

**CRITICAL**: Disable rate limiting for K6 tests in CI:

```bash
# API configuration (Development/CI environments)
DISABLE_RATE_LIMITING=true
RateLimiting__MaxTokens__user=999999
RateLimiting__RefillRate__user=10000
```

**Never** run load tests with rate limiting enabled - tests will fail immediately.

### 2. Test Isolation

- Use dedicated test database (`meepleai_test`)
- Clean state before/after tests
- Avoid testing against production endpoints

### 3. Realistic Scenarios

- Use weighted endpoint distribution (mimic real usage)
- Include authentication flows
- Simulate think time between requests (e.g., 30s polling interval)

### 4. Incremental Load

For stress testing:
1. Start with smoke test (validate functionality)
2. Run load test (baseline performance)
3. Run stress test (identify breaking point)

**Never** start with stress test - it may crash unprepared systems.

## Metrics Glossary

| Metric | Description | Target |
|--------|-------------|--------|
| **p50 (median)** | 50% of requests faster than this | Informational |
| **p95** | 95% of requests faster than this | < 500ms |
| **p99** | 99% of requests faster than this | < 1s |
| **Error Rate** | % of failed HTTP requests | < 1% |
| **Cache Hit Rate** | % of requests served from cache | > 70% |
| **VUs (Virtual Users)** | Concurrent simulated users | Scenario-dependent |

## Additional Resources

- [K6 Documentation](https://k6.io/docs/)
- [K6 Thresholds Guide](https://k6.io/docs/using-k6/thresholds/)
- [K6 Scenarios](https://k6.io/docs/using-k6/scenarios/)
- [K6 Metrics](https://k6.io/docs/using-k6/metrics/)
- [MeepleAI API Documentation](http://localhost:8080/scalar/v1)

## Related Issues

- **#2918**: Admin Dashboard Load Testing (this implementation)
- **#2286**: K6 Workflow Re-enablement
- **#2284**: Non-blocking Performance Monitoring
- **#2152**: Database Connection Fixes

## Maintainers

Performance testing infrastructure maintained by the MeepleAI development team.

For questions or issues, please:
1. Check troubleshooting section above
2. Review existing GitHub issues
3. Create new issue with `kind/perf` label

---

**Last Updated**: 2026-01-23 (Issue #2918)
