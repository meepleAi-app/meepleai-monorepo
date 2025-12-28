# K6 Performance Testing Guide

**Issue:** #873
**Status:** Production Ready
**Last Updated: 2025-12-13T10:59:23.970Z

## Overview

This guide covers the k6 load testing infrastructure for MeepleAI API endpoints. The system validates performance targets, enforces SLA thresholds, and prevents backend performance regressions.

## Table of Contents

- [Quick Start](#quick-start)
- [Performance Targets](#performance-targets)
- [Test Scenarios](#test-scenarios)
- [Running Tests](#running-tests)
- [CI/CD Integration](#cicd-integration)
- [Regression Detection](#regression-detection)
- [Interpreting Results](#interpreting-results)
- [Troubleshooting](#troubleshooting)

## Quick Start

```bash
# Install k6 (first time only)
# macOS:
brew install k6

# Linux:
sudo gpg -k
sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
sudo apt-get update
sudo apt-get install k6

# Configure environment
cd tests/k6
cp config/env.example.json config/env.json
# Edit config/env.json with your API URL and credentials

# Run smoke test (quick validation)
npm run test:smoke

# Run full load test
npm run test:load
```

## Performance Targets

| Endpoint | P95 Latency | Throughput | Error Rate | Priority |
|----------|-------------|------------|------------|----------|
| **RAG Search** | < 2000ms | 1000 req/s | < 1% | Critical |
| **Chat (QA)** | < 1000ms | 500 req/s | < 1% | Critical |
| **Game Search** | < 500ms | 2000 req/s | < 0.5% | High |
| **Session Mgmt** | < 100ms | 1000 req/s | < 0.1% | High |
| **Database Ops** | < 200ms | - | < 1% | Medium |
| **Redis Cache** | < 50ms | - | < 0.1% | Medium |

## Test Scenarios

### 1. RAG Search (`scenarios/rag-search.js`)

Tests the hybrid RAG search endpoint with vector + keyword fusion.

**Endpoint:** `POST /api/v1/knowledge-base/search`

**Load Profile:**
- Smoke: 10 VUs for 30s
- Load: 100 VUs ramping over 9min
- Stress: 200 VUs sustained for 9min

**Metrics:**
- Response time (P50, P95, P99)
- RAG confidence scores
- Snippet counts
- Token usage

```bash
npm run test:rag-search
```

### 2. Chat Messaging (`scenarios/chat.js`)

Tests the Q&A endpoint with follow-up question generation.

**Endpoint:** `POST /api/v1/agents/qa`

**Load Profile:**
- Smoke: 5 VUs for 30s
- Load: 50 VUs ramping over 9min
- Stress: 100 VUs sustained for 9min

**Metrics:**
- Response time
- Answer quality
- Follow-up question generation

```bash
npm run test:chat
```

### 3. Game Search (`scenarios/games.js`)

Tests the game catalog endpoint (read-heavy, high throughput).

**Endpoint:** `GET /api/v1/games`

**Load Profile:**
- Smoke: 20 VUs for 30s
- Load: 200 VUs ramping over 9min
- Stress: 400 VUs sustained for 9min

**Metrics:**
- Response time
- Throughput (requests/sec)
- Cache effectiveness

```bash
npm run test:games
```

### 4. Session Management (`scenarios/sessions.js`)

Tests session retrieval endpoint (ultra-fast target).

**Endpoint:** `GET /api/v1/auth/sessions`

**Load Profile:**
- Smoke: 10 VUs for 30s
- Load: 100 VUs ramping over 9min
- Stress: 200 VUs sustained for 9min

**Metrics:**
- Response time (target: <100ms P95)
- Session data consistency

```bash
npm run test:sessions
```

### 5. Database Stress (`scenarios/database-stress.js`)

Tests database performance under various load patterns.

**Scenarios:**
- Complex queries (joins, aggregations)
- Concurrent writes (write contention)
- Read-heavy workload (read scalability)

**Metrics:**
- Query execution time
- Connection pool utilization
- Deadlock detection

```bash
npm run test:database
```

### 6. Redis Cache (`scenarios/redis-cache.js`)

Validates cache effectiveness and eviction behavior.

**Scenarios:**
- Cache hit test (repeated queries)
- Cache miss test (unique queries)
- Cache eviction (high volume)

**Metrics:**
- Cache hit rate (target: >80%)
- Cache operation latency
- Eviction rate

```bash
npm run test:redis
```

### 7. WebSocket Load (`scenarios/websocket.js`)

Tests WebSocket connection stability and message throughput.

**Target:** 1000+ concurrent connections

**Load Profile:**
- Ramp: 10 → 100 → 500 → 1000 VUs over 6min
- Hold: 1000 VUs for 3min
- Ramp down: 1000 → 100 → 0 over 3min

**Metrics:**
- Connection duration
- Message throughput
- Error rate

```bash
npm run test:websocket
```

## Running Tests

### Test Types

#### Smoke Test
Quick validation (1 minute, 1-20 VUs)
```bash
npm run test:smoke
```

#### Load Test (Default)
Sustained normal traffic (10 minutes, 50-200 VUs)
```bash
npm run test:load
# or
npm run test
```

#### Stress Test
Beyond normal capacity (15 minutes, 200-1000 VUs)
```bash
npm run test:stress
```

#### Spike Test
Sudden traffic surge (10 minutes, 10 → 500 instant spike)
```bash
npm run test:spike
```

### Run All Tests
```bash
npm run test
# or
k6 run scenarios/all.js
```

### Run Specific Endpoint
```bash
npm run test:rag-search
npm run test:chat
npm run test:games
npm run test:sessions
```

### Custom Test Type
```bash
k6 run --env TEST_TYPE=stress scenarios/rag-search.js
```

## CI/CD Integration

### GitHub Actions Workflow

**File:** `.github/workflows/k6-performance.yml`

**Status:** ✅ **ACTIVE** (Re-enabled 2025-12-28 - Issue #2286)

**Previous Status:** Temporarily disabled during CI modernization (commit c986389e)

**Recent Improvements (Issue #2286):**
- ✅ Increased API readiness timeout (30→60 retries)
- ✅ Exponential backoff retry logic (1s→5s max)
- ✅ Enhanced error logging and diagnostics
- ✅ Silent health checks for faster startup detection

**Triggers:**
- **Nightly:** 2 AM UTC (full load test)
- **PR:** On API changes (smoke test)
- **Manual:** Workflow dispatch (any test type)

**Jobs:**
1. Setup infrastructure (Postgres, Redis, Qdrant)
2. Build and start API
3. Run k6 tests
4. Generate HTML report
5. Detect regressions
6. Upload artifacts
7. Post PR comment (if applicable)

**Artifacts:**
- Performance reports (JSON/HTML) - 30 days
- API logs - 7 days

### Nightly Benchmarking

Automated nightly runs track performance trends over time:

1. Run full load test suite
2. Compare against previous baseline
3. Detect regressions (>10% degradation)
4. Archive results for trend analysis
5. Alert on failures (Slack/email)

## Regression Detection

### Automatic Detection

The CI pipeline automatically detects regressions on PRs:

```bash
node utils/detect-regression.js baseline.json current.json
```

**Threshold:** 10% degradation in any metric

**Metrics Compared:**
- Overall response time (P95)
- Endpoint-specific latency (P95)
- Error rate
- Throughput

**Example Output:**
```
✅ OK          RAG Search Response Time (P95)    1823.45 → 1891.23 (+3.71%)
❌ REGRESSION  Chat Response Time (P95)          856.12 → 1045.67 (+22.14%)
✅ OK          Games Response Time (P95)         324.89 → 298.45 (-8.14%)
```

### Manual Comparison

```bash
cd tests/k6

# Run baseline test
k6 run scenarios/all.js > baseline.json

# Make changes...

# Run current test
k6 run scenarios/all.js > current.json

# Detect regressions
node utils/detect-regression.js \
  reports/baseline.json \
  reports/current.json \
  15  # 15% threshold (optional)
```

## Interpreting Results

### Summary Report

After each run, k6 generates a summary report:

```
=============================================================
  MeepleAI Performance Test Suite - LOAD
=============================================================

📊 Overall Statistics
-------------------------------------------------------------
  Total Requests: 125,432
  Failed Requests: 87 (0.07%)
  Requests/sec: 209.05
  Data Received: 45.23 MB
  Data Sent: 12.34 MB

⏱️  Response Times
-------------------------------------------------------------
  Average: 234.56ms
  P50: 198.23ms
  P95: 456.78ms
  P99: 789.12ms
  Max: 1234.56ms

🎯 Endpoint Performance
-------------------------------------------------------------
  ✓ RAG Search            P95: 1823.45ms (threshold: 2000ms)
  ✓ Chat                  P95: 856.12ms  (threshold: 1000ms)
  ✓ Games                 P95: 324.89ms  (threshold: 500ms)
  ✓ Sessions              P95: 67.23ms   (threshold: 100ms)
```

### HTML Dashboard

Generate visual HTML report:

```bash
npm run report
# Opens reports/summary.html in browser
```

The dashboard includes:
- Overall metrics cards
- Response time breakdown table
- Endpoint performance comparison
- Custom metrics (RAG confidence, cache hit rate, etc.)
- Threshold pass/fail status

### Key Metrics

**Response Time Distribution:**
- **P50 (Median):** 50% of requests faster than this
- **P95:** 95% of requests faster than this (SLA target)
- **P99:** 99% of requests faster than this (outlier detection)

**Throughput:**
- **Requests/sec:** Sustained request rate
- **Data Transferred:** Network bandwidth usage

**Error Rate:**
- **Failed Requests:** HTTP errors (4xx, 5xx)
- **Timeout Errors:** Requests exceeding timeout

**Custom Metrics:**
- **RAG Confidence:** Average confidence scores (target: >0.70)
- **Cache Hit Rate:** Percentage of cache hits (target: >80%)
- **DB Query Time:** Database operation latency

## Troubleshooting

### Common Issues

#### k6 Not Installed
```bash
# macOS
brew install k6

# Linux
wget https://github.com/grafana/k6/releases/download/v0.47.0/k6-v0.47.0-linux-amd64.tar.gz
tar -xzf k6-v0.47.0-linux-amd64.tar.gz
sudo cp k6-v0.47.0-linux-amd64/k6 /usr/local/bin/
```

#### API Not Running
```bash
# Check API health
curl http://localhost:8080/health

# Start API if needed
cd apps/api/src/Api
dotnet run
```

#### Authentication Failures
```bash
# Verify test user exists
# If not, create via API:
curl -X POST http://localhost:8080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@meepleai.dev","password":"Test123!","displayName":"Test User"}'

# Update config/env.json with credentials
```

#### High Error Rates
1. Check API logs for errors
2. Verify database connectivity
3. Check Redis/Qdrant availability
4. Reduce VU count and retry
5. Increase timeout thresholds temporarily

#### Threshold Violations
1. Review performance report for bottlenecks
2. Check database query performance
3. Verify cache hit rates
4. Profile API endpoints
5. Scale infrastructure if needed

#### WebSocket Connection Failures
```bash
# Check WebSocket endpoint
wscat -c ws://localhost:8080/ws

# Verify session token is valid
# Check firewall/proxy settings
```

## Best Practices

### 1. Always Start with Smoke Tests
Before running full load tests, validate with smoke tests:
```bash
npm run test:smoke
```

### 2. Use Realistic Data
- Vary queries, game IDs, user sessions
- Use production-like data volumes
- Simulate real user behavior patterns

### 3. Monitor System Resources
While tests run, monitor:
- CPU usage (`top`, `htop`)
- Memory consumption
- Disk I/O
- Network bandwidth
- Database connection pools

### 4. Ramp Up Gradually
Avoid instant spikes to max VUs:
```javascript
stages: [
  { duration: '2m', target: 50 },   // Ramp up
  { duration: '5m', target: 50 },   // Hold
  { duration: '2m', target: 0 },    // Ramp down
]
```

### 5. Set Appropriate Thresholds
Fail fast on violations:
```javascript
thresholds: {
  'http_req_duration': ['p(95)<2000'],
  'http_req_failed': ['rate<0.01'],
}
```

### 6. Archive Reports
Track performance trends over time:
```bash
# Archive with timestamp
cp reports/summary.json reports/archive/$(date +%Y%m%d_%H%M%S).json
```

### 7. Run Tests in Isolation
For accurate results:
- Close unnecessary applications
- Use dedicated test environment
- Avoid concurrent operations
- Disable unnecessary services

## Architecture

```
tests/k6/
├── config/
│   ├── env.example.json      # Config template
│   ├── env.json             # Local config (gitignored)
│   └── thresholds.js        # Performance SLAs
├── scenarios/
│   ├── all.js               # Run all tests
│   ├── rag-search.js        # RAG endpoint
│   ├── chat.js              # Chat endpoint
│   ├── games.js             # Games endpoint
│   ├── sessions.js          # Sessions endpoint
│   ├── database-stress.js   # DB load tests
│   ├── redis-cache.js       # Cache tests
│   └── websocket.js         # WebSocket tests
├── utils/
│   ├── auth.js              # Authentication helpers
│   ├── common.js            # Shared utilities
│   ├── metrics.js           # Custom metrics
│   ├── generate-report.js   # HTML report generator
│   └── detect-regression.js # Regression detection
├── reports/                 # Generated reports (gitignored)
├── .gitignore
├── package.json
└── README.md
```

## Related Documentation

- [Performance Testing Guide](./performance-testing-guide.md) (Lighthouse CI)
- [Integration Tests Guide](./integration-tests-performance-guide.md)
- [API Specification](../../03-api/board-game-ai-api-specification.md)
- [CI/CD Pipeline](../../03-deployment/ci-cd-pipeline.md)

---

**Issue**: #873
**Maintained by**: Engineering Team
**Last Updated**: 2025-12-13T10:59:23.970Z